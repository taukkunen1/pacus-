# PACUS — Checklist de Segurança e LGPD

Gerado a partir da auditoria de 2026-08-28/29. Regra: seguimos esta lista, um item por vez, na ordem abaixo. Cada item marcado `[x]` foi concluído e commitado em `feature/next-migration`.

Convenção:
- **[AQUI]** — dá pra fazer nesta sessão (código, testes, documento que eu redijo).
- **[DEPOIS]** — precisa de acesso externo (Render, MongoDB Atlas, GitHub settings) ou decisão/assinatura sua. Fica anotado aqui pra não esquecer, eu não mexo nisso sozinho.

---

## Fase A — Segurança (Prioridade 1, itens que faltam)

- [x] **[AQUI] A1. Rate limiting no login (adulto e criança) e no bootstrap.** Hoje não existe limite de tentativas — o PIN da criança tem só 4 dígitos (10.000 combinações), então dá pra forçar bruta sem nenhum bloqueio. Implementar limite por IP/usuário nos endpoints `/api/v1/auth/*` e `/api/v1/bootstrap`.
- [x] **[AQUI] A2. Testes de isolamento por família nos controllers que ainda não têm** (Store, Habitat, Points, DailyRoutines, Settings) — hoje só `TasksController` tem esse tipo de teste. Replicar o padrão criado em `TasksHttpIntegrationTests.cs`.
- [x] **[AQUI] A3. Testes de manipulação de ObjectId / troca de papel** (criança tentando ação de adulto, adulto de uma família usando id de outra) nos endpoints que ainda não cobrem isso.
- [x] **[AQUI] A4. Renomear os campos `UserId` que na verdade significam `FamilyId`** em `DailyRoutine`, `TaskTemplate`, `StoreItem`, `Redemption`, `Settings`, `PointTransaction` (entidades + repositórios + serviços). Não é bug hoje, mas é a maior fonte provável de bug futuro.
- [ ] **[AQUI] A5. Log de auditoria para ações administrativas sensíveis** (excluir tarefa, aprovar/rejeitar resgate, ajustar pontos manualmente) — registrar quem fez, quando, e o que mudou, separado do dado em si.
- [ ] **[DEPOIS] A6. Confirmar no Render/Atlas:** usuário do MongoDB com privilégio mínimo, rede restrita (IP allowlist), TLS ativo, `CORS_ALLOWED_ORIGINS` configurado só com o domínio real (não `*`), HTTPS redirect funcionando de fato em produção.
- [ ] **[DEPOIS] A7. Checar o histórico do Git em busca de segredo vazado** (procurar se `JWT_SECRET`/connection string do Mongo já apareceram em algum commit antigo) e rotacionar se achar algo.
- [ ] **[DEPOIS] A8. Rotacionar o PAT clássico do GitHub** (`ghp_...`, escopo `repo` completo) por um fine-grained token limitado só a este repositório, ou revogar quando não precisar mais de push automatizado.

## Fase B — LGPD: base (Prioridade 2)

- [ ] **[AQUI] B1. Mapa de dados** — documento cobrindo as 11 collections (`users`, `pacus`, `daily_routines`, `task_templates`, `point_transactions`, `pacus_growth`, `task_events`, `habitats`, `settings`, `store_items`, `redemptions`): campo, finalidade, categoria do titular, origem, quem acessa, base de tratamento, retenção, destino em exclusão, controles de segurança.
- [ ] **[AQUI] B2. Endpoint de exportação de dados** (adulto exporta os dados da própria família em JSON/CSV).
- [ ] **[AQUI] B3. Endpoint de exclusão de conta** com estratégia de cascata/anonimização definida por collection (a partir do mapa de dados do B1).
- [ ] **[AQUI] B4. Rascunho de Política de Privacidade e Termos de Uso** (documento — você revisa e, se quiser, valida com um advogado antes de publicar).
- [ ] **[DEPOIS] B5. Publicar Política de Privacidade/Termos no site**, decidir onde ficam hospedados e como o usuário aceita (tela de consentimento) — decisão de produto/UX sua.
- [ ] **[DEPOIS] B6. Registro de consentimentos** — depende de decidir o fluxo de aceite (B5) antes de implementar o armazenamento.

## Fase C — Crianças (Prioridade 3)

- [ ] **[AQUI] C1. Revisão dos campos da collection `users`** — checar se há dado coletado sem necessidade clara e propor remoção.
- [ ] **[AQUI] C2. Teste específico: criança da Família A não enumera/acessa nada da Família B** (cobertura adicional além do que já existe).
- [ ] **[DEPOIS] C3. Decisão de produto sobre vínculo responsável-criança e regras de criação de conta infantil** (ex.: exigir e-mail do responsável, dupla confirmação) — depende de como vocês querem que o cadastro funcione.

## Fase D — Governança (Prioridade 4)

- [ ] **[AQUI] D1. Registro das operações de tratamento** (um por fluxo: cadastro/login adulto, autenticação da criança, tarefas, pontos, crescimento do PACUS, loja/resgates, exclusão de conta) — depende do B1 estar pronto.
- [ ] **[AQUI] D2. RIPD** (Relatório de Impacto à Proteção de Dados) focado em: dados de crianças, autenticação, isolamento por família, exposição por ObjectId, exclusão de conta, incidentes, dados de comportamento/rotina — depende do B1/D1.
- [ ] **[AQUI] D3. Plano de resposta a incidentes** (detecção, investigação, contenção, evidências, correção, avaliação de impacto, comunicação, registro, revisão pós-incidente).
- [ ] **[DEPOIS] D4. Canal de contato de privacidade** (e-mail/formulário dedicado) — precisa de um endereço/infra que só você pode criar.

## Fase E — CI/infra (achado durante a validação)

- [ ] **[DEPOIS] E1. Decidir como consolidar `main` e `feature/next-migration`** (históricos desconexos hoje — nada no branch de trabalho passa pelo CI oficial de `main` até isso ser resolvido). Ex.: reescrever `main` a partir do branch de trabalho, ou abrir um merge com `--allow-unrelated-histories`.

---

### Progresso
_(atualizado a cada item concluído)_

- 2026-08-28/29: Corrigido `TasksController.Delete` sem checagem de família (commit `979cd19`), criado `TasksHttpIntegrationTests.cs`, corrigido `JWT_SECRET` ausente no CI e dois testes desatualizados (regra de pontos antiga). CI validado verde.
- 2026-08-29: **A1 concluído.** Rate limiting nativo do ASP.NET Core (auth: 10/5min por IP, bootstrap: 5/15min por IP), desativado em Development pra não quebrar os testes de integração. Corrigido de brinde um teste com bug de timezone (`HistoryHttpIntegrationTests` calculava "hoje" em UTC em vez de America/Sao_Paulo). CI validado verde.
- 2026-08-29: **A2 concluído.** Adicionados testes de isolamento por família em Store, Habitat, Points, Settings e DailyRoutines (`StoreHttpIntegrationTests.cs`, `HabitatHttpIntegrationTests.cs`, `PointsHttpIntegrationTests.cs`, `SettingsHttpIntegrationTests.cs` novo, `DailyRoutineHttpIntegrationTests.cs`). Nenhum bug encontrado nesses controllers -- todos já escopam por `FamilyId` do token, sem id externo. CI validado verde (commit `d2bb249`). Removido trigger/debug temporário do `ci.yml`.
- 2026-08-29: **A3 concluído.** Adicionados testes de manipulação de ObjectId / troca de papel: `DailyTasksHttpIntegrationTests.TaskOperations_WithAnotherFamilysTaskId_ShouldNotBeAllowed` (id de tarefa de outra família em complete/update/delete -- BadRequest, garantia estrutural via `GetLatestOpenAsync` escopado por FamilyId), `PointsHttpIntegrationTests.AdjustBalance_ShouldBeForbiddenForChild`, `DailyRoutineHttpIntegrationTests.AdjustGameTimer_ShouldBeForbiddenForChild`, e novo arquivo `PacusHttpIntegrationTests.cs` (UpdateState -- controller não tinha nenhuma cobertura ainda: forbidden para criança, isolamento por família). CI validado verde. Removido trigger/debug temporário do `ci.yml`.
- 2026-08-29: **A4 concluído.** Renomeado `UserId` -> `FamilyId` em `DailyRoutine`, `TaskTemplate`, `StoreItem`, `Redemption`, `Settings` e `PointTransaction` (entidades, repositórios e services), com `[BsonElement("userId")]` explícito preservando o nome do campo já gravado no Mongo -- nenhuma migração de dados necessária. DTOs de resposta da API mantidos como estavam (contrato público do frontend não muda). Fora do escopo: `PacusGrowthLog.UserId` e `TaskEvent.UserId` não estavam na lista do item A4. CI validado verde de primeira. Removido trigger temporário do `ci.yml`.
