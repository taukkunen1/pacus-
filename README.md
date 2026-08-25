# PACUS

PACUS é um aplicativo de rotina com histórico diário, tarefas dinâmicas, Pacus Points e crescimento independente do desempenho.

## Arquitetura

Frontend Web (HTML/CSS/JS) -> ASP.NET Core 10 / C# -> MongoDB Atlas

Flutter será um cliente futuro da mesma API.

## Regras centrais
- Cada dia começa às 00:00 no timezone do usuário.
- O histórico dos dias encerrados é preservado.
- As tarefas do dia são independentes da configuração permanente.
- Existem três tipos: `mandatory`, `expected`, `challenge`.
- Cada tarefa vale 1, 2 ou 3 Pacus Points.
- Tarefa concluída ganha pontos; não concluída ganha zero e não perde saldo.
- 1 Pacus Point equivale a R$ 0,05.
- O PACUS cresce uma vez por dia encerrado, independentemente da conclusão das tarefas.
- A criança pode alterar somente as tarefas do dia atual, conforme permissões.
- O adulto administra regras permanentes, configurações e histórico autorizado.

## Desenvolvimento local

### API
1. Configure `MONGODB_URI`, `MONGODB_DATABASE` e `JWT_SECRET`.
2. Execute `dotnet restore backend/Pacus.sln`.
3. Execute `dotnet run --project backend/src/Pacus.Api`.
4. Health: `GET /api/v1/health`.

### Frontend
Sirva `frontend/` por um servidor HTTP, por exemplo `python -m http.server 5500 --directory frontend`, e configure `window.PACUS_API_BASE_URL` em `frontend/index.html` se necessário.

## CI/CD
`.github/workflows/ci.yml` testa backend e sintaxe JavaScript.
`.github/workflows/pages.yml` prepara deploy do frontend no GitHub Pages.

## Deploy da API
`backend/Dockerfile` gera uma imagem ASP.NET Core 10. `deploy/docker-compose.yml` documenta a execução com MongoDB Atlas externo. O provedor de hospedagem da API ainda precisa ser escolhido.

## Segurança
Nunca commitar senha do MongoDB, JWT secret ou connection strings reais.
