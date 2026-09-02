# PACUS — Propósito

Definido pelo dono do produto em 2026-09-01. Isto é a referência para toda decisão de
produto e de código deste projeto — nenhuma feature, prioridade ou corte de escopo deve
contradizer o que está escrito aqui. Em caso de dúvida sobre se algo "faz sentido pro
PACUS", a resposta está neste documento antes de estar no código.

## A proposta, em uma frase

PACUS é um sistema de apoio à rotina familiar que ajuda crianças a desenvolver
autonomia, responsabilidade e hábitos positivos por meio de pequenas tarefas diárias,
acompanhamento dos adultos e reforço positivo.

## Como isso funciona na prática

A criança participa ativamente da própria rotina: visualiza o que precisa fazer,
acompanha seu progresso e recebe reconhecimento pelo esforço e pela consistência. O
adulto deixa de depender apenas de cobrança verbal e passa a ter uma ferramenta para
estruturar expectativas, acompanhar a evolução e reforçar comportamentos desejados.

O Pacus, o bichinho virtual, representa visualmente esse processo de crescimento: ele
evolui conforme os dias passam, criando uma experiência contínua de desenvolvimento e
vínculo com a rotina — **sem transformar o cumprimento das tarefas em uma experiência de
punição**. É por isso que o PACUS cresce independentemente do desempenho do dia, e por
isso que uma tarefa não concluída rende zero pontos mas nunca desconta saldo.

O objetivo final não é simplesmente fazer a criança "cumprir tarefas" ou "ganhar
dinheiro", mas ensinar progressivamente a criança a cuidar das próprias
responsabilidades, compreender consequências, construir hábitos e conquistar autonomia
dentro da família.

## A distinção entre as peças

- **Pacus Points** são o mecanismo de recompensa.
- **O Pacus** (o bichinho) é o elemento de vínculo e progresso.
- **A rotina** é a ferramenta.
- **A autonomia da criança** é o objetivo.

Nenhuma dessas quatro peças substitui as outras. Uma feature que otimiza só os Pacus
Points (ex.: mais formas de gastar/ganhar pontos) sem servir a autonomia da criança está
otimizando o mecanismo, não o objetivo — vale questionar sempre que uma decisão de
produto parecer ir nessa direção.

## Como isso deve guiar decisões técnicas

- Nunca introduzir um fluxo que penalize a criança além de "não ganhar pontos" (sem
  desconto de saldo, sem mensagens de cobrança agressivas, sem bloqueio do PACUS por mau
  desempenho).
- Priorizar features que aumentam a participação ativa e a visibilidade do progresso da
  criança sobre features que só facilitam a fiscalização do adulto.
- Tratar a recorrência flexível de tarefas (custom days, rotação semanal) como algo
  central, não cosmético — é o que permite a rotina se adaptar à vida real da família em
  vez de virar uma lista genérica que ninguém segue.
- Ao avaliar o "o que falta" do produto, pesar cada lacuna por quanto ela serve a
  autonomia da criança e o vínculo com a rotina, não só por conveniência técnica ou do
  adulto.
- **Nunca implementar mecânica de "sequência de dias" com perda visível ao quebrar**
  (ex.: contador de streak que reseta e é mostrado de volta a zero). A pesquisa sobre
  streaks em apps (mesmo mecanismo do Snapchat) liga esse padrão a uso compulsivo e
  ansiedade em adolescentes — contradiz diretamente o "sem punição" deste documento,
  mesmo sendo tecnicamente só "ausência de recompensa". Se algum dia fizer sentido
  reconhecer consistência ao longo do tempo, fazer isso sem contador que possa "quebrar"
  (ex.: um total acumulado que só cresce, nunca zera).
- Elogio/reforço no app (toasts, mensagens do PACUS) deve ser de **esforço e estratégia**
  ("você conseguiu", "você tentou de um jeito novo"), nunca de traço/resultado ("você é
  o melhor", "nota 10") — pesquisa de Dweck & Mueller (1998) sobre mindset de
  crescimento. Ver `frontend/js/utils/effort-messages.js`.
- Ao pontuar uma tarefa nova, cuidado com o **efeito de supergratificação**
  (overjustification — Lepper, Greene & Nisbett 1973): recompensar demais uma atividade
  que a criança já faria por prazer próprio (ex.: uma tarefa criativa) pode reduzir o
  interesse genuíno nela. Na conta real de hoje isso já está calibrado na direção certa
  sem ter sido pensado assim — tarefas repetitivas/de manutenção (escovar dente, beber
  água) valem pouco (1 PP) e tarefas que já são recompensadoras por si (Momento
  Criativo, desafios) valem mais (3-4 PP). Manter esse padrão ao criar tarefas novas:
  pontuação alta não é prêmio por "tarefa importante", é compensação por esforço que a
  criança não faria sozinha de bom grado.
- A **terceira necessidade da Teoria da Autodeterminação** (relatedness — vínculo com
  outra pessoa, não só autonomia e competência) tem uma feature dedicada desde
  2026-09-01: a reação pessoal do adulto sobre o dia (`DailyRoutine.Reaction`, ver
  `pacus/habitat.js`). É vínculo, não recompensa — por isso não gera Pacus Points e não
  é obrigatória. Hoje só numa direção (adulto → criança); abrir para os dois lados é
  uma extensão natural, não decidida ainda.
