# Architecture Decision Records — BaseCoodeshChallenge

## Visão Geral do Sistema

3 projetos separados comunicando via **FIX 4.4 (QuickFIX/n)**:
- `OrderAccumulator` — console app, FIX Acceptor, controle de exposição
- `OrderGenerator.Backend` — ASP.NET Core, FIX Initiator, SignalR server
- `OrderGenerator.Frontend` — React/Vite, formulário de ordens

Orquestração via **Docker Compose** com rede bridge `fix-network` para DNS entre containers.

---

## OrderGenerator.Backend

### Arquitetura: Clean Architecture
**Motivo:** aplicação de borda com cadência humana (formulário), crescimento esperado de regras de negócio. Clean Architecture justificada por manutenibilidade e testabilidade, não por convenção.
Camadas: Domain → Application → Infrastructure → WebAPI.

### Validação de formato
Símbolo, quantidade e preço validados **aqui**, não no Accumulator. O Accumulator confia que o que chega via FIX já está bem-formado.

### SignalR em vez de REST simples
Decisão de cenário real — push assíncrono do resultado da ordem ao frontend. Correlação de resposta FIX via `TaskCompletionSource` por `ClOrdID`, necessário porque QuickFIX/n recebe `ExecutionReport` em thread diferente da que enviou a ordem.

### Outbox assíncrono em memória (`ConcurrentQueue`)
**Motivo:** evitar falha no envio quando o Accumulator está offline. Ordens são salvas como `PENDING` imediatamente e enviadas em background por `FIXOutboxWorker` assim que a sessão FIX é reestabelecida — sem perda de experiência do usuário durante indisponibilidade do Accumulator.

### Generic Host em vez de loop procedural
`while(true)` substituído por `IHostedService` (`FIXAcceptorHostedService`). Permite DI nativa e graceful shutdown  e fecha sockets FIX de forma limpa.


### Testes
Divididos em duas categorias:
- **Mockados:** WebAPI em memória (`TestServer`) com camada FIX mockada — testa validações HTTP e mecanismo de Outbox sem dependência de rede
- **E2E:** requisições reais via SignalR à porta `5000` do container Docker ativo — marcados com `Skip` por padrão para não quebrar CI/CD sem containers ativos

---

## OrderAccumulator

### Arquitetura: enxuta, sem over-abstraction
**Motivo:** caminho crítico de latência, propósito único (aceitar/rejeitar ordens por exposição).

`FIXAcceptorApplication` faz a ponte: recebe FIX → chama `ExposureState` → responde FIX.

### ExposureState — decisões internas

**`Dictionary<string, decimal>` + `lock` privado**
Não `ConcurrentDictionary` — atomicidade precisa cobrir a sequência ler→decidir→escrever, não só cada operação isolada. `ConcurrentDictionary` garante atomicidade por operação, não por regra de negócio.

Alternativas avaliadas e descartadas:
- `SemaphoreSlim` — mais verboso, overhead maior, vantagem real só com `await` dentro da seção crítica (não é o caso aqui)
- `Channel` — eliminaria o lock via consumidor único, mas perderia o resultado síncrono necessário para responder o `ExecutionReport` imediatamente; adicionaria latência de troca de contexto e complexidade de correlação por `TaskCompletionSource`
- Disruptor/Ring Buffer — correto para volume HFT (busy spin, array fixo por índice, zero alocações no hot path), desproporcional ao escopo atual

**`OrderInfo` como `readonly record struct`**
Imutável por construção, sem alocação no heap, seguro para concorrência. Não reutilizado entre mensagens — struct pequeno na stack, sem pressão de GC, sem risco de corrida por campo compartilhado mutável.

**`_orderIdCounter` incrementado para toda ordem (aceita e rejeitada)**
Garante consistência do `OrderID` após replay. Sem isso, ordens geradas após restart teriam IDs que colidem com IDs históricos.

**`ApplyHistoricalExposure` e `AdvanceOrderIdCounter` sem lock**
Rodam exclusivamente durante startup, antes do Acceptor iniciar — sem concorrência possível nesse momento.

**`GenerateExecID` com `Interlocked.Increment`**
Não `lock(typeof(...))` — anti-padrão que expõe objeto de lock global. `Interlocked.Increment` resolve atomicidade de incremento sem overhead de monitor e sem risco de contenção externa.

**`ExposureState` é singleton**
Thread-safety necessária porque `ThreadedSocketAcceptor` roda uma thread dedicada por sessão — múltiplas sessões chamam `FromApp` (e consequentemente `TryProcessOrder`) concorrentemente.

### Testes unitários dedicados
`OrderAccumulator.UnitTests` sem dependências externas, cobrindo:
- Cálculo de impacto e regras concorrentes de limite (±R$ 100M) no `ExposureState`
- Geração thread-safe e padrão sequencial de IDs no `OrderTracker`

---

## Recovery/Replay

### Tentativa 1: `ResendRequest` via rede (descartada)
Pedir ao Backend reenviar as `NewOrderSingle` via protocolo. **Falhou:** protocolo rejeita resend que voltaria o `NextTargetMsgSeqNum` para trás do valor já conhecido — limitação de design do protocolo FIX, não de configuração. `EndSeqNo=0` ("replay tudo") gerava `Session Reject` porque `NewSeqNo < NextTargetMsgSeqNum`.

### Tentativa 2: ler `FileStore` incoming do Accumulator (descartada)
`FileStore` padrão do QuickFIX/n persiste mensagens **enviadas** (outgoing), não recebidas (incoming) — só guarda controle de sequência das recebidas, não o corpo.

### Decisão final: ler FileStore de envio do próprio Accumulator
Os `ExecutionReport` emitidos pelo Accumulator já contêm tudo necessário para reconstruir a exposição: símbolo, lado, quantidade, preço, `ExecType`. Leitura local, sem dependência de rede, sem pedir nada à contraparte.

**Fluxo de replay:**
1. Antes do Acceptor iniciar, ler todos os `ExecutionReport` do `FileStore` local
2. `ExecType=New` (aceito) → `ApplyHistoricalExposure` (aplica exposição + avança contador de ID)
3. `ExecType=Rejected` → `AdvanceOrderIdCounter` (só avança contador, sem tocar exposição)
4. Só após replay completo, iniciar o Acceptor para aceitar novas conexões

**Por que a ordem de leitura não importa para a exposição:** soma de decisões já tomadas é comutativa — processar em qualquer sequência produz o mesmo estado final. O contador de ID só precisa da contagem total correta, não da ordem.

### Prevenção de duplicidades (PossDupFlag)
Removido filtro manual de `PossDupFlag=Y` do `FromApp`. Delegado ao gerenciamento nativo de SeqNum do QuickFIX/n:
- **Mensagem gravada antes do crash:** Accumulator reconstrói exposição via replay do FileStore e rejeita duplicata via protocolo nativamente
- **Mensagem perdida no crash:** não foi processada, reprocessada de forma segura na reconexão

---

## FIX — Configurações

```properties
ResetOnLogon=N
ResetOnLogout=N
ResetOnDisconnect=N
```
Preserva SeqNum entre reinícios — integridade histórica de sincronização de mensagens.

`FileStorePath` configurado em ambos os lados — persiste histórico de sessão em disco.

`ReconnectInterval=5` no Backend (Initiator) — reconecta automaticamente quando Accumulator volta.

`MemoryStore` avaliado e descartado — menos persistente que `FileStore`, não resolve recovery.

---

## Frontend

### Redesenho visual inspirado na Base Exchange
Tema escuro profundo (`#060709` / `#0E1118`), detalhes em roxo (`#6C35FF`) e verde neon (`#00FF88`), fontes `Red Hat Display` e `Cousine` via Google Fonts.

### Combobox em vez de grid de botões
Substituição do grid de ativos por dropdown customizado React com controle de fechamento automático em cliques externos — mais compacto e profissional.

### CSS modularizado
Estilos extraídos do TSX inline para `styles.css` externo — separação de concerns, reaproveitável.

---

## Infraestrutura

### Cultura PT-BR nos containers
Instalação de locales Debian no runtime dos Dockerfiles + variáveis `LANG`, `LANGUAGE`, `LC_ALL=pt_BR.UTF-8` no `docker-compose.yml` — padroniza decimais, moedas e timestamps nos logs.

---

## Melhorias futuras documentadas (não implementadas)

**Snapshot incremental via Channel**
`ExposureState` serializado em JSON local a cada N ordens ou X segundos, combinado com SeqNum do snapshot — reduziria tempo de boot em volume histórico alto sem infraestrutura externa. Escrita atômica via arquivo temporário + rename para evitar corrupção.

**`IMessageStore` customizado**
Capturaria mensagens recebidas (incoming), não só enviadas — padrão comum em sistemas de mercado financeiro para auditoria e replay independente da contraparte.

**Disruptor/Ring Buffer para volume HFT**
Eliminaria o `lock` via consumidor único em thread dedicada com busy spin, array fixo de exposição por índice (`decimal[3]`), zero alocações no hot path. Avaliado conceitualmente — desproporcional ao escopo atual onde `lock` quase nunca tem contenção real.

---

## Uso de IA

Copilot usado para scaffolding e boilerplate. Toda lógica de negócio crítica (ExposureState, replay, recovery, decisões de concorrência) revisada e decidida manualmente. Raciocínio das principais decisões documentado neste arquivo.
