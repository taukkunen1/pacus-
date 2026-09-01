# PACUS — instruções para Claude neste repositório

## Propósito do produto (leia antes de qualquer decisão)

**Leia `docs/PROPOSITO.md` inteiro antes de propor, priorizar ou implementar qualquer
feature.** Resumo, para não haver desculpa de não ter visto:

> PACUS é um sistema de apoio à rotina familiar que ajuda crianças a desenvolver
> autonomia, responsabilidade e hábitos positivos por meio de pequenas tarefas diárias,
> acompanhamento dos adultos e reforço positivo.

- **Pacus Points** são o mecanismo de recompensa.
- **O Pacus** (o bichinho virtual) é o elemento de vínculo e progresso.
- **A rotina** é a ferramenta.
- **A autonomia da criança** é o objetivo.

O objetivo final nunca é só "fazer a criança cumprir tarefas" ou "ganhar dinheiro" — é
ensinar progressivamente autonomia, responsabilidade e hábitos, sem transformar a rotina
em punição (por isso não concluir uma tarefa nunca desconta saldo, e o PACUS cresce
independente do desempenho do dia). Qualquer feature nova deve ser avaliada por quanto
ela serve esse objetivo, não só por conveniência técnica ou do adulto.

## Outras referências deste repositório

- `docs/ESTADO_ATUAL.md` — o que já está implementado de verdade, verificado direto no
  código (não confiar só no README para isso).
- `docs/SECURITY_LGPD_CHECKLIST.md` — checklist de segurança/LGPD, itens `[AQUI]` vs
  `[DEPOIS]`.
- Convenção de validação de CI sem Docker local: ver seção "CI/CD" do
  `docs/ESTADO_ATUAL.md` (truque do gatilho temporário em `feature/next-migration`).

## Regra permanente de branch

**Nunca fazer merge ou push de `feature/next-migration` para `main` sem autorização
explícita do dono do produto**, mesmo que `main` esteja desatualizada. Trabalhar em
`feature/next-migration` até receber essa autorização.
