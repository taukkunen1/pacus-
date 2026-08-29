# PACUS — Plano de Resposta a Incidentes de Segurança

Documento de referência para conformidade com a LGPD (Lei 13.709/2018, arts. 46-48), item D3 do checklist de segurança e LGPD. Define o processo a seguir caso ocorra um incidente de segurança que afete dados pessoais tratados pelo PACUS — vazamento, acesso não autorizado, perda de dados, ou comprometimento de credenciais.

Este é um plano dimensionado para o estágio atual do produto: um responsável técnico (você), sem uma equipe de segurança dedicada. As etapas abaixo são as ações concretas que uma pessoa consegue executar sozinha, não um processo corporativo com múltiplos times.

## 1. O que conta como incidente

Qualquer evento que exponha, altere ou destrua dados pessoais sem autorização, ou que comprometa a disponibilidade do serviço de forma anormal. Exemplos concretos para o PACUS:

- Vazamento do banco de dados (MongoDB Atlas) — acesso não autorizado às collections.
- Comprometimento do `JWT_SECRET` — permitiria forjar tokens de qualquer usuário.
- Vazamento da connection string do MongoDB ou de outro segredo (ex. via commit acidental no Git — ver item A7).
- Uma falha de isolamento por família (risco 3 do `docs/RIPD.md`) sendo explorada na prática, não só um risco teórico.
- Acesso indevido à infraestrutura de hospedagem (Render, Atlas) por credenciais comprometidas.
- Um bug que exponha dados de uma família para outra, mesmo sem intenção maliciosa de ninguém (ex. um endpoint novo que esqueceu de escopar por `FamilyId`).

## 2. Detecção

Como um incidente pode ser percebido, hoje:

- **Log de auditoria** (item A5) — permite reconstruir ações administrativas sensíveis depois do fato, mas não é um sistema de alerta em tempo real.
- **Relato de usuário** — a família percebe algo estranho na própria conta (dado que não reconhece, saldo alterado sem explicação) e avisa pelo canal de contato.
- **Você mesmo, ao revisar logs de erro da hospedagem** (Render) ou métricas do MongoDB Atlas.

🔲 *Lacuna conhecida: não há monitoramento automatizado (alertas de acesso anômalo, IDS, etc.) — para o volume atual de usuários, isso é proporcional, mas deve ser revisitado se a base de usuários crescer.*

## 3. Investigação

Ao suspeitar de um incidente:

1. **Não mexa em nada que possa apagar evidência.** Antes de corrigir o problema, registre o que foi observado (prints, logs, timestamps).
2. **Confirme o escopo:** que dado foi afetado, quantas famílias, desde quando. Use o log de auditoria (`audit_logs`) e os logs da hospedagem (Render/Atlas) para reconstruir a linha do tempo.
3. **Identifique a causa raiz:** bug de código, credencial vazada, configuração de infraestrutura incorreta, ou ação de terceiro malicioso.
4. **Classifique a severidade** com base no `docs/RIPD.md`: quantos titulares afetados, que categoria de dado (nunca há dado sensível no PACUS hoje, mas dado de criança pesa mais), reversível ou não.

## 4. Contenção

Ações imediatas para estancar o incidente, adaptadas conforme a causa:

- **Credencial comprometida** (`JWT_SECRET`, senha do MongoDB, PAT do GitHub): rotacionar imediatamente. Isso invalida todos os tokens JWT ativos — todos os usuários precisarão logar de novo, o que é aceitável frente ao risco.
- **Bug de isolamento explorável**: colocar a API em modo de manutenção (ou desabilitar o endpoint específico via deploy de emergência) até a correção estar pronta, se o risco de exploração ativa for real.
- **Acesso indevido à infraestrutura**: revogar a credencial usada, revisar quem mais tem acesso (Render, Atlas, GitHub), forçar troca de senha/token nesses serviços.
- **Vazamento de segredo no histórico do Git** (item A7): rotacionar o segredo vazado imediatamente — reescrever o histórico do Git não é suficiente sozinho, porque uma cópia pode já ter sido clonada.

## 5. Correção

- Corrigir a causa raiz identificada na investigação (patch de código, correção de configuração, rotação de credencial).
- Adicionar um teste de regressão que comprove que o cenário específico do incidente não volta a acontecer — mesmo padrão já usado nos itens A2/A3/C2 (teste de isolamento por família para cada controller/cenário).
- Validar a correção em CI antes de considerar o incidente encerrado tecnicamente.

## 6. Avaliação de impacto e obrigação de comunicação (LGPD, art. 48)

Depois de conter e corrigir, avaliar:

- **Quais titulares foram afetados** — usar o log de auditoria e os dados da investigação para identificar as famílias específicas, não só "algumas famílias".
- **Que dado foi exposto** — cruzar com o `docs/DATA_MAP.md` para saber exatamente que campos das collections afetadas.
- **Risco ou dano relevante aos titulares** — a LGPD (art. 48) exige comunicação à ANPD e aos titulares afetados quando o incidente acarretar risco ou dano relevante. Para o PACUS, um vazamento de nome/tarefas de uma família é diferente (mais grave) de, por exemplo, um bug interno que nunca chegou a ser explorado por ninguém.

🔲 *Decisão que precisa de vocês: definir o canal e o texto padrão de comunicação a titulares afetados, caso um incidente exija. O item D4 (canal de contato de privacidade, `[DEPOIS]`) é um pré-requisito prático disso — hoje o único canal é o e-mail pessoal do responsável.*

Se a avaliação concluir que há risco ou dano relevante:

- **Comunicar aos titulares afetados** — o que aconteceu, que dados foram envolvidos, o que já foi feito, e o que a família pode fazer (ex. trocar a senha).
- **Comunicar à ANPD** — em prazo razoável, conforme a gravidade (a LGPD não fixa um prazo exato como o GDPR, mas exige comunicação "em prazo razoável").

## 7. Registro

Manter um registro interno de cada incidente (mesmo os que não geraram obrigação de comunicação externa), com:

- Data de detecção e de contenção.
- Causa raiz.
- Titulares e dados afetados (ou confirmação de que não houve exposição real).
- Ações tomadas.
- Se houve comunicação à ANPD/titulares, e quando.

🔲 *Sugestão: um arquivo simples (ex. `docs/INCIDENTES.md`, não versionado publicamente se contiver detalhe sensível, ou uma planilha privada) é suficiente no estágio atual — não precisa de uma ferramenta dedicada.*

## 8. Revisão pós-incidente

Depois de qualquer incidente real (não simulações):

- O que permitiu o incidente acontecer, e por que as mitigações existentes (`docs/RIPD.md`) não impediram?
- Que item do checklist de segurança (este documento) precisa ser revisitado ou criado?
- Atualizar este plano se o incidente revelar uma etapa faltando.

## 9. Contatos e responsabilidades

Hoje, um único responsável técnico cobre todas as etapas acima:

- **Responsável técnico e de privacidade:** Pedro 🔲 *(nome completo a preencher)* — pedro.hdslima98@gmail.com

Não há uma equipe de segurança dedicada nem um DPO (Encarregado de Dados) formalmente designado. 🔲 *Se o volume de usuários crescer, nomear um Encarregado (LGPD, art. 41) e formalizar esse papel é o próximo passo natural de governança.*
