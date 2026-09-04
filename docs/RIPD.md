# PACUS — Relatório de Impacto à Proteção de Dados (RIPD/DPIA)

Documento de referência para conformidade com a LGPD (Lei 13.709/2018, art. 5º, XVII e art. 38), item D2 do checklist de segurança e LGPD. Avalia os riscos aos titulares de dados no PACUS, focado nas sete áreas indicadas no checklist: dados de crianças, autenticação, isolamento por família, exposição por ObjectId, exclusão de conta, incidentes de segurança e dados de comportamento/rotina.

Este relatório se apoia diretamente no `docs/DATA_MAP.md` (B1, o que existe) e no `docs/REGISTRO_TRATAMENTO.md` (D1, por que existe) — aqui o foco é **o que pode dar errado** e **o quanto já foi mitigado**.

## Como ler este documento

Cada área de risco tem:

- **Descrição do risco** — o cenário concreto que preocupa.
- **Titulares afetados** — quem sofreria o impacto.
- **Probabilidade** e **Severidade** — avaliação qualitativa (Baixa/Média/Alta), sem dados históricos de incidente real (o app é novo) — portanto uma estimativa de engenharia, não uma estatística.
- **Mitigações já implementadas** — o que existe hoje no código, com referência ao item do checklist que introduziu.
- **Risco residual** — o que resta depois das mitigações.
- **Recomendação** — o que fazer a seguir, se algo.

---

## 1. Dados de crianças

**Descrição do risco:** o PACUS trata dados de crianças (nome, PIN de acesso, histórico de tarefas e comportamento de uso) sem que a criança tenha capacidade de consentir por si mesma, e sem verificação de que quem se cadastra como "adulto responsável" é de fato quem diz ser.

- **Titulares afetados:** criança.
- **Probabilidade:** Baixa (o modelo de cadastro já pressupõe que só quem tem acesso à conta da família cria perfis dentro dela).
- **Severidade:** Média (dados de criança pedem cuidado redobrado por natureza, mas o volume/sensibilidade dos dados tratados aqui é baixo — sem dado sensível, sem geolocalização, sem conteúdo gerado livremente pela criança).
- **Mitigações já implementadas:**
  - A criança não se cadastra sozinha; o perfil é criado e controlado pelo adulto (base legal do art. 14, §1º, exercida no próprio fluxo de bootstrap — ver `docs/DATA_MAP.md`).
  - A criança não informa e-mail nem qualquer identificador externo.
  - Nenhum dado sensível (art. 5º, II) é coletado de ninguém, criança incluída.
  - PIN da criança tratado com o mesmo hashing forte do adulto (PBKDF2-SHA256).
  - Rate limiting no login da criança (item A1) — mitiga o risco específico de um PIN de 4 dígitos ser mais fácil de adivinhar que uma senha.
- **Risco residual:** não há verificação de idade/identidade de quem se cadastra como adulto (permanece uma limitação conhecida, dependente de identidade externa e fora do escopo deste produto).
- **Recomendação:** o item **C3** do checklist foi implementado — o cadastro (`BootstrapRequest.ResponsibleConsent`) agora exige um checkbox de consentimento específico e destacado ("Confirmo que sou responsável pela criança e autorizo o tratamento dos dados necessários para usar o PACUS"), versionado via `BootstrapService.ChildDataConsentVersion`, distinto do aceite genérico dos Termos. O item pode ser considerado `[FEITO]`.

## 2. Autenticação

**Descrição do risco:** comprometimento de credenciais (senha do adulto, PIN da criança) levando a acesso não autorizado à conta da família, incluindo tentativas de força bruta.

- **Titulares afetados:** ambos.
- **Probabilidade:** Baixa a Média (PIN de 4 dígitos é um alvo relativamente fácil de força bruta sem proteção — mas já mitigado, ver abaixo).
- **Severidade:** Alta (comprometer a conta do adulto dá acesso a todos os dados da família, incluindo os da criança).
- **Mitigações já implementadas:**
  - Hashing PBKDF2-SHA256 com salt por senha/PIN — mesmo um vazamento do banco não expõe credenciais em texto puro (item A1, e já era assim antes da auditoria).
  - Rate limiting por IP/usuário em `/api/v1/auth/*` e `/api/v1/bootstrap` (item A1) — mitiga força bruta tanto na senha do adulto quanto no PIN de 4 dígitos da criança.
  - Tokens JWT com expiração (12h adulto, 7 dias criança) — limita a janela de um token vazado.
  - Mensagens de erro genéricas no login ("email ou senha inválidos") — não revelam se o e-mail existe.
- **Risco residual:** não há autenticação de dois fatores (2FA), nem notificação ao adulto de login em novo dispositivo/local. Não há política de expiração/rotação de senha.
- **Recomendação:** para o estágio atual do produto (app familiar, não uma conta financeira), a superfície de risco parece proporcional às mitigações já implementadas. 2FA e alertas de novo dispositivo são melhorias razoáveis para uma fase futura, não bloqueadores.

## 3. Isolamento por família

**Descrição do risco:** uma família conseguir ler, alterar ou apagar dados de outra família — o risco mais grave de uma arquitetura multi-tenant sem isolamento adequado.

- **Titulares afetados:** ambos, de qualquer família.
- **Probabilidade:** Baixa (extensivamente testado — ver abaixo).
- **Severidade:** Alta (seria uma violação de confidencialidade completa entre famílias, incluindo dados de crianças).
- **Mitigações já implementadas:**
  - Toda consulta ao banco escopa por `FamilyId` extraído do token JWT — nunca de um parâmetro que o cliente controla.
  - Testes de integração de isolamento por família em todos os controllers que expõem dados (`Store`, `Habitat`, `Points`, `Settings`, `DailyRoutines`, `Tasks`, `Export`, `Account` — itens A2, A3, B2, B3).
  - Testes específicos de manipulação de id (tentar usar o id de um recurso de outra família) cobrindo tanto o adulto quanto a criança (itens A3 e C2).
  - `HistoryController` nem aceita id externo — o filtro é sempre implícito pelo token, eliminando a superfície de ataque por design, não por checagem.
- **Risco residual:** baixo, mas nunca zero — qualquer novo endpoint futuro que esqueça de escopar por `FamilyId` reintroduziria o risco. É uma responsabilidade de revisão de código contínua, não algo que se "resolve" de uma vez.
- **Recomendação:** manter o padrão de sempre incluir um teste de isolamento por família (e, quando aplicável, um teste específico com a criança) para qualquer endpoint novo que leia ou escreva dados — o padrão já está estabelecido em `TasksHttpIntegrationTests.cs` e replicado nos demais arquivos de teste.

## 4. Exposição por ObjectId

**Descrição do risco:** os identificadores usados pela API são `ObjectId` do MongoDB — sequenciais o suficiente para permitir enumeração (tentar ids vizinhos) se a autorização não estivesse bem implementada.

- **Titulares afetados:** ambos.
- **Probabilidade:** Baixa (a mitigação real não é esconder o id, é a checagem de posse — ver risco 3, isolamento por família).
- **Severidade:** Média (por si só, adivinhar um id de outra família não retorna nada — o dado só vazaria se o isolamento por família, risco 3, falhasse).
- **Mitigações já implementadas:**
  - O `ObjectId` nunca é a barreira de segurança sozinho — toda operação verifica também que o recurso pertence ao `FamilyId` do token, então saber (ou adivinhar) o id de um recurso de outra família não é suficiente.
  - Testes específicos de "outro id, mesma operação" (itens A3, C2) validam esse comportamento na prática, não só na teoria do design.
- **Risco residual:** `ObjectId` do MongoDB embute um timestamp de criação nos primeiros 4 bytes — tecnicamente permite inferir *quando* um recurso foi criado, mas não seu conteúdo, e isso não é um dado pessoal em si.
- **Recomendação:** nenhuma ação necessária — o design já trata `ObjectId` como não-confidencial e depende só da checagem de posse, que está testada. Não há necessidade de trocar para UUID aleatório ou qualquer outro esquema.

## 5. Exclusão de conta

**Descrição do risco:** falha ao apagar (ou apagar dado demais/de menos) na exclusão de conta — deixando dado pessoal retido além do necessário, ou destruindo dado que deveria ser preservado por obrigação legal (ex. investigação de fraude em andamento).

- **Titulares afetados:** a família que solicita a exclusão.
- **Probabilidade:** Baixa (implementado e testado — item B3).
- **Severidade:** Alta se falhar (é uma operação irreversível; um bug aqui não tem como ser corrigido depois).
- **Mitigações já implementadas:**
  - Estratégia por collection definida a partir do mapa de dados (B1): hard delete nas 11 collections de dados de uso, anonimização (não exclusão) dos logs de auditoria por 12 meses, com purga automática via índice TTL do MongoDB.
  - Exige senha atual do adulto — reduz o risco de exclusão por sessão esquecida ou token vazado.
  - Restrito ao adulto (`[RequireRole(UserRole.Adult)]`).
  - Testes de integração verificam, direto no banco, que os dados realmente saem das 11 collections e que os logs de auditoria são anonimizados (não apagados) com o vínculo removido.
  - Isolamento por família testado especificamente para este endpoint — excluir a Família A não pode afetar a Família B.
- **Risco residual:** a exclusão não é atômica entre collections (não há transação multi-documento cobrindo as 11 operações) — uma falha no meio da sequência deixaria a conta parcialmente excluída. Hoje isso exigiria uma falha de infraestrutura (queda de conexão com o Mongo) no meio da operação, não é provocável por um usuário.
- **Recomendação:** aceitável para o estágio atual. Se o volume de contas crescer a ponto de falhas parciais nesse fluxo virarem um problema prático, vale revisitar com uma transação do MongoDB (replica set já é pré-requisito de produção) ou um mecanismo de repetição/verificação pós-exclusão.

## 6. Incidentes de segurança

**Descrição do risco:** um incidente (vazamento de dados, comprometimento de credenciais de infraestrutura, bug explorado) acontecer sem que haja um processo definido de detecção, contenção e comunicação.

- **Titulares afetados:** potencialmente todas as famílias, dependendo do incidente.
- **Probabilidade:** não estimável de forma significativa sem um plano formal — é exatamente essa lacuna que o item D3 (plano de resposta a incidentes) endereça.
- **Severidade:** Alta (depende do incidente, mas o teto é "vazamento de dados de todas as famílias, incluindo crianças").
- **Mitigações já implementadas:**
  - Log de auditoria para ações administrativas sensíveis (item A5) — ajuda a reconstruir "o que aconteceu" depois de um incidente envolvendo essas ações.
  - Rate limiting (item A1) — reduz a superfície de ataques automatizados de força bruta.
  - Hashing forte de credenciais — reduz o dano de um vazamento do banco.
- **Risco residual:** não existe hoje um plano formal de resposta a incidentes (detecção, investigação, contenção, comunicação aos titulares e à ANPD quando aplicável). Itens de infraestrutura como TLS/CORS/allowlist de rede (item A6) e verificação de segredo vazado no histórico do Git (item A7) ainda são `[DEPOIS]`.
- **Recomendação:** **é o próprio item D3** deste checklist — plano de resposta a incidentes, tratado a seguir como o próximo item. Depois disso, os itens `[DEPOIS]` de infraestrutura (A6, A7, A8) fecham a lacuna restante, mas dependem de acesso que só vocês têm (Render, Atlas, configurações do GitHub).

## 7. Dados de comportamento/rotina

**Descrição do risco:** o histórico de tarefas, pontos e uso diário do app constitui, ao longo do tempo, um perfil relativamente detalhado do comportamento e da rotina de uma criança (que dia fez o quê, quando, com que frequência) — um tipo de dado que merece cautela mesmo sem ser "sensível" na definição estrita da LGPD.

- **Titulares afetados:** principalmente a criança (é o histórico de rotina dela que o app existe para acompanhar).
- **Probabilidade:** N/A (o tratamento é constante e intencional — é o próprio propósito do produto, não um risco acidental).
- **Severidade:** Média (o dado é sensível no sentido comum da palavra — revela hábitos de uma criança — mas não é dado sensível na definição do art. 5º, II da LGPD, e não sai do escopo familiar).
- **Mitigações já implementadas:**
  - O histórico só é acessível pela própria família, nunca por terceiros (não há compartilhamento, não há anúncio, não há perfilamento comercial — ver `docs/REGISTRO_TRATAMENTO.md`).
  - Isolamento por família cobre este dado como qualquer outro (risco 3).
  - Hard delete completo na exclusão de conta (risco 5) — nada desse histórico sobrevive à exclusão, ao contrário de serviços que "anonimizam mas retêm" dados comportamentais para analytics.
  - O app não faz nenhum tipo de perfilamento automatizado, recomendação algorítmica, ou decisão automatizada com efeito sobre a criança (art. 20 da LGPD não se aplica — não há decisão automatizada aqui, só exibição de dados para os próprios pais).
- **Risco residual:** o próprio volume de detalhe acumulado ao longo do tempo (meses/anos de rotina diária) é, por natureza, mais revelador quanto mais a família usa o app — um risco estrutural do produto, não um bug a corrigir.
- **Recomendação:** manter o padrão atual (uso estritamente interno à família, nenhum compartilhamento, exclusão completa e real quando solicitada) é a mitigação mais eficaz que existe para este tipo de dado. Não há uma ação de código específica a fazer aqui além do que os outros itens já cobrem.

---

## Conclusão

Das sete áreas avaliadas, seis (autenticação, isolamento por família, exposição por ObjectId, exclusão de conta, dados de comportamento/rotina, e agora dados de crianças com o consentimento destacado do item C3) têm mitigações técnicas já implementadas e testadas, com risco residual baixo para o estágio atual do produto. A única pendência que este relatório não pode fechar sozinho é o item D3 (plano de resposta a incidentes), tratado a seguir.

Nenhuma das sete áreas indica um risco alto e não mitigado que justificasse pausar o lançamento do produto — a conclusão do D3 é a peça que mais reduziria o risco residual remanescente, se priorizada.
