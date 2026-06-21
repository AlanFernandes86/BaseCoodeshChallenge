# OrderGenerator.Backend – Gerador de Ordens FIX

O **OrderGenerator.Backend** recebe ordens da interface via HTTP ou SignalR e as envia para o `OrderAccumulator` utilizando o protocolo FIX. O projeto utiliza **Clean Architecture** e adota um fluxo **Fire-and-Forget** (orientado a eventos) com suporte offline (**Outbox**).

---

## Fluxo de Execução e Arquitetura

1. **Recepção e Validação**:
   - `OrdersController` (REST) e `OrderHub` (SignalR) recebem as requisições.
   - Validações de negócio (ticker, limites, múltiplos de preço) são feitas centralizadamente pelo `OrderValidator`.
   - Se validada, a ordem é despachada para o `ISubmitOrderUseCase`.

2. **Processamento Fire-and-Forget**:
   - O `SubmitOrderUseCase` envia a ordem via FIX (ou a guarda no `OrderTracker.OutboxQueue` caso offline) e **retorna imediatamente** um status `PENDING` ao cliente via HTTP ou SignalR.

3. **Retorno Assíncrono (Event-Driven)**:
   - O `FIXInitiatorService` escuta os retornos do Acceptor.
   - Ao receber uma resposta (Aceita ou Rejeitada), dispara o evento `ExecutionReportReceived`.
   - O `HandleExecutionReportService` escuta este evento e faz um broadcast (`Clients.All.SendAsync`) do modelo `SubmitOrderResponse` diretamente para o Frontend via WebSocket.

4. **Tratamento Offline (Outbox)**:
   - O `OrderOutboxService` (BackgroundService) monitora ativamente a fila de outbox.
   - Assim que o serviço FIX acusa conexão (`IsSessionActive`), as ordens enfileiradas são enviadas progressivamente para o acumulador.

---

## Regras de Validação (OrderValidator)

* **Símbolo**: Apenas `PETR4`, `VALE3` ou `VIIA4`.
* **Lado**: `Buy` (Compra) ou `Sell` (Venda).
* **Quantidade**: Inteiro positivo menor que `100.000`.
* **Preço**: Decimal positivo menor que `1.000` e múltiplo estrito de `0,01`.

---

## Como Executar

1. Requer **.NET 10.0+**.
2. Restaure e compile:
   ```bash
   cd OrderGenerator.Backend
   dotnet build
   ```
3. Inicie a aplicação (o Acceptor local ou remoto deve estar configurado no `fix.cfg`):
   ```bash
   dotnet run --project src/OrderGenerator.WebAPI
   ```
A API iniciará nos endpoints configurados no `launchSettings.json` (ex: `http://localhost:5000`).
