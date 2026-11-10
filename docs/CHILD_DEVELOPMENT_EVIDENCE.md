# PACUS — revisão de evidências para funcionalidades voltadas a 8–10 anos

Este documento define um critério de **revisão por mudança** para funcionalidades que afetam a experiência de usuários por volta de 9 anos. Não é uma declaração de eficácia clínica nem substitui avaliação profissional ou feedback das famílias.

## Critérios para novos PRs

1. Descrever idade, contexto, objetivo comportamental/educacional e eventuais riscos.
2. Procurar, antes da implementação, evidências recentes e relevantes (primeiro revisões sistemáticas, meta-análises e recomendações profissionais; depois estudos primários). Registrar ano, população estudada e limitações de generalização.
3. Evitar apresentação de hipóteses de UX como fatos científicos. Separar princípio respaldado, adaptação proposta e resultado ainda não demonstrado no PACUS.
4. Apoiar autonomia: permitir escolhas, adequar a dificuldade, dar oportunidade de pedir ajuda/adiar e evitar humilhação, punições ou comparações entre usuários.
5. Evitar coleta desnecessária de relatos íntimos, diagnósticos ou interpretação automática das emoções. Não obrigar exposição de sentimentos aos adultos.
6. Manter tarefas em pequenos passos **de apoio**, sem transformar cada passo em obrigação pontuada; testar ausência de duplicação de pontos.
7. Verificar preservação de dados, consentimento de adulto quando aplicável, acessibilidade e linguagem compreensível.
8. Revisar o resultado após uso real com feedback voluntário, sem presumir que maior tempo no app represente benefício.
9. Reavaliar a base científica quando forem alterados incentivos, notificações, aprendizagem, sono, atividade física, experiências emocionais ou tempo de tela; registrar fontes e data da nova análise.

## Aplicação: missão “Desenhar na lousa — o humor de hoje” (08/10/2026)

**Objetivo:** favorecer o início de uma atividade criativa breve e repetível, mantendo a tarefa existente e sua recompensa (1 PP por conclusão, sem pontos extras por etapa).

**Desenho da funcionalidade:** cinco passos curtos; uma sugestão aberta selecionada pela data local da rotina entre 21 desafios; a pessoa pode ignorar a sugestão e fazer sua própria criação. Não há avaliação estética, interpretação automatizada, necessidade de contar sentimentos, exigir título ou revelar conteúdo emocional.

**Base de evidências:**
- Potters et al. (2023), *Educational Research Review*, revisão sistemática sobre tarefas e desenvolvimento da criatividade no ensino fundamental. DOI: https://doi.org/10.1016/j.edurev.2023.100532 — apoia explorar atividades abertas; a literatura apresenta heterogeneidade das medidas de criatividade.
- Wang et al. (2024), *Learning and Motivation*, revisão sistemática e meta-análise de 36 intervenções baseadas na teoria da autodeterminação (amostra escolar, idades variadas). DOI: https://doi.org/10.1016/j.lmot.2024.102015 — intervenções que apoiam autonomia/competência podem favorecer motivação, mas não avaliam diretamente esta tarefa.
- Feng et al. (2024), *Frontiers in Psychology*, meta-análise com 30 estudos sobre envolvimento parental e criatividade estudantil. DOI: https://doi.org/10.3389/fpsyg.2024.1407279 — apoio à autonomia teve associação positiva pequena (r=0,144), e controle psicológico, associação negativa (r=-0,117); dados correlacionais, não causalidade direta para PACUS.

**O que NÃO foi demonstrado:** não há evidência específica de que exatamente cinco etapas, 21 prompts rotativos, 5–10 minutos de desenho ou 1 PP aumentem criatividade em usuários de 9 anos. Esses parâmetros são decisões de produto, sujeitas a ajustes após feedback.

**Verificação técnica:** manter a tarefa permanente, compatibilidade com modelo anterior de quatro etapas, progresso atual mapeado quando o adulto aplica a atualização, histórico de rotinas fechado imutável, recompensas sem duplicação e datas de desafios determinísticas por dia operacional.

## Checklist de revisão do PR

- [ ] Fontes e limitações revisadas para a faixa etária.
- [ ] Autonomia e alternativa sem exposição emocional respeitadas.
- [ ] Nenhuma penalidade por estilo/qualidade ou recusa de compartilhar.
- [ ] Recompensas, dados históricos e controle do adulto preservados.
- [ ] Testes de regressão e CI aprovados.
- [ ] Pós-publicação: confirmar experiência real e efeitos indesejados.

Última revisão documental: 08/10/2026.
