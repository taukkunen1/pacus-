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

## Limitações para liberação

1. As novas configurações são materializadas em dias **novos**, não são
   retroaplicadas a tarefas já criadas no dia atual.
2. A estrutura legada atualiza `daily_routines`, `point_transactions` e
   `task_events` em operações separadas. O endurecimento transacional do
   ledger (A2) deve ocorrer antes do merge para produção; esta V4 não
   introduz nenhuma nova operação de pontos no fluxo de etapas, mas altera
   o bônus de iniciativa existente.
3. Aplicar a alteração somente com testes de CI verdes e validação funcional
   em conta de teste. O PR permanece como *draft* e não deve ser integrado
   automaticamente à branch `main`.
