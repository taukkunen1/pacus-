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
