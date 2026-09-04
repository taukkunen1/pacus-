# PACUS — Estado atual do projeto

Verificado diretamente no código-fonte. A auditoria original foi feita em `feature/next-migration` em 2026-09-01; em 2026-09-04 essa branch está integralmente contida em `main`, que é a branch atual do projeto. Cada afirmação abaixo foi conferida lendo o arquivo citado, não apenas assumida a partir do README ou de commits antigos.

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

## Branches (estado verificado em 2026-09-04)

Em 2026-09-01, `main` e `feature/next-migration` tinham históricos desconexos: `main` tinha apenas dois commits, incluindo um `pacus.zip` de 22 MB sem código-fonte útil, enquanto o app vivia em `feature/next-migration`. O histórico antigo foi preservado na tag `main-legacy-zip-backup`, e `main` foi então alinhada à feature.

Desde então, o desenvolvimento foi integrado em `main`. No estado atual, `feature/next-migration` aponta para `c23cc10` e é ancestral de `main` (`4c368b7`); ela não possui commits que não estejam em `main`. Portanto, `main` é a fonte de verdade para desenvolvimento, CI e deploy. A branch `feature/next-migration` permanece apenas como referência histórica e pode ser arquivada ou removida numa manutenção futura, se não houver necessidade de mantê-la.

## Segurança e LGPD (`docs/SECURITY_LGPD_CHECKLIST.md`)

17 itens concluídos, 5 pendentes (todos marcados `[DEPOIS]`, ou seja, dependem de uma decisão de produto/infra sua, não de código):

- **Concluído:** rate limiting, testes de isolamento por família, correção de um bug real de checagem de posse em `TasksController.Delete`, renomeação `UserId → FamilyId`, log de auditoria, mapa de dados das 12 collections, exportação de dados (LGPD, portabilidade), exclusão de conta (hard delete + anonimização de `audit_logs` com TTL de 12 meses), rascunhos de Política de Privacidade/Termos de Uso, RIPD e Plano de Resposta a Incidentes.
- **Pendente (decisão sua):** confirmar hardening de infra no Render/Atlas (A6), checar histórico do Git por segredo vazado (A7), **rotacionar o PAT do GitHub por um fine-grained token limitado a este repositório** (A8), publicar Política/Termos no site (B5) e definir o canal de contato de privacidade (D4).

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
- Suíte completa (36 unitários + integração via Testcontainers) verde no CI real. Esse trabalho foi posteriormente integrado em `main`.

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

## DTOs de resposta pra DailyRoutine/DailyTask (2026-09-01, achado #3 da auditoria de engenharia de API)

`DailyTasksController`, `DailyRoutinesController` e `HistoryController` devolviam a entidade de domínio `DailyRoutine`/`DailyTask` crua (`Ok(routine)`) — qualquer campo interno adicionado à entidade (os dois campos `[BsonIgnore]` calculados, por exemplo, ou um campo de auditoria futuro) vazava pra API sem ninguém decidir isso de propósito. Corrigido:

- **`DailyRoutineResponse`/`DailyTaskResponse`/`DailyRoutineStatsResponse`/`DailyReactionResponse`** (`Pacus.Application/DTOs/DailyRoutineDto.cs`, substituindo o `DailyRoutineDto`/`TaskDto` antigos que já existiam no código mas nunca eram usados por nenhum controller): shape explícito, com extension methods `ToResponse()` fazendo a única conversão de entidade pra DTO.
- **Shape mantido idêntico** ao que a serialização direta da entidade já produzia (mesmos nomes de campo, mesmos valores, incluindo `origin` e `deletedAt` — tarefas deletadas continuam na lista de `tasks` com `deletedAt` preenchido, o frontend que já filtra `!task.deletedAt`), pra não quebrar frontend nem os testes de integração existentes. Único cuidado técnico: `Type`/`Period`/`Status` dos DTOs continuam tipados como os enums do domínio (não `string`), porque o conversor global de enum→camelCase (`JsonStringEnumConverter`, configurado em `Program.cs`) só se aplica a propriedades realmente tipadas como enum — converter pra `string` no meio do caminho quebraria "mandatory"/"morning"/"pending" virando "Mandatory"/"Morning"/"Pending".
- 3 controllers atualizados (`GetToday`, `GetByDate`, `UpdateOrder`, `PauseGameTimer`, `ResumeGameTimer`, `AdjustGameTimer`, `SetReaction` em `DailyRoutinesController`; `Create`, `Complete`, `Reopen`, `SelectOption`, `AdjustPoints`, `Update`, `Delete` em `DailyTasksController`; `Get` em `HistoryController`, incluindo o `IEnumerable<DailyRoutine>` do histórico por período).
- 55 testes unitários verdes; build limpo sem warnings novos.

## Paginação em listagens que crescem sem limite (2026-09-01, achado #4 da auditoria de engenharia de API)

Dois endpoints devolviam a lista inteira de uma vez, sem nenhum jeito de pedir só uma página — e ambos crescem pra sempre com o uso normal do app (um dia a mais no histórico, uma transação a mais a cada tarefa/ajuste/resgate). Os outros dois candidatos citados na auditoria original (fila de resgates pendentes, audit log/growth log) foram avaliados e não entraram nesta rodada: a fila de resgates é naturalmente limitada (o adulto revisa e ela esvazia — não é um log que só cresce), e audit log/growth log não têm nenhum endpoint `GET` exposto hoje (só são lidos internamente pela exportação de dados, que precisa mesmo devolver tudo, sem paginar — é portabilidade LGPD, não uma tela).

- **`PagedResult<T>`** (`Pacus.Application/DTOs/PaginationDto.cs`): envelope genérico `{ items, page, pageSize, totalCount, totalPages }`. **`PaginationHelper.Validate`** (`Pacus.Application/Utils/PaginationHelper.cs`) valida `page >= 1` e `1 <= pageSize <= 100` antes de bater no banco, lançando `ValidationException` (400, aproveitando o middleware do achado #1) em vez de deixar um valor absurdo virar uma query gigante no Mongo.
- **`GET /api/v1/history`** (sem `date`, ou seja o histórico por período) e **`GET /api/v1/points/transactions`**: aceitam `page`/`pageSize` (default 1/20), devolvem `PagedResult`. Os repositórios (`DailyRoutineRepository.GetHistoryAsync`, `PointTransactionRepository.GetHistoryAsync`) agora fazem `CountDocumentsAsync` + `Skip`/`Limit` em vez de trazer tudo (o segundo tinha um `limit=100` fixo, sem jeito de ver o resto).
- **`PointTransactionResponse`** (`Pacus.Application/DTOs/PointsDtos.cs`, novo): o extrato de pontos também devolvia a entidade de domínio crua — corrigido no mesmo passo, já que a resposta desse endpoint estava sendo alterada de qualquer forma (mesmo espírito do achado #3, mas fora do escopo original que era só `DailyRoutine`/`DailyTask`).
- **Frontend**: `history-api.js`/`points-api.js` aceitam `page`/`pageSize`; `screens/history.js` e `screens/points.js` carregam a primeira página (20 itens) e ganharam um botão "Carregar mais" que busca a próxima e concatena, em vez de pedir a lista inteira de uma vez como antes.
- **Testes**: suíte unitária ajustada (fakes de repositório implementam a nova assinatura paginada) e suíte de integração corrigida onde a resposta deixou de ser um array solto (`GetHistory_WithDateRange_ShouldReturnFamilyHistory`, `GetTransactions_ShouldReturnOnlyCurrentFamilyTransactions`, `Transactions_ShouldNotLeakBetweenFamilies` — todas passam a ler `body.GetProperty("items")`).
- 55 testes unitários verdes; build limpo sem warnings novos.

## Concorrência otimista em DailyRoutine (2026-09-01, achado #5 da auditoria de engenharia de API)

`DailyRoutineRepository.UpdateAsync` fazia um `ReplaceOneAsync` filtrado só pelo `Id`, sem nenhuma guarda de versão. Toda ação do dia (completar/reabrir tarefa, ajustar pontos, pausar/retomar/ajustar o game timer, reagir ao dia, reordenar) segue o mesmo padrão: busca a rotina, muda um pedaço dela, grava a rotina inteira de volta. Se a criança completa uma tarefa e o adulto ajusta o game timer quase ao mesmo tempo, as duas requisições leem a mesma rotina, cada uma muda seu próprio pedaço, e a segunda gravação sobrescreve a rotina inteira — apagando silenciosamente a mudança da primeira, sem erro nenhum (*lost update* clássico). Numa família de 2-3 pessoas isso é raro, mas quando acontece hoje ninguém percebe.

- **`DailyRoutine.Version`** (novo campo `int`, default `0`): incrementado a cada gravação bem-sucedida. Documentos já existentes no Mongo sem esse campo desserializam como `0` automaticamente — sem migração. Não exposto na API (não entrou em `DailyRoutineResponse`, achado #3) — é um detalhe interno de persistência, não algo que o frontend precise decidir sobre.
- **`DailyRoutineRepository.UpdateAsync`**: o filtro do `ReplaceOneAsync` agora exige `Id == id && Version == versaoLida`. Se `MatchedCount == 0` (alguém já gravou por baixo), lança `ConflictException` (409, via o middleware do achado #1) em vez de sobrescrever silenciosamente.
- **Trade-off consciente, sem retry automático**: quem perde a corrida recebe um 409 e precisa tentar de novo (reabrir a tela/repetir o toque) — não implementei retry automático nos ~12 pontos de `DailyRoutineService` que chamam `UpdateAsync` (isso exigiria reestruturar cada método pra reler+reaplicar a mutação em caso de conflito, um refactor maior e desproporcional a uma corrida rara numa app de família pequena). O importante é que a falha agora é visível e barulhenta em vez de invisível e silenciosa.
- **Testes**: `DailyRoutineConcurrencyTests.cs` (novo) — duas leituras independentes da mesma rotina, a primeira grava com sucesso (versão 0→1), a segunda (baseada na versão 0, que já não existe mais) lança `ConflictException`, e o que fica salvo é o que a primeira gravou, intacto. Pra isso funcionar de verdade, `FakeDailyRoutineRepository` passou a devolver/guardar cópias independentes (`Clone`) em vez da mesma referência em memória — do jeito que o Mongo de verdade já se comporta (cada leitura é um round-trip que desserializa um documento novo), mas que o fake antigo não simulava.
- 58 testes unitários verdes (55 + 3 novos); build limpo sem warnings novos.

## Bug crítico pós-deploy do achado #5 + recorrência "dia sim, dia não" + editor de tarefas do dia num painel só (2026-09-02)

### 409 em rotinas antigas (regressão do achado #5 de concorrência otimista)

Depois do deploy da concorrência otimista (`DailyRoutine.Version`, achado #5, seção acima), qualquer ação numa rotina criada **antes** desse campo existir passou a falhar sempre com `ConflictException` (409) — não era uma corrida rara, era 100% das vezes, porque a query filtro `Version == 0` não bate em documento do Mongo onde o campo simplesmente não existe (Mongo não trata "campo ausente" como igual a `0` numa comparação direta). `Version` desserializa como `0` em C#, mas o filtro do `ReplaceOneAsync` rodava direto no banco, sem passar pela desserialização.

- **`DailyRoutineRepository.UpdateAsync`**: quando a versão esperada é `0`, o filtro agora aceita `Version == 0` OU o campo ausente (`Builders.Filter.Or(Eq(Version, 0), Exists(Version, false))`). Documentos legados sem `Version` voltam a gravar normalmente, sem script de migração nem tocar nos dados existentes.
- **`DailyRoutinesController.GetToday()`**: ganhou uma repetição (retry) de uma tentativa em caso de 409, para absorver o caso raro de corrida real de verdade (dois requests concorrentes de fato) sem devolver erro pro usuário — o trade-off "sem retry automático" registrado no achado #5 continua valendo para os outros ~12 pontos de `DailyRoutineService` que chamam `UpdateAsync`, só `GetToday` ganhou a repetição.
- Frontend (`api-client.js`): uma tentativa extra automática quando a API responde 409, antes de mostrar erro pro usuário.

### Recorrência "dia sim, dia não" (`RecurrenceInterval`)

Pedido pontual: uma tarefa permanente tipo "Lavar o cabelo" que não é nem diária nem por dia da semana fixo, e sim por intervalo de dias corridos.

- **`TaskTemplate`**: novo valor de recorrência `RecurrenceInterval`, com dois campos novos, `AnchorDate` (data-âncora, formato `yyyy-MM-dd`) e `IntervalDays` (inteiro, dias entre ocorrências — `2` = dia sim, dia não).
- **`DailyRoutineService.ResolveTemplateForDay`**: passou a receber a data operacional inteira (não só o dia da semana), porque `RecurrenceInterval` precisa contar dias corridos desde a âncora — diferente de `RecurrenceCustom`/`RecurrenceWeekdayRotation`, que se repetem sempre nos mesmos dias da semana, um intervalo em dias vai deslizando pela semana com o tempo. Novo helper `IsIntervalDay` calcula `(diasCorridosDesdeAAncora % IntervalDays) == 0`.
- **`CreateTaskRequest`/`TaskTemplateService`**: validação e persistência dos dois campos novos quando `Recurrence == "interval"`.
- **Testes**: `TaskRecurrenceTests.cs` cobrindo dia-sim-dia-não a partir de âncoras diferentes, incluindo a virada de semana.

### Editor de tarefas do dia — um painel só, em vez da fila de prompts

Reclamação do dono do produto: o botão de editar tarefa do dia (✎, tela "Hoje") abria uma sequência de `prompt`/`confirm` do navegador, um atrás do outro ("ok > ok > ok > ok"), e nesse meio de caminho não ficava claro que dava pra trocar o tipo da tarefa entre Obrigatória / Deve fazer / Desafio — a opção estava enterrada num desses prompts genéricos, sem destaque.

- **`components/modal.js`**: nova função `promptTaskForm(...)`, um painel único com todos os campos de uma vez — título, descrição, pontos (com a mesma validação de sempre), o tipo da tarefa como um grupo de opções sempre visível (não mais escondido atrás de um prompt numerado), a lista de opções (quando a tarefa é de múltipla escolha, com botão de adicionar/remover, de 2 a 4 opções) e o motivo. Substitui os antigos `promptForType`, `promptForPoints`, `promptForDescription`, `promptForOptions` e `promptForReason` (todos removidos de `home.js`, junto com as constantes que só eles usavam).
- **`css/components/modal.css`**: estilo novo pro painel (`.modal-box--form`, rola por dentro em telas baixas) e pro grupo de tipo (`.task-form-type-option`, visual de "pill" com destaque quando selecionado), checkbox e lista de opções.
- **`screens/home.js`**: os fluxos de criar e editar tarefa do dia (`#add-task` e `[data-task-action=edit]`) agora chamam `promptTaskForm` uma vez só, em vez da cadeia de prompts sequenciais. `promptForReactionChoice` (reação do adulto ao dia, recurso sem relação com isto) não foi tocado.
- Validado localmente (checagem de sintaxe com `node --check` nos arquivos tocados e um teste visual/funcional isolado do painel antes de subir) — sem suíte automatizada de frontend no projeto, então a validação aqui foi manual, como já registrado nas seções anteriores.

### Faxina do repositório

Apagado o arquivo solto `pacusretryfix.patch` (resíduo de um patch aplicado manualmente, sem uso depois de aplicado) e as 4 branches órfãs já mescladas e sem trabalho pendente (`fix/routine-conflict-retry`, `fix/routine-conflict-retry-1`, `taukkunen1-patch-1`, `taukkunen1-patch-2`). Esta observação descreve a limpeza feita naquele momento; o repositório pode ter novas branches de feature depois dela.

## Revisão geral pós-auditoria + lentidão no primeiro acesso (2026-09-01)

A pedido do dono do produto: "revise tudo, veja se tudo está testado e funcionando" + relato de que o site às vezes "parece carregando e demora tanto".

**Revisão geral:** build da solução inteira limpo (0 erros), 58 testes unitários verdes localmente, e os 5 achados da auditoria de API (exceções tipadas, DTOs de resposta, paginação, concorrência otimista) cada um validado individualmente no CI real (unitário + integração via Testcontainers/MongoDB) antes de ser considerado fechado. A referência aos runs de `feature/next-migration` é histórica; a validação contínua atual ocorre em `main`.

**Diagnóstico da lentidão:** não é bug de código — é comportamento normal de infraestrutura em plano gratuito. O frontend (`frontend/`) é hospedado como site estático no GitHub Pages (`.github/workflows/pages.yml`, deploy só em push pra `main`) — isso carrega instantâneo, sempre. Quem hiberna é a **API no Render** (`pacus.onrender.com`): medido diretamente nesta sessão, uma chamada a `/api/v1/health` depois de um tempo sem uso levou **5.6s**, contra **0.3s** nas chamadas seguintes (já "acordada"). Isso é o padrão conhecido do plano gratuito do Render — o serviço dorme depois de um período sem tráfego e o primeiro request seguinte paga o custo de acordar o container (em planos gratuitos isso costuma passar de 30s quando o sono é mais longo que o medido aqui). Não há nada de anormal no `Dockerfile` ou no `Program.cs` (nenhuma chamada bloqueante extra no boot) causando lentidão adicional além do "acordar" em si.

**Correção aplicada (frontend, já que a causa raiz é infraestrutura fora do meu acesso):** antes, `fetch` em `api-client.js` não tinha nenhum timeout nem feedback visual — durante um cold start, a tela só mostrava "Carregando..." parado (tela de login) ou o botão "Entrar" sem nenhuma mudança visível, o que parece travado/quebrado em vez de "o servidor está acordando". Adicionado:

- **`frontend/js/utils/slow-load-hint.js`** (novo): `withSlowLoadHint(promise, onSlow, delayMs=4000)` — dispara `onSlow()` se a promise ainda não resolveu depois de 4s, sem cancelar nem afetar o resultado da chamada em si.
- **`screens/login.js`**: os 3 fluxos de entrada (login adulto, redefinir senha, PIN da criança) agora desabilitam o botão, trocam o texto pra "Entrando.../Ainda conectando..." e mostram um aviso neutro (`SLOW_LOAD_MESSAGE`) depois de 4s de espera, em vez de ficar sem reação nenhuma. Login da criança também ganhou uma guarda contra duplo toque (`submitting`) que não existia antes.
- **`screens/home.js`**: a mensagem inicial "Carregando sua rotina..." vira o mesmo aviso depois de 4s.
- **CSS**: `.hint-text` (novo, `frontend/css/global.css`) — mesmo elemento de `.error-text`, cor neutra (`--text-on-dark-dim`) em vez de vermelho, pra não parecer erro quando é só demora.
- **Fora do escopo desta correção**: não mexi no plano do Render (não tenho acesso ao dashboard) nem adicionei nenhum "ping" pra manter o serviço sempre acordado — isso só empurra o problema, não resolve, e pode não valer a pena vs. simplesmente upgradar o plano se a demora incomodar de verdade. **Se quiser eliminar o cold start de vez, o caminho é olhar o plano do serviço no Render (Settings → Instance Type) e considerar um plano pago "always-on".**
- Sem testes automatizados de frontend no projeto (só checagem de sintaxe via `node --check`, que já roda no CI em `frontend` job) — validado manualmente lendo o fluxo e rodando a checagem de sintaxe local nos arquivos tocados.
- Esta melhoria foi posteriormente integrada em `main`; esta nota de "sem merge/deploy" é apenas o estado histórico da implementação.
