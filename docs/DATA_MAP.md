# PACUS — Mapa de Dados

Documento de referência para conformidade com a LGPD (Lei 13.709/2018), gerado a partir do código-fonte real do backend em `feature/next-migration` (checklist de segurança e LGPD, item B1). Cobre as 12 collections do MongoDB usadas pela aplicação — as 11 previstas originalmente no checklist mais `audit_logs`, criada no item A5.

Este documento é a base para os itens seguintes do checklist: B2 (exportação de dados), B3 (exclusão de conta), D1 (registro de operações de tratamento) e D2 (RIPD).

## Como ler este documento

Cada collection tem uma tabela de campos e uma seção de contexto com:

- **Finalidade** — por que esse dado existe.
- **Categoria do titular** — adulto (responsável), criança, ou ambos (a coleção mistura dados dos dois papéis).
- **Origem** — informado diretamente pelo titular (ou pelo adulto em nome da criança) vs. gerado automaticamente pelo sistema.
- **Quem acessa** — quais camadas da aplicação leem/escrevem esse dado, e se algum campo nunca sai do backend.
- **Base legal (LGPD)** — o fundamento do art. 7º (dados em geral) ou art. 11º (dados sensíveis, não há nenhum aqui) que autoriza o tratamento. Para dados de criança, o art. 14 exige consentimento específico de um responsável — ver nota geral abaixo.
- **Retenção** — por quanto tempo o dado é mantido.
- **Destino em exclusão** — o que acontece com o dado quando a conta/família é excluída (ver B3).
- **Controles de segurança** — hashing, isolamento por família, etc. já implementados (ou a implementar).

### Nota geral sobre dados de crianças (LGPD art. 14)

O PACUS não coleta dados diretamente de crianças de forma autônoma: a conta da criança é criada pelo adulto responsável através do fluxo de bootstrap, que já pressupõe consentimento do responsável para o uso do produto em nome do filho. Isso cobre a base legal do art. 14, §1º, mas **não substitui um texto de consentimento específico e em destaque** — esse é o item C3 do checklist (`[DEPOIS]`, decisão de produto pendente: exigir e-mail do responsável, tela de consentimento dedicada, etc.).

### Nota geral sobre base legal

Para a quase totalidade dos dados abaixo, a base legal é **execução de contrato / procedimentos preliminares** (LGPD art. 7º, V) — o dado existe porque é estritamente necessário para o app funcionar (autenticar, registrar tarefas, calcular pontos). Os logs de auditoria e segurança (`audit_logs`, rate limiting) usam **legítimo interesse** (art. 7º, IX) para prevenção a fraude e responsabilização. Nenhum dado sensível (art. 5º, II) é coletado hoje — ver C1 para revisão de necessidade de coleta.

---

## 1. `users`

Conta de cada membro da família — um documento por adulto e por criança.

| Campo | Tipo | Descrição |
|---|---|---|
| `_id` | ObjectId | Identificador do usuário. |
| `role` | enum | `Adult` ou `Child`. |
| `name` | string | Nome exibido no app. |
| `email` | string? | Só para adulto — usado no login. |
| `passwordHash` | string? | Só para adulto — PBKDF2-SHA256, 100.000 iterações, salt de 16 bytes por senha (`PasswordHasher.cs`). Nunca a senha em texto puro. |
| `pinHash` | string? | Só para criança — mesmo mecanismo do `passwordHash`, aplicado ao PIN de 4 dígitos. |
| `timezone` | string | Fuso horário da família (hoje fixo em `America/Sao_Paulo` — ver TODO no código para vir de `settings` no futuro). |
| `familyId` | ObjectId | Agrupa os membros da mesma família (adulto e criança(s) compartilham o mesmo `familyId`). |
| `createdAt` / `updatedAt` | DateTime | Timestamps de auditoria básica. |

- **Finalidade:** autenticação e identificação dos membros da família.
- **Categoria do titular:** ambos (um documento por pessoa).
- **Origem:** informado pelo adulto no bootstrap (nome, e-mail, senha do adulto; nome e PIN da criança).
- **Quem acessa:** `AuthController`, `BootstrapController`, `AuthService`, `BootstrapService`, `UserRepository`. `passwordHash`/`pinHash` nunca são serializados de volta para o frontend (não existem em nenhum DTO de resposta).
- **Base legal:** execução de contrato (art. 7º, V) para o adulto; consentimento do responsável (art. 14, §1º) para a criança, exercido implicitamente no fluxo de bootstrap — ver nota geral acima.
- **Retenção:** enquanto a conta estiver ativa.
- **Destino em exclusão:** hard delete do documento (nome, e-mail e hash não têm valor fora do contexto da conta — ver B3).
- **Controles de segurança:** hashing PBKDF2 (nunca texto puro), rate limiting no login (item A1), nunca exposto em resposta de API.

## 2. `pacus`

O bichinho de estimação virtual da família — um documento por família.

| Campo | Tipo | Descrição |
|---|---|---|
| `_id` | ObjectId | Id do PACUS. |
| `familyId` | ObjectId | Família dona do PACUS. |
| `name` / `species` | string | Nome e espécie escolhidos. |
| `birthDate` | DateTime | Data de "nascimento" (criação). |
| `stage` | enum | Estágio de crescimento (`Egg` → `Adult`). |
| `size` | double | Tamanho atual, calculado a partir de `totalClosedDays`. |
| `totalClosedDays` | int | Quantos dias fechados (rotina completa) já contribuíram pro crescimento. |
| `lastGrowthDate` | string? | Última data em que o crescimento foi processado (guarda dupla contra duplicação, junto com o índice único em `pacus_growth`). |
| `stageHistory` | array | Histórico de quando cada estágio foi alcançado. |
| `createdAt` / `updatedAt` | DateTime | Timestamps. |

- **Finalidade:** mecânica de gamificação central do app (recompensa visual pelo cumprimento de tarefas).
- **Categoria do titular:** ambos — pertence à família, não a uma pessoa.
- **Origem:** nome/espécie informados pelo adulto no setup; `stage`/`size`/`totalClosedDays` gerados pelo sistema (`DayClosingService`).
- **Quem acessa:** `PacusController`, `DayClosingService`, `PacusRepository`. Leitura liberada para adulto e criança; escrita administrativa (`UpdateState`) restrita ao adulto.
- **Base legal:** execução de contrato (art. 7º, V).
- **Retenção:** enquanto a família estiver ativa.
- **Destino em exclusão:** hard delete.
- **Controles de segurança:** isolamento por `FamilyId` (leitura sempre via `GetByFamilyIdAsync`), `UpdateState` restrito a adulto (`[RequireRole(UserRole.Adult)]`), coberto por testes de isolamento e troca de papel (A2/A3).

## 3. `daily_routines`

A "fotografia" de cada dia — tarefas do dia, status, cronômetro de jogo. Congela quando fechada.

| Campo | Tipo | Descrição |
|---|---|---|
| `_id` | ObjectId | Id da rotina. |
| `userId` *(campo no Mongo; propriedade C# `FamilyId`)* | ObjectId | Família dona da rotina — nome do campo no banco preservado por compatibilidade (ver A4). |
| `date` | string | Data operacional (`YYYY-MM-DD`, no timezone da família). |
| `timezone` | string | Timezone usado para calcular `date`. |
| `status` | enum | `Open` ou `Closed`. |
| `tasks` | array de `DailyTask` | Cópia imutável das tarefas do dia (título, descrição, tipo, período, pontos, status, quem completou, origem). |
| `stats` | objeto | Contagem de tarefas concluídas por tipo + taxa de conclusão. |
| `pointsEarned` | int | Soma de pontos ganhos no dia. |
| `closedAt` | DateTime? | Quando a rotina foi fechada. |
| `gameTimerUnlockedAt`, `gameTimerExtraMinutes`, `gameTimerPausedAt`, `gameTimerPausedMs` | — | Estado do cronômetro de tempo de jogo do dia. |
| `createdAt` | DateTime | Timestamp. |

- **Finalidade:** núcleo funcional do app — rastrear o que cada família fez em cada dia.
- **Categoria do titular:** ambos — quem completou cada tarefa fica registrado dentro de `tasks[].createdBy`/`origin`, mas o documento é da família.
- **Origem:** gerado pelo sistema ao abrir o dia; alterado por ações de adulto/criança (completar, criar tarefa ad-hoc, ajustar pontos).
- **Quem acessa:** `DailyRoutinesController`, `DailyTasksController`, `DailyRoutineService`, `DailyRoutineRepository`.
- **Base legal:** execução de contrato (art. 7º, V).
- **Retenção:** indefinida enquanto a conta existe — é o histórico de uso do app, base do `history` endpoint.
- **Destino em exclusão:** hard delete de todos os documentos da família (nenhuma finalidade de retenção pós-conta identificada).
- **Controles de segurança:** todo acesso escopado por `FamilyId` (`GetByUserAndDateAsync`/`GetLatestOpenAsync`/`GetHistoryAsync` sempre filtram por família); manipulação de id de tarefa de outra família testada e bloqueada (A3).

## 4. `task_templates`

Tarefas permanentes/recorrentes configuradas pela família (a partir das quais `daily_routines.tasks` são geradas).

| Campo | Tipo | Descrição |
|---|---|---|
| `_id` | ObjectId | Id do template. |
| `userId` *(Mongo)* / `FamilyId` *(C#)* | ObjectId | Família dona. |
| `title` / `description` | string | Conteúdo da tarefa. |
| `type`, `period`, `points`, `order` | — | Configuração de exibição/pontuação. |
| `active` | bool | Se está em uso. |
| `recurrence` | string | `daily` \| `weekday` \| `weekend` \| `custom`. |
| `createdBy` | ObjectId | Quem criou (adulto ou criança, se permitido). |
| `createdAt` / `updatedAt` / `deletedAt` | DateTime | Timestamps — exclusão é soft delete (`deletedAt`). |

- **Finalidade:** configuração recorrente de tarefas da família.
- **Categoria do titular:** ambos.
- **Origem:** informado pelo adulto (ou criança, se `ChildPermissions.CanCreateTasks`).
- **Quem acessa:** `TasksController`, `TaskTemplateService`, `TaskTemplateRepository`.
- **Base legal:** execução de contrato (art. 7º, V).
- **Retenção:** indefinida (soft-deleted templates continuam no banco, mas somem da listagem ativa).
- **Destino em exclusão:** hard delete de todos os templates da família, incluindo os soft-deletados (não há razão pra reter um template órfão).
- **Controles de segurança:** posse verificada via `FamilyId` em toda operação (`Update`/`Activate`/`Delete`) — o bug original corrigido nesta auditoria (`979cd19`) era aqui; exclusão agora gera entrada em `audit_logs` (A5).

## 5. `point_transactions`

Extrato de pontos — cada ganho, reversão, ajuste ou gasto vira uma transação, nunca um valor "solto".

| Campo | Tipo | Descrição |
|---|---|---|
| `_id` | ObjectId | Id da transação. |
| `userId` *(Mongo)* / `FamilyId` *(C#)* | ObjectId | Família dona. |
| `date` | string | Data operacional. |
| `dailyRoutineId` | ObjectId? | Rotina associada (nulo para transações fora de um dia específico, ex. resgate). |
| `taskId` / `taskTitle` | string | Referência legível à origem da transação. |
| `type` | enum | `Award`, `Reversal`, `Adjustment`, `Redemption`. |
| `points` | int | Delta assinado. |
| `balanceAfter` | int | Saldo após a transação (facilita auditoria/extrato sem recalcular). |
| `reason` | string? | Motivo, obrigatório em ajustes manuais. |
| `actorId` / `actorRole` | ObjectId / enum | Quem realizou a ação. |
| `createdAt` | DateTime | Timestamp. |

- **Finalidade:** extrato auditável do saldo de Pacus Points da família (moeda interna do app).
- **Categoria do titular:** ambos.
- **Origem:** gerado pelo sistema a cada ação (completar tarefa, aprovar resgate, ajuste manual do adulto).
- **Quem acessa:** `PointsController`, `PointsService`, `PointTransactionRepository`.
- **Base legal:** execução de contrato (art. 7º, V).
- **Retenção:** indefinida — é o próprio histórico financeiro (interno) da família.
- **Destino em exclusão:** hard delete junto com o resto da família.
- **Controles de segurança:** isolamento por `FamilyId`; ajuste manual de saldo gera entrada correspondente em `audit_logs` (A5).

## 6. `pacus_growth`

Log dedicado do crescimento do PACUS — auditável independentemente do estado atual do bicho, e garante (via índice único `{userId, date}`) que o crescimento nunca duplica por dia.

| Campo | Tipo | Descrição |
|---|---|---|
| `_id` | ObjectId | Id do log. |
| `userId` | ObjectId | Família (nome do campo **não** renomeado — fora do escopo do item A4; ver nota abaixo). |
| `pacusId` | ObjectId | PACUS ao qual o log pertence. |
| `date` | string | Data do fechamento que gerou o crescimento. |
| `dailyRoutineId` | ObjectId | Rotina que disparou o crescimento. |
| `stageBefore` / `stageAfter` | enum | Transição de estágio. |
| `sizeBefore` / `sizeAfter` | double | Transição de tamanho. |
| `createdAt` | DateTime | Timestamp. |

> Nota: o campo `userId` aqui armazena o `FamilyId`, assim como nas outras collections, mas **não foi renomeado no item A4** (o checklist original não listava esta entidade). Fica anotado aqui para uma futura limpeza — o risco de confusão é o mesmo descrito no A4, só que ainda não corrigido.

- **Finalidade:** trilha de auditoria interna do crescimento do PACUS (debug/suporte, não exposta como feature ao usuário hoje).
- **Categoria do titular:** ambos.
- **Origem:** gerado pelo sistema (`DayClosingService.GrowPacusOnceAsync`).
- **Quem acessa:** só `DayClosingService`/`PacusGrowthRepository` — não há endpoint de API que devolva isso ao frontend.
- **Base legal:** execução de contrato (art. 7º, V) — suporte à integridade da mecânica de crescimento.
- **Retenção:** indefinida.
- **Destino em exclusão:** hard delete junto com a família.
- **Controles de segurança:** nenhum endpoint expõe esta collection; acesso só server-side.

## 7. `task_events`

Log de eventos das tarefas do dia (criada, concluída, reaberta, editada, excluída, pontos ajustados, reordenada) — o mecanismo de auditoria que já existia antes do A5, específico para `daily_routines.tasks`.

| Campo | Tipo | Descrição |
|---|---|---|
| `_id` | ObjectId | Id do evento. |
| `userId` | ObjectId | Família (mesma observação do `pacus_growth` — não renomeado no A4). |
| `dailyRoutineId` | ObjectId? | Rotina relacionada. |
| `taskId` | string? | Id da tarefa dentro da rotina. |
| `taskTemplateId` | ObjectId? | Template de origem, se houver. |
| `eventType` | enum | `Created`, `Updated`, `Deleted`, `Completed`, `Reopened`, `Reordered`, `PointsProposed`, `PointsAdjusted`. |
| `payload` | BsonDocument? | Estado antes/depois (`{ before, after }`), quando aplicável. |
| `actorId` / `actorRole` | ObjectId / enum | Quem fez a ação. |
| `createdAt` | DateTime | Timestamp. |

- **Finalidade:** trilha de auditoria das tarefas do dia — histórico de quem fez o quê, quando.
- **Categoria do titular:** ambos.
- **Origem:** gerado pelo sistema a cada ação em `DailyRoutineService`.
- **Quem acessa:** só server-side (`DailyRoutineService`/`TaskEventRepository`) — sem endpoint de leitura hoje.
- **Base legal:** legítimo interesse (art. 7º, IX) — responsabilização e resolução de disputas dentro da família (ex. "quem marcou essa tarefa como feita?").
- **Retenção:** indefinida.
- **Destino em exclusão:** hard delete junto com a família.
- **Controles de segurança:** nenhum endpoint expõe esta collection; acesso só server-side.

## 8. `habitats`

Configuração visual do aquário/habitat do PACUS.

| Campo | Tipo | Descrição |
|---|---|---|
| `_id` | ObjectId | Id do habitat. |
| `familyId` | ObjectId | Família dona. |
| `elements` | objeto | Água, plantas, pedras, esconderijos, bolhas (customização visual). |
| `bounds` | objeto | Largura/altura da área do habitat. |
| `theme` | string? | Tema visual escolhido. |
| `createdAt` / `updatedAt` | DateTime | Timestamps. |

- **Finalidade:** personalização visual (feature de engajamento, sem dado sensível).
- **Categoria do titular:** ambos.
- **Origem:** informado pelo adulto (`[RequireRole(UserRole.Adult)]` no `Update`); leitura liberada para os dois papéis.
- **Quem acessa:** `HabitatController`, `HabitatRepository`.
- **Base legal:** execução de contrato (art. 7º, V).
- **Retenção:** indefinida enquanto a conta existe.
- **Destino em exclusão:** hard delete.
- **Controles de segurança:** isolamento por `FamilyId`; escrita restrita a adulto (testado em A2/A3).

## 9. `settings`

Configurações administrativas da família.

| Campo | Tipo | Descrição |
|---|---|---|
| `_id` | ObjectId | Id do documento. |
| `userId` *(Mongo)* / `FamilyId` *(C#)* | ObjectId | Família dona. |
| `pointToBrlRate` | double | Taxa de conversão de Pacus Points para R$ (referência interna, não transação financeira real). |
| `growthStages` | array | Configuração manual de estágios de crescimento (migração de progresso anterior). |
| `childPermissions` | objeto | O que a criança pode fazer sozinha (`CanCreateTasks`, `CanEditTasks`, `CanDeleteTasks`, `CanReorderTasks`, `CanSetPoints`). |
| `gameTimerEnabled` / `gameTimerMinutes` | bool / int | Configuração do cronômetro de tempo de jogo. |
| `createdAt` / `updatedAt` | DateTime | Timestamps. |

- **Finalidade:** parâmetros administrativos configuráveis pelo adulto responsável.
- **Categoria do titular:** a família (configuração), mas afeta diretamente o que a criança pode fazer.
- **Origem:** informado pelo adulto.
- **Quem acessa:** `SettingsController`, `SettingsRepository`, e lido internamente por `DailyRoutineService`/`DayClosingService`.
- **Base legal:** execução de contrato (art. 7º, V).
- **Retenção:** indefinida enquanto a conta existe.
- **Destino em exclusão:** hard delete.
- **Controles de segurança:** isolamento por `FamilyId`; escrita restrita a adulto.

## 10. `store_items`

Itens da loja de recompensas configurados pela família.

| Campo | Tipo | Descrição |
|---|---|---|
| `_id` | ObjectId | Id do item. |
| `userId` *(Mongo)* / `FamilyId` *(C#)* | ObjectId | Família dona. |
| `title` / `description` | string | Conteúdo do item. |
| `cost` | int | Custo em Pacus Points. |
| `category` | string | `screen_time` \| `toy` \| `activity` \| `other`. |
| `icon` | string? | Ícone exibido. |
| `active` | bool | Se está disponível. |
| `stock` | int? | Estoque (`null` = ilimitado). |
| `createdBy` | ObjectId | Quem criou (sempre adulto — `[RequireRole(UserRole.Adult)]`). |
| `createdAt` / `updatedAt` | DateTime | Timestamps. |

- **Finalidade:** catálogo de recompensas que a criança pode resgatar com pontos.
- **Categoria do titular:** a família (configuração pelo adulto).
- **Origem:** informado pelo adulto.
- **Quem acessa:** `StoreController`, `StoreService`, `StoreRepository`.
- **Base legal:** execução de contrato (art. 7º, V).
- **Retenção:** indefinida enquanto a conta existe.
- **Destino em exclusão:** hard delete.
- **Controles de segurança:** isolamento por `FamilyId`; criação restrita a adulto.

## 11. `redemptions`

Pedidos de resgate de itens da loja feitos pela criança e revisados pelo adulto.

| Campo | Tipo | Descrição |
|---|---|---|
| `_id` | ObjectId | Id do resgate. |
| `userId` *(Mongo)* / `FamilyId` *(C#)* | ObjectId | Família dona. |
| `storeItemId` | ObjectId | Item resgatado. |
| `itemTitle` / `cost` | string / int | Cópia do item no momento do pedido (não muda se o item for editado depois). |
| `status` | enum | `Pending`, `Approved`, `Rejected`. |
| `requestedBy` | ObjectId | Quem pediu (a criança). |
| `reviewedBy` | ObjectId? | Quem revisou (o adulto). |
| `requestedAt` / `reviewedAt` | DateTime / DateTime? | Timestamps do pedido e da revisão — já funcionam como trilha básica embutida no próprio dado. |
| `pointTransactionId` | ObjectId? | Transação de pontos gerada na aprovação. |

- **Finalidade:** fluxo de pedido/aprovação de recompensas.
- **Categoria do titular:** ambos (pedido da criança, revisão do adulto).
- **Origem:** gerado pela ação da criança (pedido) e do adulto (revisão).
- **Quem acessa:** `StoreController`, `StoreService`, `StoreRepository`.
- **Base legal:** execução de contrato (art. 7º, V).
- **Retenção:** indefinida — é histórico de uso da loja.
- **Destino em exclusão:** hard delete.
- **Controles de segurança:** isolamento por `FamilyId` (`GetOwnedPendingRedemptionAsync`); aprovação/rejeição restrita a adulto e agora também gera entrada em `audit_logs` (A5).

## 12. `audit_logs`

Log de auditoria para ações administrativas sensíveis, criado no item A5 — deliberadamente separado do dado em si.

| Campo | Tipo | Descrição |
|---|---|---|
| `_id` | ObjectId | Id do log. |
| `familyId` | ObjectId | Família à qual a ação pertence. |
| `action` | string | Identificador curto: `task_template.deleted`, `redemption.approved`, `redemption.rejected`, `points.manual_adjustment`. |
| `entityType` / `entityId` | string | Tipo e id da entidade afetada. |
| `details` | string? | Descrição legível da mudança. |
| `actorId` / `actorRole` | ObjectId / enum | Quem realizou a ação. |
| `createdAt` | DateTime | Timestamp. |

- **Finalidade:** responsabilização — permitir reconstruir quem fez o quê em ações administrativas sensíveis, mesmo que o dado original mude ou seja apagado.
- **Categoria do titular:** ambos (a ação pode ter sido feita por adulto ou, em tese, criança, dependendo do `actorRole`).
- **Origem:** gerado pelo sistema, nunca editável via API.
- **Quem acessa:** só server-side hoje (`AuditLogRepository`) — sem endpoint de leitura no frontend ainda (natural candidato a um painel futuro "atividade recente da família").
- **Base legal:** legítimo interesse (art. 7º, IX) — prevenção a fraude/abuso e responsabilização, ponderado como não conflitante com os interesses da criança (o log registra ações administrativas, não comportamento da criança).
- **Retenção:** proposta — reter por período fixo (ex. 12 meses) mesmo após a ação que originou o log deixar de existir (ex. o `TaskTemplate` foi soft-deleted, mas o log da exclusão continua); reavaliar prazo definitivo no D2 (RIPD).
- **Destino em exclusão:** **decisão pendente para o B3** — duas opções: (a) hard delete junto com a família (mais simples, mas perde a trilha caso a exclusão em si precise ser auditada); (b) reter por um período fixo pós-exclusão com `familyId`/`actorId` anonimizados (pseudonimização), preservando só o fato de que a ação ocorreu. Recomendação: opção (b) para ações que envolvem valores financeiros internos (ajuste de pontos), opção (a) para o restante — a decidir em B3.
- **Controles de segurança:** nunca alterado pelo fluxo normal da aplicação (só `CreateAsync`, sem update/delete no repositório).

---

## Resumo — retenção e exclusão por collection

| Collection | Retenção | Destino em exclusão de conta |
|---|---|---|
| `users` | Enquanto a conta existir | Hard delete |
| `pacus` | Enquanto a família existir | Hard delete |
| `daily_routines` | Indefinida (histórico de uso) | Hard delete |
| `task_templates` | Indefinida (soft-deleted incluído) | Hard delete |
| `point_transactions` | Indefinida (extrato) | Hard delete |
| `pacus_growth` | Indefinida | Hard delete |
| `task_events` | Indefinida | Hard delete |
| `habitats` | Enquanto a conta existir | Hard delete |
| `settings` | Enquanto a conta existir | Hard delete |
| `store_items` | Enquanto a conta existir | Hard delete |
| `redemptions` | Indefinida (histórico) | Hard delete |
| `audit_logs` | Proposta: 12 meses após a ação, mesmo pós-exclusão | A decidir no B3 (ver seção 12) |

Esta tabela é o ponto de partida direto para o **B3** (endpoint de exclusão de conta): a estratégia é, para 11 das 12 collections, excluir todos os documentos com o `FamilyId` da conta encerrada; `audit_logs` precisa de uma decisão de produto sobre reter (anonimizado) ou excluir junto.

## Achados e recomendações desta auditoria (para revisão)

1. **`pacus_growth.userId` e `task_events.userId` não foram renomeados no A4** — guardam `FamilyId`, mas o nome do campo ainda é `UserId`, o mesmo risco de confusão que motivou o A4 nas outras 6 entidades. Recomendação: estender o A4 a estas duas entidades numa próxima passada (não estava no escopo original do checklist).
2. **`pacus_growth` e `task_events` não têm endpoint de leitura na UI normal do app** — são logs internos. O endpoint de exportação (`GET /api/v1/export`, item B2) já inclui os dois, então o adulto consegue ver esses dados via exportação mesmo sem uma tela dedicada no app.
3. **`redemptions.itemTitle`/`cost` são cópias congeladas no momento do pedido** — bom para consistência histórica, mas significa que a exportação (B2) precisa considerar que o item pode não existir mais (`storeItemId` órfão) sem que isso seja um erro.
4. **Nenhum dado sensível (LGPD art. 5º, II) é coletado hoje** — nenhuma collection tem saúde, biometria, dado racial, religioso, etc. O C1 (revisão de necessidade de coleta em `users`) deve confirmar isso e propor remoção de qualquer campo desnecessário encontrado.
