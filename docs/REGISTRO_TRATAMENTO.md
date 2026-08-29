# PACUS — Registro das Operações de Tratamento (LGPD, art. 37)

Documento de referência para conformidade com a LGPD (Lei 13.709/2018), item D1 do checklist de segurança e LGPD. Organiza o que o `docs/DATA_MAP.md` já mapeou por *collection* em uma visão por **fluxo operacional** — a forma como o art. 37 da LGPD pede o registro das operações de tratamento.

Cobre os 7 fluxos indicados no checklist, mais 2 adicionais que também são operações de tratamento de dados pessoais e ficariam incompletos se omitidos: exportação de dados (B2) e logs de auditoria/segurança (A1 + A5). Cada fluxo referencia as seções correspondentes do `docs/DATA_MAP.md` para o detalhamento campo a campo — este documento não repete tabelas de campo, foca na operação como um todo.

## Como ler este documento

Cada fluxo tem:

- **Descrição** — o que a operação faz, em uma frase.
- **Categorias de titulares** — adulto, criança, ou ambos.
- **Categorias de dados tratados** — resumo, com link para o `docs/DATA_MAP.md`.
- **Finalidade** — por que o tratamento existe.
- **Base legal (LGPD)** — art. 7º (dados em geral) ou a combinação com o art. 14 (criança).
- **Collections envolvidas** — onde o dado é persistido.
- **Quem opera** — camadas do backend que tocam o dado.
- **Compartilhamento com terceiros** — hoje, nenhum tratamento envolve terceiros (ver Política de Privacidade, seção 4).
- **Retenção e eliminação** — por quanto tempo, e o que acontece na exclusão de conta (B3).
- **Medidas de segurança** — controles já implementados.

---

## 1. Cadastro e login do adulto

**Descrição:** criação da conta da família (bootstrap) e autenticação subsequente do adulto responsável.

- **Categorias de titulares:** adulto.
- **Categorias de dados:** identificação (nome, e-mail), credencial (senha, com hash), fuso horário. Ver `docs/DATA_MAP.md`, seção 1 (`users`).
- **Finalidade:** permitir que o adulto crie e acesse a conta da família.
- **Base legal:** execução de contrato / procedimentos preliminares (art. 7º, V).
- **Collections envolvidas:** `users`.
- **Quem opera:** `BootstrapController`/`BootstrapService` (criação), `AuthController`/`AuthService` (login), `PasswordHasher` (hash da senha), `JwtTokenService` (emissão do token de sessão).
- **Compartilhamento com terceiros:** nenhum.
- **Retenção e eliminação:** enquanto a conta estiver ativa; hard delete na exclusão de conta (B3).
- **Medidas de segurança:** senha com hash PBKDF2-SHA256 (100.000 iterações, salt por senha), rate limiting no login e no bootstrap (item A1), token JWT com expiração (12h para o adulto), e-mail nunca reutilizável entre contas (índice único).

## 2. Autenticação da criança

**Descrição:** acesso da criança ao perfil criado pelo adulto, usando um PIN numérico de 4 dígitos em vez de e-mail/senha.

- **Categorias de titulares:** criança.
- **Categorias de dados:** identificação (nome), credencial (PIN, com hash), fuso horário. Ver `docs/DATA_MAP.md`, seção 1 (`users`).
- **Finalidade:** permitir que a criança acesse o próprio perfil dentro da conta da família, sem precisar de e-mail.
- **Base legal:** consentimento do responsável, exercido no ato de o adulto criar o perfil da criança (art. 14, §1º) — ver nota sobre dados de crianças no `docs/DATA_MAP.md`.
- **Collections envolvidas:** `users`.
- **Quem opera:** `AuthController`/`AuthService` (login), `PasswordHasher` (hash do PIN), `JwtTokenService` (emissão do token de sessão, 7 dias de validade — mais longo que o do adulto, pensado para tablet compartilhado da família).
- **Compartilhamento com terceiros:** nenhum.
- **Retenção e eliminação:** enquanto a conta estiver ativa; hard delete na exclusão de conta (B3), feita pelo adulto.
- **Medidas de segurança:** PIN com hash (nunca texto puro), rate limiting no login (item A1) — mitigação relevante aqui porque um PIN de 4 dígitos tem só 10.000 combinações, o perfil da criança não tem e-mail associado (não pode ser usado para login em outro lugar), e o token da criança nunca concede acesso a rotas restritas ao adulto (`[RequireRole(UserRole.Adult)]`).

## 3. Tarefas (configuração e execução diária)

**Descrição:** o adulto configura tarefas permanentes da família; o app gera e acompanha a execução diária dessas tarefas (rotina do dia), que a criança e o adulto marcam como concluídas.

- **Categorias de titulares:** ambos (o adulto configura; a criança e o adulto executam/registram).
- **Categorias de dados:** conteúdo das tarefas (título, tipo, pontos, período) e histórico de execução diária. Ver `docs/DATA_MAP.md`, seções 3 (`daily_routines`), 4 (`task_templates`) e 7 (`task_events`).
- **Finalidade:** é o propósito central do app — acompanhar a rotina da família.
- **Base legal:** execução de contrato (art. 7º, V); para a criança, dentro do consentimento dado pelo responsável ao criar o perfil (art. 14, §1º).
- **Collections envolvidas:** `daily_routines`, `task_templates`, `task_events`.
- **Quem opera:** `TasksController`/`TaskTemplateService` (configuração permanente), `DailyTasksController`/`DailyRoutineService` (execução do dia), `DayClosingService` (fechamento automático de dias anteriores), `TaskEventRepository` (log interno de eventos de tarefa).
- **Compartilhamento com terceiros:** nenhum.
- **Retenção e eliminação:** indefinida enquanto a conta existir (é o histórico que o app existe para preservar); hard delete na exclusão de conta (B3).
- **Medidas de segurança:** isolamento por `FamilyId` em toda leitura/escrita, permissões configuráveis por criança (`ChildPermissions` — o adulto pode restringir o que a criança edita), log de auditoria para exclusão permanente de tarefa (item A5).

## 4. Pontos (Pacus Points)

**Descrição:** cálculo e histórico do saldo de pontos que a família acumula ao concluir tarefas, incluindo ajustes manuais feitos pelo adulto.

- **Categorias de titulares:** ambos (pontos são da família, mas cada transação registra quem a originou).
- **Categorias de dados:** transações de pontos (valor, motivo, tarefa de origem, quem registrou). Ver `docs/DATA_MAP.md`, seção 5 (`point_transactions`).
- **Finalidade:** dar à família um retrato transparente e auditável do saldo de pontos, sem depender de um único "número mágico" — o saldo é sempre a soma das transações.
- **Base legal:** execução de contrato (art. 7º, V).
- **Collections envolvidas:** `point_transactions`.
- **Quem opera:** `PointsController`/`PointsService`/`PointTransactionRepository`.
- **Compartilhamento com terceiros:** nenhum.
- **Retenção e eliminação:** indefinida (é o extrato da família); hard delete na exclusão de conta (B3).
- **Medidas de segurança:** isolamento por `FamilyId`, ajuste manual de saldo restrito ao adulto (`[RequireRole(UserRole.Adult)]`) e sempre gera log de auditoria (item A5).

## 5. Crescimento do PACUS (o bichinho virtual)

**Descrição:** o estado do PACUS (o bichinho de estimação virtual da família) evolui uma vez por dia encerrado, independentemente da conclusão das tarefas; a aparência do "habitat" pode ser customizada pela família.

- **Categorias de titulares:** ambos (o crescimento reflete o uso da família como um todo).
- **Categorias de dados:** estágio de crescimento, histórico de evolução, customizações visuais do habitat. Ver `docs/DATA_MAP.md`, seções 2 (`pacus`), 6 (`pacus_growth`) e 8 (`habitats`).
- **Finalidade:** o elemento lúdico central do app — dar à família um retorno visual e não-punitivo do uso da rotina.
- **Base legal:** execução de contrato (art. 7º, V).
- **Collections envolvidas:** `pacus`, `pacus_growth`, `habitats`.
- **Quem opera:** `PacusController`/`DayClosingService` (avanço de estágio), `HabitatController` (customização visual).
- **Compartilhamento com terceiros:** nenhum.
- **Retenção e eliminação:** indefinida; hard delete na exclusão de conta (B3).
- **Medidas de segurança:** isolamento por `FamilyId`.

## 6. Loja de recompensas e resgates

**Descrição:** o adulto cadastra itens resgatáveis com Pacus Points; a criança solicita o resgate; o adulto aprova ou rejeita.

- **Categorias de titulares:** ambos (o adulto cadastra e aprova; a criança solicita).
- **Categorias de dados:** itens da loja (título, custo, estoque) e resgates (item, status, quem solicitou/revisou). Ver `docs/DATA_MAP.md`, seções 10 (`store_items`) e 11 (`redemptions`).
- **Finalidade:** dar à criança um destino concreto para os pontos acumulados, com controle do adulto sobre o que é oferecido e aprovado.
- **Base legal:** execução de contrato (art. 7º, V); para a solicitação de resgate pela criança, dentro do consentimento dado pelo responsável (art. 14, §1º).
- **Collections envolvidas:** `store_items`, `redemptions`.
- **Quem opera:** `StoreController`/`StoreService`/`StoreRepository`.
- **Compartilhamento com terceiros:** nenhum. Pacus Points não são moeda real — nenhum processador de pagamento está envolvido (ver Termos de Uso, seção 5).
- **Retenção e eliminação:** indefinida (histórico de resgates); hard delete na exclusão de conta (B3). `redemptions.itemTitle`/`cost` são cópias congeladas no momento do pedido, preservadas mesmo se o item for depois alterado ou desativado.
- **Medidas de segurança:** isolamento por `FamilyId` (a checagem de posse do item cobre inclusive a criança tentando usar id de outra família — item C2), aprovação/rejeição restrita ao adulto (`[RequireRole(UserRole.Adult)]`), cada decisão gera log de auditoria (item A5), baixa de estoque atômica com a aprovação.

## 7. Exclusão de conta

**Descrição:** o adulto solicita a exclusão permanente da conta da família, mediante confirmação de senha.

- **Categorias de titulares:** ambos (a exclusão apaga os dados de toda a família).
- **Categorias de dados:** todos os tratados nos fluxos 1-6, mais os logs de auditoria (fluxo 9).
- **Finalidade:** dar ao titular o exercício efetivo do direito de eliminação (LGPD, art. 18, VI).
- **Base legal:** cumprimento de obrigação legal (art. 7º, II) — é o próprio exercício de um direito do titular previsto na LGPD.
- **Collections envolvidas:** todas as 12 (11 com hard delete; `audit_logs` é anonimizado, não apagado — ver fluxo 9).
- **Quem opera:** `AccountController`/`AccountDeletionService` (item B3).
- **Compartilhamento com terceiros:** nenhum.
- **Retenção e eliminação:** a própria operação É a eliminação. É irreversível e não tem prazo de retenção posterior, exceto os logs de auditoria anonimizados (12 meses, ver fluxo 9).
- **Medidas de segurança:** restrito ao adulto (`[RequireRole(UserRole.Adult)]`), exige confirmação da senha atual (reautenticação contra sessão esquecida/token vazado), a própria exclusão gera uma entrada de auditoria (já anonimizada).

## 8. Exportação de dados *(adicional — não estava na lista original do checklist, mas é uma operação de tratamento)*

**Descrição:** o adulto baixa uma cópia de todos os dados da família em formato JSON.

- **Categorias de titulares:** ambos (o arquivo inclui dados de adulto e criança).
- **Categorias de dados:** todas as 12 collections, exceto `passwordHash`/`pinHash` (nunca incluídos).
- **Finalidade:** dar ao titular o exercício efetivo do direito de portabilidade/acesso (LGPD, art. 18, II e V).
- **Base legal:** cumprimento de obrigação legal (art. 7º, II).
- **Collections envolvidas:** todas as 12 (leitura apenas — nenhum dado é alterado por este fluxo).
- **Quem opera:** `ExportController`/`DataExportService` (item B2).
- **Compartilhamento com terceiros:** nenhum — o arquivo é gerado e entregue diretamente ao adulto que fez a requisição, via download HTTP.
- **Retenção e eliminação:** não aplicável — o backend não guarda cópia do arquivo gerado; ele existe só na resposta HTTP.
- **Medidas de segurança:** restrito ao adulto (`[RequireRole(UserRole.Adult)]`), projeção dedicada que nunca inclui hash de senha/PIN, isolamento por `FamilyId`.

## 9. Logs de auditoria e segurança *(adicional — não estava na lista original do checklist, mas é uma operação de tratamento)*

**Descrição:** registro de ações administrativas sensíveis (exclusão de tarefa, aprovação/rejeição de resgate, ajuste manual de pontos, exclusão de conta) e limite de tentativas em formulários de autenticação.

- **Categorias de titulares:** ambos (qualquer um dos dois papéis pode ser o ator de uma ação registrada).
- **Categorias de dados:** quem fez a ação (`ActorId`, `ActorRole`), o quê, quando, e um detalhe legível. Ver `docs/DATA_MAP.md`, seção 12 (`audit_logs`).
- **Finalidade:** responsabilização (reconstruir quem fez o quê) e prevenção a fraude/abuso, inclusive tentativas de força bruta em login/PIN.
- **Base legal:** legítimo interesse (art. 7º, IX), equilibrado com a minimização de dados exigida pela LGPD.
- **Collections envolvidas:** `audit_logs`. O rate limiting de login (item A1) atua em memória/infraestrutura, sem persistir dados pessoais em uma collection própria.
- **Quem opera:** `AuditLogRepository`, chamado a partir de `TaskTemplateService`, `StoreService`, `PointsController`, `AccountDeletionService`.
- **Compartilhamento com terceiros:** nenhum.
- **Retenção e eliminação:** enquanto a conta estiver ativa, os logs permanecem vinculados ao autor. Na exclusão de conta (B3), são **anonimizados** (perdem o vínculo com a pessoa) e purgados automaticamente 12 meses depois via índice TTL do MongoDB — não são apagados imediatamente, ao contrário das outras 11 collections, porque preservar o registro da ação (sem o vínculo pessoal) por um tempo após a exclusão é o que sustenta a finalidade de responsabilização.
- **Medidas de segurança:** nunca exposto na UI normal do app (só via exportação, fluxo 8); nunca editado ou removido pelo fluxo normal da aplicação, só pela rotina de exclusão de conta.

---

## Observações finais

- Nenhum dos 9 fluxos envolve dado sensível (LGPD, art. 5º, II) ou compartilhamento com terceiros para fins comerciais — confirmado no B1 (`docs/DATA_MAP.md`) e no C1 (revisão de campos de `users`).
- Este documento é a base direta para o **D2** (Relatório de Impacto à Proteção de Dados), que aprofunda os riscos de cada fluxo, e complementa o **D3** (plano de resposta a incidentes).
