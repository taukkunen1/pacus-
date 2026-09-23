# PACUS — Estado atual do projeto

Atualizado em 2026-09-23 após a auditoria final da migração para Flutter Web.

## Fonte de verdade

- Branch de produção: `main`.
- Frontend oficial: `flutter_app/` (Flutter Web).
- Backend: ASP.NET Core 10 / C#.
- Banco: MongoDB Atlas.
- API de produção: Fly.io (`pacus-pacus-api.fly.dev`).
- Site de produção: GitHub Pages, domínio `www.pacus.com.br`.
- O antigo frontend HTML/CSS/JS foi aposentado e removido do repositório.

## Funcionalidades atuais

### Conta e família
- login de adulto;
- login de membro por código da família + PIN;
- criação de família;
- redefinição de senha com código de recuperação;
- troca de PIN;
- inclusão de membro;
- fuso horário por família;
- modo claro/escuro/sistema.

### Rotina e autonomia
- rotina de Hoje;
- tarefas obrigatórias, esperadas e desafios;
- criação, edição, remoção e ordenação;
- opções de escolha;
- justificativa de importância;
- registro de iniciativa;
- motivo de tarefa não realizada;
- planejamento da noite;
- planejamento de Amanhã;
- sugestões/autonomia;
- histórico de dias;
- atualização automática da tela na virada do dia;
- reconhecimento de esforço ao concluir tarefas.

### Pontos e recompensas
- saldo de Pacus Points;
- extrato;
- valor de referência em reais configurável pelo adulto;
- ajuste manual de saldo pelo adulto;
- loja;
- limite diário;
- estoque;
- resgate;
- aprovação/rejeição;
- crédito de minutos de tela.

### Tempo de tela
- saldo diário;
- sessões por duração escolhida;
- pausa/retomada local da sessão;
- consumo no backend ao finalizar;
- ajuste do saldo pelo adulto;
- alarme sonoro de término.

### PACUS
- estágios Ovo, Rachando, Eclosão, Bebê, Jovem e Adulto;
- crescimento por dias vividos;
- histórico de estágios;
- ajuste administrativo pelo adulto;
- visual animado no Flutter.

### Comunicação
- chat privado por família;
- histórico persistido no MongoDB;
- atualização periódica;
- identificação do remetente;
- isolamento por `FamilyId`;
- estado de leitura por usuário;
- contagem de mensagens não lidas;
- badge do Chat atualizado em segundo plano.

### Navegação
- Hoje;
- Amanhã;
- Chat;
- Histórico;
- Pontos;
- PACUS;
- Loja;
- Config (adulto);
- badges de tarefas pendentes, mensagens não lidas e resgates aguardando aprovação.

## CI/CD

`.github/workflows/ci.yml`:
- `dotnet restore`;
- `dotnet build`;
- `dotnet test`;
- `flutter pub get`;
- `flutter analyze`;
- `flutter test`;
- `flutter build web`.

`.github/workflows/pages.yml` compila e publica o Flutter Web em cada alteração relevante no `main`.

## Segurança e LGPD

O checklist principal continua em `docs/SECURITY_LGPD_CHECKLIST.md`. O frontend Flutter mantém as páginas:
- `flutter_app/web/privacidade.html`;
- `flutter_app/web/termos.html`.

Endpoints sensíveis permanecem protegidos por JWT e regras de papel/família no backend.

## Auditoria da migração Flutter

A comparação funcional final está em `docs/FLUTTER_MIGRATION_AUDIT.md`.

Resultado: o Flutter cobre o conjunto funcional útil do frontend legado. As diferenças encontradas na auditoria foram portadas quando ainda tinham valor de produto; código legado sem uso real não foi carregado para a nova base.


## Hospedagem e CORS

A migração Render → Fly.io está encerrada.

- Frontend: GitHub Pages em `https://www.pacus.com.br`.
- API: Fly.io em `https://pacus-pacus-api.fly.dev`.
- Banco: MongoDB Atlas.
- O antigo serviço do Render foi suspenso.
- Em produção, o backend aceita CORS exclusivamente de `https://www.pacus.com.br`.
- Configurações antigas de CORS no provedor não conseguem reabrir origens aposentadas, porque a whitelist de produção é aplicada no código.


## Monitoramento de produção

`.github/workflows/production-health.yml` executa a cada 15 minutos e também manualmente:
- verifica `https://www.pacus.com.br`;
- verifica `GET /api/v1/health`;
- exige `status=ok` e `database=connected`;
- confirma CORS para `https://www.pacus.com.br`;
- confirma que a origem aposentada `https://pacus-1.onrender.com` continua bloqueada.

## V2 — comunicação e notificações

A V2 foi iniciada em 2026-09-23 pela fundação de notificações internas:
- mensagens não lidas são calculadas por usuário;
- leitura é persistida no backend;
- o Chat mostra badge na navegação;
- o shell atualiza o badge em segundo plano.

Incrementos já implementados nesta etapa:
- pedidos rápidos da criança no Chat (`Preciso de ajuda`, `Mudar tarefa`, `+10 min`, `+20 min`);
- aprovação/rejeição pelo adulto no próprio Chat;
- pedidos de tempo extra aprovados alteram o game timer real;
- pedidos pendentes continuam sinalizados no badge mesmo depois da leitura.

Próximos incrementos possíveis: centro de notificações consolidado e, depois, push web/PWA.


## Testes Flutter

A suíte Flutter cobre atualmente:
- parsing e regras derivadas de `DailyTask`/`DailyRoutine`;
- sessão e papel Adult/Child;
- mensagens de esforço;
- formatação e labels do Chat;
- cliente HTTP: Bearer token, chamadas públicas, erros da API e limpeza de sessão em 401.
