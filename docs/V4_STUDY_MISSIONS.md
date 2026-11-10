# PACUS V4 — Missões de estudo (implementação em revisão)

## Escopo

Recurso **opt-in por tarefa permanente** para as três atividades escolhidas pelo adulto:
leitura do livro (`reading`), lição de casa (`homework`) e caderno de caligrafia
(`handwriting`). Não há detecção automática por título; o adulto seleciona
explicitamente cada modelo de tarefa em Configurações > Tarefas permanentes.

As demais tarefas mantêm o comportamento anterior, inclusive pontuação,
recorrência, horários e histórico.

## Regras

- A configuração `supportKind` do template é `null` por padrão (desativada).
- `supportSteps` possui 2–6 rótulos, de 1–140 caracteres; vazio usa o roteiro padrão do perfil.
- No novo dia, a configuração é copiada para a `DailyTask`. Dia fechado não é reprocessado.
- `completedSupportSteps`, `supportStartedAt`, `supportPostponedUntil`,
  `supportPostponeCount` e `supportHelpCount` pertencem apenas à ocorrência diária.
- Pausar, pedir ajuda, avançar ou desfazer etapas **não concede nem desconta pontos**.
- A conclusão continua pelo endpoint existente; não exige marcar todas as etapas.
- O primeiro registro de iniciativa de cada tarefa diária concede **+1 PP**,
  para todos os níveis: `selfStarted`, `promptedByPacus`, `promptedByAdult`.
  Alterações subsequentes não geram novo bônus; registros anteriores não são alterados.
- Botão "Não quero fazer agora" oferece adiar 10 minutos, pedir ajuda ou começar.
  Adiar não remove a tarefa nem bloqueia sua retomada antecipada.
- O seletor visual de missões muda apenas a sequência de abertura; não reordena
  a rotina, os períodos ou os horários.
- O relatório semanal soma somente ações de apoio nas tarefas com `supportKind` ativo.

## API

`PUT /api/v1/daily-tasks/{id}/support`

Exemplos de payload:
- `{"action":"start"}`
- `{"action":"step","stepIndex":0}`
- `{"action":"undo-step","stepIndex":0}`
- `{"action":"postpone"}`
- `{"action":"help"}`
- `{"action":"resume"}`

O endpoint exige autenticação, verifica a família autenticada via
`GetLatestOpenAsync`, rejeita tarefas excluídas, concluídas ou sem opt-in,
e valida índices de etapa no servidor. Todas as ações emitem um
`TaskEventType.SupportAction`, sem chamar `PointsService`.

`GET /api/v1/autonomy/weekly` inclui novos campos
`postponements`, `helpRequests`, `previousPostponements`,
`previousHelpRequests`.

## A2 — consistência transacional (PR #69)

O PR #68 foi integrado à `main` antes desta correção. O PR #69 altera
exclusivamente a camada de consistência, sem reprocessar pontos históricos.

`MongoTaskLedgerCommitter` utiliza uma transação MongoDB para confirmar,
em uma única operação lógica:

- a alteração de `daily_routines` com comparação de `Version` (inclusive
  documentos antigos sem o campo);
- a transação de pontos em `point_transactions`, quando houver delta;
- o evento correspondente de `task_events`;
- no caso de exclusão permanente, a inativação do `task_template` e
  as tarefas futuras na rotina planejada.

Os fluxos protegidos incluem conclusão, reabertura, ajuste de pontos,
edição de tarefa concluída, exclusão, bônus único de iniciativa (+1 PP)
e ações V4 de suporte. Falha em qualquer escrita aborta a transação.

A API constrói `DailyRoutineService` com `ITaskLedgerCommitter`
obrigatório; não existe fallback não transacional na DI de produção.
O construtor sem committer permanece apenas para os testes unitários
antigos, que usam repositórios fictícios.

O saldo oficial é a soma do ledger. `balanceAfter` permanece um snapshot
informativo, que pode ser defasado em escritas concorrentes de **outras**
fontes de pontos (loja/água/ajustes manuais) ainda fora deste committer.
Esses fluxos são independentes e precisam de sua própria revisão.

### Testes funcionais isolados

A suíte `Pacus.IntegrationTests` cria um replica set MongoDB em container e,
para cada `PacusApiFactory`, um database aleatório `pacus_api_test_*`.
Ela cria conta de adulto e membro fictícios, obtém tokens JWT e testa
os endpoints HTTP, inclusive as transações reais. Casos incluídos:

1. Missão de leitura com etapas, ajuda, adiamento, iniciativa +1
   e conclusão, com verificação de ledger e auditoria.
2. Duas conclusões simultâneas; apenas um prêmio é lançado.
3. Validador Mongo temporário que rejeita `task_events`; tanto
   estado quanto pontos sofrem rollback e a repetição posterior funciona.
4. Ciclo de conclusão, ajustes, reabertura, nova conclusão e exclusão:
   a soma final dos lançamentos da tarefa retorna a zero.

## Cuidados para liberação

1. As novas configurações V4 são materializadas apenas em dias **novos**;
   não são retroaplicadas a tarefas anteriores, nem alteram histórico.
2. O Mongo de produção precisa suportar transações multi-documento em
   replica set (MongoDB Atlas é compatível). Se não suportar, a operação
   falha de maneira explícita, sem confirmar parte dos dados.
3. A correção A2 do PR #69 deve ser revisada e integrada somente após CI
   verde e validação dos testes HTTP isolados. Testes manuais de interface
   e deploy de produção são etapas separadas.
