# FIX Order System

Um sistema de acumulação de ordens financeiras com limite de exposição por ativo comunicando-se via protocolo FIX 4.4, com interface web em React e backend em .NET.

## Tecnologias Usadas

- **Linguagens:** C#, TypeScript, HTML/CSS
- **Backend:** .NET (ASP.NET Core, Worker Services), SignalR
- **Protocolo:** QuickFIX/n (FIX 4.4)
- **Frontend:** React, Vite
- **Infraestrutura:** Docker, Docker Compose

## Como Instalar e Usar

**Pré-requisitos:**
- Docker e Docker Compose instalados em sua máquina.

**Instruções de execução:**

1. Clone o repositório para a sua máquina e acesse a pasta raiz do projeto:
   ```bash
   git clone <url-do-repositorio>
   cd BaseCoodeshChallenge
   ```

2. Faça o build e inicie os containers com o Docker Compose:
   ```bash
   docker-compose up --build
   ```

3. Acesse o front no seu navegador:
   - **Frontend (Interface do Usuário):** [http://localhost:5173](http://localhost:5173)

O ambiente inicia os três componentes principais:
- **OrderAccumulator (`9999`):** Aceita conexões FIX, recebe as ordens e calcula a exposição de risco (simulada).
- **OrderGenerator.Backend (`5000`):** API e servidor SignalR que envia as ordens criadas pelo usuário via FIX para o Accumulator.
- **OrderGenerator.Frontend (`5173`):** Aplicação visual que interage com o Backend para emitir e acompanhar os status das ordens em tempo real.

---

>  This is a challenge by [Coodesh](https://coodesh.com/)