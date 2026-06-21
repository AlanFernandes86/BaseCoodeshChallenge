# Testes no Backend - OrderGenerator

Esta pasta contém as suítes de testes da aplicação Backend. A validação está dividida em dois projetos distintos para isolamento e velocidade de execução:

## 1. Testes Unitários (`OrderGenerator.UnitTests`)
Contém testes focados na lógica de negócio pura, executados 100% em memória, instantaneamente e sem qualquer dependência externa (banco de dados, rede ou containers).
- **O que testam:** Validação de formato de requisições de ordens (`OrderValidator`), geração thread-safe de identificadores de ordens (`OrderTracker`) e casos de uso de submissão e fila outbox (`SubmitOrderUseCase`).
- **Como executar:**
  ```bash
  dotnet test tests/OrderGenerator.UnitTests/OrderGenerator.UnitTests.csproj
  ```

## 2. Testes Integrados (`OrderGenerator.IntegratedTests`)
Contém testes que levantam a API real em memória (`TestServer`) ou interagem com dependências simuladas e reais. Ele é dividido em duas categorias:

### A. Testes Integrados Mockados (`Controllers/` e `Services/`)
Utilizam a classe `MockedWebApplicationFactory` para interceptar as injeções de dependência da infraestrutura FIX (`IFIXOrderSubmissionService`).
- Eles sobem a WebAPI inteira na memória para testar as rotas HTTP e SignalR de forma totalmente isolada.
- Podem rodar em qualquer ambiente de integração contínua (CI/CD) nativamente.

### B. Testes End-to-End (`E2E/`)
O teste `AccumulatorE2ETests` valida o fluxo real de ponta a ponta (Cliente -> SignalR Hub -> FIX Initiator -> TCP Connection -> FIX Acceptor -> SignalR Notification).
- **Atenção:** Para rodar esses testes, é estritamente obrigatório que a aplicação **OrderGenerator.Backend** e a aplicação **OrderAccumulator** estejam ativas e aceitando conexões (ex: via `docker-compose up`).
- O teste vem ignorado por padrão (`[Fact(Skip = "...")]`) para não quebrar pipelines automatizados de CI/CD. Remova a propriedade `Skip` para executá-lo localmente.

### Como executar os testes integrados:
```bash
dotnet test tests/OrderGenerator.IntegratedTests/OrderGenerator.IntegratedTests.csproj
```
