# PACUS

PACUS é um sistema de apoio à rotina familiar que ajuda crianças a desenvolver autonomia, responsabilidade e hábitos positivos por meio de pequenas tarefas diárias, acompanhamento dos adultos e reforço positivo — sem transformar a rotina numa experiência de punição. Veja o propósito completo (e o porquê de decisões como "tarefa não concluída nunca desconta saldo") em [`docs/PROPOSITO.md`](docs/PROPOSITO.md).

Na prática, hoje isso é um app de rotina com histórico diário, tarefas dinâmicas, Pacus Points e um bichinho virtual (o PACUS) que cresce independente do desempenho.

## Arquitetura

Frontend Web (HTML/CSS/JS) -> ASP.NET Core 10 / C# -> MongoDB Atlas

Flutter será um cliente futuro da mesma API.

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

### Frontend Flutter Web
O cliente oficial está em `flutter_app/`. Para desenvolvimento local:

```bash
cd flutter_app
flutter pub get
flutter run -d chrome --dart-define=PACUS_API_BASE_URL=http://localhost:5000/api/v1
```

Produção é compilada pelo GitHub Actions a partir de `flutter_app/` e publicada em `www.pacus.com.br`. O frontend HTML/CSS/JS legado foi aposentado após a auditoria final de paridade em 2026-09-23.

## CI/CD
`.github/workflows/ci.yml` testa backend e sintaxe JavaScript.
`.github/workflows/pages.yml` prepara deploy do frontend no GitHub Pages.

## Deploy da API
`backend/Dockerfile` gera uma imagem ASP.NET Core 10. `deploy/docker-compose.yml` documenta a execução com MongoDB Atlas externo. Hospedada em produção no Fly.io (`pacus-pacus-api.fly.dev`, região `iad`); migrada do Render em 2026-09-09 — ver `docs/ESTADO_ATUAL.md` para o histórico da migração.

## Segurança
Nunca commitar senha do MongoDB, JWT secret ou connection strings reais. Checklist completo de segurança/LGPD em `docs/SECURITY_LGPD_CHECKLIST.md`.

## Estado atual do projeto
Ver `docs/ESTADO_ATUAL.md` — verificação regra por regra do que está implementado, estrutura do projeto, status do checklist de segurança/LGPD e diferenças entre as branches `main` e `feature/next-migration`.
