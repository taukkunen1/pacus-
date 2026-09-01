# PACUS — Estado atual do projeto

Verificado direto no código-fonte de `feature/next-migration` em 2026-09-01. Cada afirmação abaixo foi conferida lendo o arquivo citado, não apenas assumida a partir do README ou de commits antigos.

## Regras centrais do README — o que está implementado de verdade

| Regra do README | Status | Onde está no código |
|---|---|---|
| Dia começa às 00:00 no timezone do usuário | ✅ Implementado | `TimezoneHelper.GetOperationalDate` (`Pacus.Application/Utils`), usado em `DailyRoutineService`, `DayClosingService`, `PointsController`, `StoreService` |
| Histórico de dias encerrados é preservado | ✅ Implementado | `DayClosingService` marca `routine.Status = Closed` e grava `ClosedAt`; rotina não é apagada, só fechada |
| Tarefas do dia independentes da configuração permanente | ✅ Implementado | `DailyTask` é cópia imutável do `TaskTemplate` no momento da geração (comentário explícito na entidade: "Alterar o TaskTemplate de origem nunca reescreve tarefas já geradas") |
| Três tipos: `mandatory`, `expected`, `challenge` | ✅ Implementado | `enum TaskType` (`Pacus.Domain/Enums/TaskType.cs`) |
| Tarefa vale 1, 2 ou 3 Pacus Points | ⚠️ Parcialmente diferente | Validação real em `DailyRoutineService` aceita **1 a 10** (ou -1 a -10 como penalidade), não só 1/2/3 — checklist item A-relacionado já documenta isso como regra atual. README está desatualizado nesse ponto específico. |
| Concluída ganha pontos; não concluída ganha zero e não perde saldo | ✅ Implementado | `ToggleTaskAsync` em `DailyRoutineService` |
| 1 Pacus Point = R$ 0,05 | ✅ Implementado | `Settings.PointToBrlRate = 0.05` (configurável), usado em `PointsController.GetBalance` |
| PACUS cresce uma vez por dia encerrado, independente da conclusão das tarefas | ✅ Implementado | `DayClosingService.GrowPacusOnceAsync`, protegido por `lastGrowthDate` (idempotente), chamado incondicionalmente ao fechar o dia — não olha se as tarefas foram concluídas |
| Criança só altera tarefas do dia atual, conforme permissões | ✅ Implementado | `EnsureChildPermissionAsync` com flags granulares (`CanCreateTasks`, `CanEditTasks`, `CanSetPoints`, `CanReorderTasks`) checadas em cada ação de `DailyRoutineService` |
| Adulto administra regras permanentes, configurações e histórico autorizado | ✅ Implementado | `TasksController` (templates permanentes) tem `[RequireRole(UserRole.Adult)]` na classe inteira |

**Ação sugerida:** atualizar a linha "Cada tarefa vale 1, 2 ou 3 Pacus Points" no `README.md` para refletir a faixa real (1–10, com penalidade de -1 a -10).

## Estrutura do projeto

- **Backend** — ASP.NET Core 10 (`net10.0`), Clean Architecture em 4 projetos: `Pacus.Domain` (entidades/enums), `Pacus.Application` (services/DTOs/interfaces), `Pacus.Infrastructure` (Mongo/repositórios/auth), `Pacus.Api` (controllers). MongoDB via driver oficial, sem ORM.
- **Frontend** — HTML/CSS/JS vanilla, sem framework nem bundler, organizado em `components/`, `screens/`, `api/`, `state/`, `utils/`. Telas principais: `home.js` (rotina de hoje), `pacus.js` (habitat + tarefas permanentes).
- **Testes** — suíte de integração em xUnit no backend (`*.HttpIntegrationTests.cs`), cobrindo isolamento por família, permissões por papel (adulto/criança) e os fluxos de LGPD (exportação, exclusão de conta).
- **CI** (`.github/workflows/ci.yml`) — dois jobs: `backend` (`dotnet build` + `dotnet test`) e `frontend` (`node --check` em todo `.js` de `frontend/js`, só sintaxe).

## Duas branches, dois mundos

- **`main`** — só 2 commits, nenhum código real; o segundo é um `pacus.zip` de 22MB subido pela interface web do GitHub. Sem histórico compartilhado com `feature/next-migration`.
- **`feature/next-migration`** — a aplicação inteira (68+ commits), é onde este documento e o trabalho recente vivem.

Isso já estava identificado e documentado como item **E1** do checklist de segurança (`docs/SECURITY_LGPD_CHECKLIST.md`) — decisão de consolidação adiada a pedido do dono do projeto. Nada muda aqui até você decidir.

## Segurança e LGPD (`docs/SECURITY_LGPD_CHECKLIST.md`)

15 itens concluídos, 8 pendentes (todos marcados `[DEPOIS]`, ou seja, dependem de uma decisão de produto/infra sua, não de código):

- **Concluído:** rate limiting, testes de isolamento por família, correção de um bug real de checagem de posse em `TasksController.Delete`, renomeação `UserId → FamilyId`, log de auditoria, mapa de dados das 12 collections, exportação de dados (LGPD, portabilidade), exclusão de conta (hard delete + anonimização de `audit_logs` com TTL de 12 meses), rascunhos de Política de Privacidade/Termos de Uso, RIPD e Plano de Resposta a Incidentes.
- **Pendente (decisão sua):** confirmar hardening de infra no Render/Atlas (A6), checar histórico do Git por segredo vazado (A7), **rotacionar o PAT do GitHub por um fine-grained token limitado a este repositório** (A8), publicar Política/Termos e implementar tela de consentimento (B5/B6), decisão sobre fluxo de cadastro infantil (C3), canal de contato de privacidade (D4), consolidação `main`/`feature` (E1).

O item **A8** é particularmente relevante agora: o checklist já pedia a troca do token clássico por um fine-grained scoped a este repo — vale fazer isso com o token que você colou nesta conversa.

## Editor de tarefas — botão de editar + descrição

Investigado a pedido seu. Resultado:

- **Tarefas permanentes** (tela "PACUS" → "Tarefas permanentes", `frontend/js/screens/pacus.js`): editar já funcionava e **já pedia a descrição** — nada a fazer aqui.
- **Tarefas do dia** (tela "Hoje", `frontend/js/screens/home.js`): o botão de editar (✎) já existia e já funcionava, mas **não deixava editar a descrição** — o valor antigo era só repassado sem chance de alteração, e a descrição nunca aparecia no card. Corrigido nesta sessão:
  - `home.js`: novo helper `promptForDescription`, usado tanto ao criar quanto ao editar uma tarefa do dia.
  - `components/task-list.js`: o card da tarefa agora exibe a descrição (quando houver) abaixo do título.
  - `css/components/tasks.css`: estilo para `.task-description` no card.
  - Backend não precisou de nenhuma mudança — `DailyTask`, `DailyTaskUpdateRequest` e o endpoint `PUT /api/v1/daily-tasks/{id}` já suportavam descrição de ponta a ponta.

Três arquivos de frontend continuam como stub sem uso real em lugar nenhum do código (`components/task-editor.js`, `components/task-card.js`, `state/task-state.js`) — sobras do esqueleto inicial do projeto, não referenciadas por nenhum import. Seguro remover quando quiser limpar, sem afetar nada.
