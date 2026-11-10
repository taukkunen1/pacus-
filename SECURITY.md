# Política de Segurança

## Como reportar uma vulnerabilidade

Não abra uma issue pública. Envie um e-mail para **pedro.hdslima98@gmail.com** com
o assunto "PACUS - segurança", descrevendo o problema e como reproduzi-lo. Também é
possível usar o recurso *Report a vulnerability* na aba Security do repositório.

Respondemos em até 7 dias. Por favor, não acesse dados de outras famílias além do
mínimo necessário para demonstrar o problema.

## Onde a segurança do PACUS é aplicada

O front (Flutter Web no GitHub Pages) é público: todo o controle de acesso fica na API.
Isolamento por família, papéis (adulto/criança), rate limit, bloqueio de conta e
auditoria estão descritos em `docs/SECURITY_LGPD_CHECKLIST.md`.

## Regras para contribuir

- Nunca commitar segredos (`MONGODB_URI`, `JWT_SECRET`, tokens). Use `fly secrets set`
  e GitHub Secrets. O workflow *Secret scan* bloqueia PRs com segredos.
- `JWT_SECRET` precisa ter pelo menos 32 caracteres aleatórios.
- Endpoint novo: sempre escopar por `FamilyId` do token e usar `[RequireRole]` quando
  for ação de adulto.
