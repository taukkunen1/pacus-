# PACUS — Estado atual do projeto

Verificado direto no código-fonte de `feature/next-migration` em 2026-09-01. Cada afirmação abaixo foi conferida lendo o arquivo citado, não apenas assumida a partir do README ou de commits antigos.

## Regras centrais do README — o que está implementado de verdade

| Regra do README | Status | Onde está no código |
|---|---|---|
| Dia começa às 00:00 no timezone do usuário | ✅ Implementado | `TimezoneHelper.GetOperationalDate` (`Pacus.Application/Utils`), usado em `DailyRoutineService`, `DayClosingService`, `PointsController`, `StoreService` |
| Histórico de dias encerrados é preservado | ✅ Implementado | `DayClosingService` marca `routine.Status = Closed` e grava `ClosedAt`; rotina não é apagada, só fechada |
| Tarefas do dia independentes da configuração permanente | ✅ Implementado | `DailyTask` é cópia imutável do `TaskTemplate` no momento da geração (comentário explícito na entidade: "Alterar o TaskTemplate de origem nunca reescreve tarefas já geradas") |
| Três tipos: `mandatory`, `expected`, `challenge` | ✅ Implementado | `enum TaskType` (`Pacus.Domain/Enums/TaskType.cs`) |
| Tarefa vale 1, 2 ou 3 Pacus Points | ✅ Corrigido no README | Validação real em `DailyRoutineService` aceita 1 a 10 (ou -1 a -10 como penalidade), não só 1/2/3 — README corrigido nesta sessão para refletir a faixa real. |
| Concluída ganha pontos; não concluída ganha zero e não perde saldo | ✅ Implementado | `ToggleTaskAsync` em `DailyRoutineService` |
| 1 Pacus Point = R$ 0,06 | ✅ Implementado (e corrigido) | `Settings.PointToBrlRate` (default `0,06` — pedido do dono do produto para subir de `0,05`). **Achado nesta sessão:** `PointsController` tinha `0.05` fixo em dois endpoints, ignorando por completo o campo `Settings.PointToBrlRate` — mudar a taxa nunca refletia no saldo em R$. Corrigido: o controller agora lê `Settings.PointToBrlRate` da família, com fallback em `Settings.DefaultPointToBrlRate`. |
| PACUS cresce uma vez por dia encerrado, independente da conclusão das tarefas | ✅ Implementado | `DayClosingService.GrowPacusOnceAsync`, protegido por `lastGrowthDate` (idempotente), chamado incondicionalmente ao fechar o dia — não olha se as tarefas foram concluídas |
| Criança só altera tarefas do dia atual, conforme permissões | ✅ Implementado | `EnsureChildPermissionAsync` com flags granulares (`CanCreateTasks`, `CanEditTasks`, `CanSetPoints`, `CanReorderTasks`) checadas em cada ação de `DailyRoutineService` |
| Adulto administra regras permanentes, configurações e histórico autorizado | ✅ Implementado | `TasksController` (templates permanentes) tem `[RequireRole(UserRole.Adult)]` na classe inteira |

Ambas as pendências acima ("Ação sugerida" original) já foram corrigidas nesta sessão, no README e no código.

## Estrutura do projeto

- **Backend** — ASP.NET Core 10 (`net10.0`), Clean Architecture em 4 projetos: `Pacus.Domain` (entidades/enums), `Pacus.Application` (services/DTOs/interfaces), `Pacus.Infrastructure` (Mongo/repositórios/auth), `Pacus.Api` (controllers). MongoDB via driver oficial, sem ORM.
- **Frontend** — HTML/CSS/JS vanilla, sem framework nem bundler, organizado em `components/`, `screens/`, `api/`, `state/`, `utils/`. Telas principais: `home.js` (rotina de hoje), `pacus.js` (habitat + tarefas permanentes).
- **Testes** — suíte de integração em xUnit no backend (`*.HttpIntegrationTests.cs`), cobrindo isolamento por família, permissões por papel (adulto/criança) e os fluxos de LGPD (exportação, exclusão de conta).
- **CI** (`.github/workflows/ci.yml`) — dois jobs: `backend` (`dotnet build` + `dotnet test`) e `frontend` (`node --check` em todo `.js` de `frontend/js`, só sintaxe).

## Branches (E1 resolvido em 2026-09-01)

`main` e `feature/next-migration` estavam com históricos desconexos: `main` tinha só 2 commits (o segundo era um `pacus.zip` de 22MB subido pela interface web do GitHub, sem código real), enquanto todo o app vivia só em `feature/next-migration`. Consolidado a pedido explícito do dono do projeto: force-push de `main` para o commit de `feature/next-migration` — as duas branches agora apontam para o mesmo commit, `main` tem o app inteiro, e `ci.yml` (que só dispara em push para `main`) volta a validar de verdade a cada push, sem precisar do truque de trigger temporário. Os 2 commits antigos de `main` (o zip) não foram perdidos — ficaram preservados na tag `main-legacy-zip-backup`. Detalhes no item **E1** do checklist (`docs/SECURITY_LGPD_CHECKLIST.md`).

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

## Loja de Pacus Points

O backend já tinha toda a base pronta (`StoreItem`, `Redemption`, fluxo de aprovação com auditoria, débito de saldo) mas **sem nenhum item cadastrado e sem tela no frontend** — a loja existia só como API, invisível na prática. Construído nesta sessão, a partir do pedido "1 hora de tela = 100 pontos, limite 1x resgate por dia, retira os pontos usados":

- **`StoreItem` ganhou dois campos novos, genéricos** (reutilizáveis por qualquer item, não só o de tela):
  - `dailyLimit` (int?) — limite de resgates por dia operacional. Pedidos `Rejected` não contam para o limite (um "não" do adulto não deveria travar o dia inteiro).
  - `screenTimeMinutes` (int?) — ao aprovar um resgate deste item, credita esses minutos automaticamente no game timer do dia (`DailyRoutineService.AdjustGameTimerAsync`, o mesmo mecanismo dos botões +5/-5 min do adulto). Isso conecta a loja ao sistema de tempo de tela que já existia, em vez de ser só um rótulo cosmético.
- **`StoreService`**: `RequestRedemptionAsync` valida o limite diário antes de criar a solicitação; `ApproveRedemptionAsync` garante que a rotina de hoje existe (`GetOrCreateTodayAsync`) *antes* de debitar qualquer ponto, para nunca aprovar um resgate e falhar ao conceder o tempo de tela depois.
- **Item padrão "1 hora de tela"** (100 pontos, `dailyLimit: 1`, `screenTimeMinutes: 60`) é criado automaticamente em `BootstrapService` para toda família nova. **Famílias já existentes no banco não recebem esse item retroativamente** — esta sessão não tem acesso ao MongoDB de produção; um adulto pode criar o mesmo item manualmente pela tela nova, ou você pode pedir um script de backfill.
- **Frontend novo**: `screens/store.js` (+ `api/store-api.js`), aba "Loja" na navegação inferior. Criança vê os itens e resgata (botão desabilitado se o saldo não alcançar); adulto também cria itens (com os campos novos) e vê a fila "Aguardando aprovação" com Aprovar/Rejeitar. Endpoint novo: `GET /api/v1/store/redemptions/pending`.
- **Testes**: 3 testes novos em `StoreServiceTests.cs` (limite diário bloqueia segunda solicitação no mesmo dia; rejeitado não consome a vaga; aprovar credita os minutos no game timer). Suíte unitária (27 testes) verde localmente. Build Release limpo, 0 erros, 0 warnings.
- **Validado com CI real**: o Docker deste sandbox não tem daemon ativo, então os testes de integração (`Pacus.IntegrationTests`, MongoDB via Testcontainers) não rodam aqui — mas o `push` normal também não os valida, porque `ci.yml` só dispara em push para `main`. Usei o mesmo truque já registrado no checklist ("trigger temporario" / "remover trigger temporario"): adicionei `feature/next-migration` ao gatilho, dei push, esperei o CI rodar de verdade, e removi de novo. Isso pegou um bug real na primeira tentativa: `PointsHttpIntegrationTests.GetBalance_ShouldSumAllPointTransactions` tinha `4.5` fixo no teste (90 pontos × taxa antiga 0,05); com a taxa em 0,06 o valor certo é 5,4 — corrigido, e ainda achei que a comparação de `double` ali usava igualdade exata (frágil por natureza: `90 * 0.06` não bate bit a bit com o literal `5.4` em IEEE754), então troquei para comparação com precisão. Suíte completa (unitária + integração, 85 testes) verde no CI antes de remover o trigger.

## Lacunas de produto fechadas (2026-09-01)

A pedido do dono do produto, depois de um levantamento do que faltava no app:

- **Fuso horário real por família**: `IFamilyTimezoneService`/`FamilyTimezoneService` lê `User.Timezone` do adulto da família (campo já existia desde o bootstrap, mas nunca era lido de volta). Substituídos os `const "America/Sao_Paulo"` em `DailyRoutinesController`, `PointsController` e `StoreService`. Novo `GET/PUT /api/v1/family/timezone` (PUT restrito ao adulto, aplica a todos os membros da família).
- **Editar/desativar item da loja**: `StoreService.UpdateItemAsync`/`SetItemActiveAsync`, endpoints `PUT /api/v1/store/items/{id}` e `PUT /api/v1/store/items/{id}/active`, mais `GET /api/v1/store/items/all` (painel do adulto, inclui itens desativados). Frontend: botões "Editar"/"Desativar" em `screens/store.js`.
- **Troca de PIN da criança**: `PUT /api/v1/family/children/{id}/pin` (adulto, valida 4 dígitos, log de auditoria `child.pin_changed`). Botão "Trocar PIN" no painel do adulto em `screens/pacus.js`.
- **"Esqueci minha senha" do adulto sem e-mail**: `User.RecoveryCodeHash` — código de recuperação gerado no bootstrap (mostrado uma única vez), uso único (rotaciona a cada reset). `POST /api/v1/auth/adult/reset-password` (público, rate-limited igual ao resto de `/auth`). Contas criadas antes deste recurso (`RecoveryCodeHash` nulo) podem gerar um código pela primeira vez logadas, via `POST /api/v1/family/recovery-code`. Tela de login ganhou o link "Esqueci minha senha".
- **Configuração de growth stages + histórico de estágio**: `GET/PUT /api/v1/settings/growth-stages` (PUT restrito ao adulto, valida nome do estágio e data `AAAA-MM-DD`). `screens/pacus.js` mostra o calendário atual, permite adicionar/limpar estágios, e lista `pacus.stageHistory` (já existia no schema, nunca era exibido).
- **Badges in-app**: `renderBottomNav` aceita um mapa `{ navKey: count }`; `screens/home.js` calcula tarefas pendentes de hoje (criança) e resgates aguardando aprovação (adulto) e mostra um numerozinho na aba correspondente. Escopo desta rodada: só a tela "Hoje" (a mais visitada) — as outras telas não calculam badges ainda.
- **Limpeza**: removidos `components/task-editor.js`, `components/task-card.js`, `state/task-state.js` (confirmado sem nenhum import em lugar nenhum). README atualizado: hospedagem da API deixou de estar "em aberto" — já roda no Render.
- **Fora do escopo por decisão do dono do produto**: notificações push/e-mail reais (ficaram só como badge in-app, sem Firebase/provedor de e-mail) e reset de senha por e-mail de verdade (ficou o recovery code, sem provedor de e-mail configurado).
- Suíte completa (36 unitários + integração via Testcontainers) verde no CI real antes de remover o trigger temporário do `ci.yml`. Ainda em `feature/next-migration` — sem merge/deploy pra `main` (instrução permanente do dono do produto).

## Melhorias baseadas em ciência do comportamento (2026-09-01, pós-`docs/PROPOSITO.md`)

Depois de registrar o propósito do produto no código, pedidas explicitamente pelo dono do produto:

- **Elogio de esforço, não de resultado** (Dweck & Mueller 1998): toast contextual ao concluir tarefa, `utils/effort-messages.js` — frases de esforço/processo (não de traço/resultado), escolhidas por prioridade (dia completo > período completo > tipo desafio > pontos altos > genérico).
- **Escolha real dentro de limites do adulto** (Teoria da Autodeterminação): `TaskTemplate.Options`/`DailyTask.Options`+`SelectedOption` — 2 a 4 opções estruturadas que a criança escolhe antes de concluir, em qualquer tipo/período de tarefa (permanente ou só-hoje). `PUT /api/v1/daily-tasks/{id}/option`. Chips clicáveis em `task-list.js`.
- **"Por que isso importa"** (parentalidade autônomo-suportiva — Joussemet, Landry & Koestner 2008): `TaskTemplate.Reason`/`DailyTask.Reason`, campo opcional de texto livre mostrado sempre no card da tarefa (não escondido atrás de um clique), distinto da descrição (que é "como fazer" — o Reason é "por quê"). Cobre qualquer tarefa, permanente ou só-hoje. As 21 tarefas reais da conta de produção (`pacus@gmail.com`) já foram preenchidas via API com motivos redigidos por Claude, a pedido do dono do produto.
- **Reação do adulto ao dia** (relatedness — terceira necessidade da Teoria da Autodeterminação, ainda sem feature dedicada até aqui): `DailyRoutine.Reaction` (`DailyReaction`: `Icon`, `Message` opcional, `CreatedBy`, `CreatedAt`) — um por dia, reagir de novo substitui a reação anterior (granularidade escolhida pelo dono do produto). Restrito ao adulto (`PUT /api/v1/daily-routines/today/reaction`, `[RequireRole(Adult)]` + checagem no service). 4 ícones pré-definidos com frase padrão editável (`heart`/`clap`/`star`/`hug` — mapeamento pro emoji fica só no frontend, `pacus/habitat.js`). Visível "vinculada ao PACUS": um indicador discreto (pulso suave) aparece no tanque quando há reação do dia, e tocar em qualquer parte do tanque revela a mensagem num modal — sem badge fixo o tempo todo. Direção só adulto → criança por enquanto (decisão do dono do produto, pra manter o escopo simples nesta rodada).
- 55 testes unitários passando; suíte de integração validada no CI real (trigger temporário em `ci.yml`, revertido depois).

## Tratamento de erros global na API (2026-09-01, achado #1+#2 da auditoria de engenharia de API)

A pedido do dono do produto, depois de uma auditoria puramente técnica da API (sem viés de produto/psicologia): os controllers repetiam o mesmo bloco `try { ... } catch (InvalidOperationException) { return BadRequest(...); } catch (UnauthorizedAccessException) { return Forbid(); }` em praticamente toda action (22+12 ocorrências), e os services usavam `InvalidOperationException` genérico pra qualquer erro de negócio — inclusive "não encontrado", que semanticamente deveria ser 404, não 400. Corrigido:

- **3 exceções de domínio novas** (`Pacus.Application/Exceptions/AppExceptions.cs`): `ValidationException` (400), `NotFoundException` (404), `ConflictException` (409). `UnauthorizedAccessException` (já existente no .NET) continua sendo o sinal de "sem permissão" (403 por padrão).
- **Middleware global** (`Pacus.Api/Middleware/AppExceptionHandler.cs`, via `IExceptionHandler` do ASP.NET Core): mapeia cada tipo pro status certo automaticamente; qualquer outra exceção não tratada vira 500 com mensagem genérica pro cliente (o detalhe real vai só pro log). Registrado em `Program.cs` (`AddExceptionHandler` + `AddProblemDetails` + `UseExceptionHandler()` logo no início do pipeline).
- **63 chamadas de `throw new InvalidOperationException`** reclassificadas nos 4 services (`StoreService`, `BootstrapService`, `DailyRoutineService`, `TaskTemplateService`) pro tipo certo, pelo sentido de cada mensagem (ex.: "tarefa não encontrada" → `NotFoundException`; "já existe"/"já foi revisado" → `ConflictException`; resto → `ValidationException`).
- **Controllers limpos**: removidos os try/catch redundantes de `DailyTasksController`, `DailyRoutinesController`, `TasksController`, `BootstrapController`, `StoreController` — o middleware já cuida do status HTTP. Única exceção deixada de propósito: `AuthController` mantém seu try/catch, porque lá `UnauthorizedAccessException` precisa virar 401 (credencial errada, não autenticado) e não o 403 padrão do middleware (autenticado mas sem permissão) — comentário no código explica a diferença.
- **Testes ajustados**: suíte unitária inteira migrada pras novas exceções (55 testes verdes) e suíte de integração corrigida onde o status esperado mudou de 400 pra 404 (cenários de "id de outra família" em tarefas, tarefas permanentes e loja/resgates, que agora corretamente batem em "não encontrado").
- Próximos itens da lista priorizada da auditoria (ainda não atacados): DTOs de resposta pra parar de vazar entidades de domínio direto (`DailyTasksController`/`DailyRoutinesController` retornam `DailyRoutine`/`DailyTask` crus), paginação nos endpoints de listagem, concorrência otimista em `DailyRoutineRepository.UpdateAsync` (hoje é `ReplaceOneAsync` sem guarda de versão).
