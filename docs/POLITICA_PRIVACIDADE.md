# Política de Privacidade do PACUS

**Última atualização: 29 de agosto de 2026**

> ⚠️ **Rascunho — item B4 do checklist de segurança e LGPD.** Este documento foi redigido com base no código-fonte real do PACUS (ver `docs/DATA_MAP.md`) e cobre o que a aplicação efetivamente faz hoje. Ele **não foi revisado por um advogado**. Antes de publicar, revise especialmente as seções marcadas com 🔲 (dados que faltam preencher) e considere validar o texto com um profissional, principalmente as partes sobre dados de crianças (art. 14 da LGPD).

## 1. Quem somos

O PACUS é um aplicativo de rotina para famílias, com histórico diário de tarefas, sistema de pontos ("Pacus Points") e um bichinho virtual ("PACUS") que cresce com o uso do app.

O controlador dos dados pessoais tratados pelo PACUS é:

- **Responsável:** Pedro 🔲 *(complete com seu nome completo antes de publicar)*
- **Contato para assuntos de privacidade e exercício de direitos LGPD:** pedro.hdslima98@gmail.com

Esta política se aplica a todos os usuários do PACUS — adultos responsáveis e crianças cujas contas são criadas por eles — e descreve quais dados coletamos, por quê, por quanto tempo guardamos, com quem compartilhamos (ninguém, hoje) e quais direitos você tem sobre eles.

## 2. Como uma conta é criada

O PACUS funciona em núcleos familiares. Um adulto cria a conta da família (via cadastro inicial) e, dentro dela, cria o perfil de uma ou mais crianças. **A criança não se cadastra sozinha** — o perfil dela é criado e controlado pelo adulto responsável, que autentica com e-mail e senha; a criança acessa com um PIN numérico de 4 dígitos definido pelo adulto.

## 3. Quais dados coletamos e por quê

O PACUS coleta apenas o necessário para o app funcionar — não usamos os dados para publicidade, não vendemos dados a terceiros e não fazemos perfilamento comportamental para fins comerciais. O detalhamento completo, coleção por coleção do banco de dados, está em `docs/DATA_MAP.md` (documento técnico, item B1 da nossa auditoria interna); o resumo abaixo é a versão em linguagem simples.

### 3.1 Dados de cadastro e identificação

- **Do adulto:** nome, e-mail, senha (armazenada com hash — nunca em texto puro), fuso horário.
- **Da criança:** nome, PIN de acesso (também armazenado com hash), fuso horário. A criança não informa e-mail.

*Finalidade:* autenticar o acesso e identificar quem fez cada ação no app.
*Base legal:* execução do contrato de uso do serviço (LGPD, art. 7º, V); para o perfil da criança, o consentimento do responsável é dado no próprio ato de criar o perfil (art. 14, §1º).

### 3.2 Dados de uso do app

- Tarefas configuradas pela família (título, tipo, pontos, período do dia) e o histórico diário de quais foram concluídas.
- Saldo e histórico de Pacus Points (cada tarefa concluída gera uma transação de pontos).
- Estado de crescimento do PACUS (o bichinho virtual) e o "habitat" (customizações visuais) da família.
- Itens da lojinha de recompensas cadastrados pelo adulto e os resgates feitos pela criança.
- Configurações da conta (preferências salvas pela família).

*Finalidade:* fazer o app funcionar — é o próprio propósito do PACUS (acompanhar rotina, calcular pontos, mostrar o crescimento do bichinho).
*Base legal:* execução do contrato (art. 7º, V).

### 3.3 Logs de auditoria e segurança

Registramos internamente algumas ações administrativas sensíveis — exclusão permanente de uma tarefa, aprovação/rejeição de um resgate na loja, ajuste manual de saldo de pontos — guardando quem fez, quando e o que mudou, separado do dado em si. Também aplicamos limite de tentativas (rate limiting) nos formulários de login e cadastro, para dificultar tentativas de adivinhação de senha/PIN.

*Finalidade:* responsabilização (saber quem fez o quê) e prevenção a fraude/abuso.
*Base legal:* legítimo interesse (art. 7º, IX), equilibrado com a minimização de dados — ver seção 6 sobre como tratamos esses registros quando você exclui sua conta.

### 3.4 O que **não** coletamos

Não coletamos dados sensíveis na forma do art. 5º, II da LGPD (origem racial ou étnica, convicção religiosa, opinião política, dado de saúde, biometria, orientação sexual, etc.). Não usamos cookies de rastreamento publicitário nem compartilhamos dados com redes de anúncio.

## 4. Com quem compartilhamos seus dados

Hoje, **não compartilhamos dados com nenhum terceiro** para fins comerciais ou de marketing. Os dados ficam armazenados em nossa infraestrutura de hospedagem (banco de dados MongoDB Atlas e servidor de aplicação), que atua estritamente como operador técnico — ou seja, processa os dados por nossa conta, sob nossas instruções, e não os usa para fins próprios.

🔲 *Se no futuro passarmos a usar outros serviços de terceiros (ex. envio de e-mail, análise de erros/crash reporting), esta seção deve ser atualizada para listá-los.*

## 5. Por quanto tempo guardamos seus dados

Os dados de uso (tarefas, histórico, pontos, resgates, crescimento do PACUS) são mantidos enquanto sua conta estiver ativa, para preservar o histórico da família. Os logs de auditoria seguem uma regra própria — veja a seção 6.

## 6. Exclusão de conta

Você pode excluir permanentemente a conta da sua família a qualquer momento, direto no app (área de configurações do adulto), mediante confirmação da sua senha. Ao confirmar a exclusão:

- **Todos os dados de uso da família são apagados de forma definitiva e irreversível** — perfis, tarefas, histórico diário, pontos, estado do PACUS, habitat, configurações, itens de loja e resgates.
- **Os logs de auditoria são uma exceção:** em vez de apagados imediatamente, eles são *anonimizados* — perdem o vínculo com quem praticou a ação, mas o registro da ação em si (o quê, quando) é preservado por até 12 meses, e então apagado automaticamente. Isso existe para que possamos investigar fraude ou abuso ocorrido pouco antes de uma exclusão de conta, sem manter dado pessoal além do necessário.

Não há como desfazer uma exclusão de conta.

## 7. Seus direitos como titular dos dados (LGPD, art. 18)

Você tem direito a:

- **Confirmação e acesso** — saber se tratamos seus dados e quais são.
- **Portabilidade** — baixar uma cópia de todos os dados da sua família em formato legível (JSON), direto no app (área de configurações do adulto) ou pedindo pelo e-mail de contato.
- **Correção** — pedir a correção de dados incompletos, inexatos ou desatualizados.
- **Eliminação** — excluir sua conta e os dados associados a qualquer momento (ver seção 6).
- **Informação sobre compartilhamento** — saber com quem compartilhamos seus dados (ver seção 4 — hoje, ninguém).
- **Revogação do consentimento** — quando o tratamento depender de consentimento, você pode revogá-lo a qualquer momento.

Para exercer qualquer um desses direitos, entre em contato pelo e-mail informado na seção 1.

## 8. Dados de crianças

O perfil da criança no PACUS é criado e administrado pelo adulto responsável — a criança não se cadastra de forma autônoma, não informa e-mail, e não tem acesso às configurações da família. O ato do adulto criar o perfil da criança e configurar seu acesso constitui a base legal para o tratamento dos dados dela (LGPD, art. 14, §1º).

🔲 *Nota interna (não faz parte do texto final): ainda não temos uma tela de consentimento específica e destacada para isso — é o item C3 do nosso checklist interno, marcado como decisão de produto pendente.*

## 9. Segurança

Aplicamos medidas técnicas para proteger seus dados, entre elas:

- Senhas e PINs armazenados com hash (nunca em texto puro).
- Isolamento dos dados por família — cada família só acessa os próprios dados.
- Autenticação obrigatória (token) em todos os endpoints que expõem dados pessoais.
- Limite de tentativas de login/PIN, para dificultar ataques de força bruta.
- Log de auditoria para ações administrativas sensíveis.
- Comunicação com o servidor via HTTPS.

Nenhum sistema é 100% imune a incidentes. Caso ocorra um incidente de segurança que afete seus dados, seguiremos nosso plano de resposta a incidentes 🔲 *(referência ao item D3 do checklist interno, ainda a ser implementado)* e cumpriremos as obrigações de notificação previstas na LGPD.

## 10. Alterações nesta política

Podemos atualizar esta política periodicamente. Quando isso acontecer, atualizaremos a data no topo do documento 🔲 *(decisão de produto pendente: se e como notificar usuários existentes de mudanças relevantes — hoje não há mecanismo de notificação no app)*.

## 11. Contato

Dúvidas sobre esta política ou sobre como tratamos seus dados: pedro.hdslima98@gmail.com
