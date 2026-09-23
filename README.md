# PACUS

PACUS é um sistema de apoio à rotina familiar que ajuda crianças a desenvolver autonomia, responsabilidade e hábitos positivos por meio de pequenas tarefas diárias, acompanhamento dos adultos e reforço positivo — sem transformar a rotina numa experiência de punição. Veja o propósito completo (e o porquê de decisões como "tarefa não concluída nunca desconta saldo") em [`docs/PROPOSITO.md`](docs/PROPOSITO.md).

Na prática, hoje isso é um app de rotina com histórico diário, tarefas dinâmicas, Pacus Points e um bichinho virtual (o PACUS) que cresce independente do desempenho.

## Arquitetura

Flutter Web -> ASP.NET Core 10 / C# -> MongoDB Atlas

O frontend oficial do PACUS é `flutter_app/`; o frontend HTML/CSS/JS legado foi aposentado após a auditoria final de migração.

## Regras centrais
- Cada dia começa às 00:00 no timezone do usuário.
- O histórico dos dias encerrados é preservado.
- As tarefas do dia são independentes da configuração permanente.
- Existem três tipos: `mandatory`, `expected`, `challenge`.
- Cada tarefa vale de 1 a 10 Pacus Points, ou de -1 a -10 como penalidade (zero não é permitido).
- Tarefa concluída ganha pontos; não concluída ganha zero e não perde saldo.
- 1 Pacus Point equivale a R$ 0,06 (configurável por família em `Settings.PointToBrlRate`).
- O PACUS cresce uma vez por dia encerrado, independentemente da conclusão das tarefas.
- A criança pode alterar somente as tarefas do dia atual, conforme permissões.
- O adulto administra regras permanentes, configurações e histórico autorizado.

## Loja de Pacus Points
O adulto cadastra itens (brinquedo, atividade, tempo de tela, outro); a criança solicita o resgate e o adulto aprova ou rejeita. Ao aprovar, o saldo é debitado (nunca na solicitação). Cada item pode opcionalmente ter:
- **Limite diário** (`dailyLimit`) — quantas vezes por dia operacional este item pode ser resgatado (pedidos rejeitados não contam para o limite).
- **Tempo de tela concedido** (`screenTimeMinutes`) — ao aprovar, soma automaticamente esses minutos no game timer do dia (mesmo mecanismo dos botões +5/-5 min do adulto).

Toda família nova já recebe o item padrão **"1 hora de tela" = 100 Pacus Points, 1 resgate por dia, +60min no game timer ao aprovar**.

## Desenvolvimento local

### API
1. Configure `MONGODB_URI`, `MONGODB_DATABASE` e `JWT_SECRET`.
2. Execute `dotnet restore backend/Pacus.sln`.
3. Execute `dotnet run --project backend/src/Pacus.Api`.
4. Health: `GET /api/v1/health`.

### Frontend Flutter
1. Entre em `flutter_app/`.
2. Execute `flutter pub get`.
3. Desenvolvimento: `flutter run -d chrome --dart-define=PACUS_API_BASE_URL=http://localhost:5000/api/v1` (ajuste a porta da API se necessário).
4. Produção web: `flutter build web --release --dart-define=PACUS_API_BASE_URL=https://pacus-pacus-api.fly.dev/api/v1`.

## CI/CD
`.github/workflows/ci.yml` valida backend e Flutter (`analyze`, `test` e `build web`).
`.github/workflows/pages.yml` compila e publica o Flutter Web no GitHub Pages com o domínio `www.pacus.com.br`.
`.github/workflows/production-health.yml` monitora a produção a cada 30 minutos (frontend, API, MongoDB e CORS).

## Deploy da API
`backend/Dockerfile` gera uma imagem ASP.NET Core 10. `deploy/docker-compose.yml` documenta a execução com MongoDB Atlas externo. Hospedada em produção no Fly.io (`pacus-pacus-api.fly.dev`, região `iad`); migrada do Render em 2026-09-09 — ver `docs/ESTADO_ATUAL.md` para o histórico da migração.

## Segurança
Nunca commitar senha do MongoDB, JWT secret ou connection strings reais. Checklist completo de segurança/LGPD em `docs/SECURITY_LGPD_CHECKLIST.md`.

## Estado atual do projeto
Ver `docs/ESTADO_ATUAL.md` para o retrato atual e `docs/FLUTTER_MIGRATION_AUDIT.md` para a auditoria final da migração.


## Comunicação V2

A primeira etapa da V2 está implementada: o Chat mantém estado de leitura por usuário, expõe contagem de mensagens não lidas e mostra badge na navegação Flutter. A arquitetura foi preparada para evoluir depois para pedidos rápidos, solicitação de tempo extra, centro de notificações e push/PWA.
