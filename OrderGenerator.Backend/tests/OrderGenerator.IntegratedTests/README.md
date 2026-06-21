# Testes Integrados - OrderGenerator.Backend

Este projeto contém a suíte de testes integrados da aplicação. Ele é dividido em duas categorias principais:

## 1. Testes Mockados (`Controllers/` e `Services/`)
Utilizam o `MockedWebApplicationFactory` para interceptar as injeções de dependência da infraestrutura FIX (`IFIXOrderSubmissionService`).
- Eles sobem a WebAPI inteira em memória usando `TestServer`.
- Permitem testar rotas HTTP, validações (`OrderValidator`) e regras do `OutboxQueue` de forma **totalmente isolada**, instantânea e sem necessidade de rede.
- Podem rodar em qualquer ambiente de CI/CD nativamente.

## 2. Testes End-to-End (`E2E/`)
O teste `AccumulatorE2ETests` valida o fluxo real de ponta a ponta (Cliente -> SignalR Hub -> FIX Initiator -> Rede -> FIX Acceptor -> Retorno).
- **Atenção**: Para que esse teste funcione, é estritamente obrigatório que a aplicação remota **OrderAccumulator** esteja ativa e aceitando conexões na porta `9999`.
- O ideal é rodar o ambiente com `docker-compose up` antes de rodar a suíte E2E.
- O teste está ignorado (`Skip = "..."`) por padrão para não quebrar pipelines de CI que não sobem a infraestrutura FIX. Para rodar na sua máquina, basta remover a anotação `[Fact(Skip = "...")]`.
