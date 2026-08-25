# PACUS — Schemas do MongoDB Atlas

Banco: `pacus`
Driver: MongoDB .NET Driver
Todas as datas armazenadas em UTC (`ISODate`). Data operacional calculada no timezone do usuário na camada de aplicação.

---

## 1. users

```json
{
  "_id": ObjectId,
  "role": "adult" | "child",
  "name": "string",
  "email": "string | null",
  "passwordHash": "string | null",
  "pin": "string | null",
  "timezone": "America/Sao_Paulo",
  "familyId": ObjectId,
  "createdAt": ISODate,
  "updatedAt": ISODate
}
```

**Índices:**
- `{ email: 1 }` único (parcial: `email != null`)
- `{ familyId: 1 }`

**Notas:**
- `familyId` agrupa adulto(s) e criança(s) da mesma casa — permite múltiplos filhos/perfis no futuro sem redesenhar o schema.
- `pin` (hash) é o mecanismo de login da criança; `passwordHash` é para o adulto.

---

## 2. pacus

```json
{
  "_id": ObjectId,
  "userId": ObjectId,
  "name": "Pacus",
  "species": "axolotl",
  "birthDate": ISODate,
  "stage": "egg" | "cracking" | "hatching" | "baby" | "young" | "adult",
  "stageHistory": [
    { "stage": "egg", "reachedAt": ISODate },
    { "stage": "cracking", "reachedAt": ISODate }
  ],
  "size": "number",
  "totalClosedDays": "int",
  "lastGrowthDate": "2026-08-24",
  "createdAt": ISODate,
  "updatedAt": ISODate
}
```

**Índices:**
- `{ userId: 1 }` único

**Notas:**
- `lastGrowthDate` como string `YYYY-MM-DD` (data operacional, não timestamp) — evita ambiguidade de timezone na checagem de duplicidade.
- Estágios com datas fixas do calendário atual (09–31/08) podem ser resolvidos comparando `birthDate`/`totalClosedDays` contra thresholds configurados em `settings`, em vez de hardcoded — permite reiniciar o ciclo futuramente.
- `stageHistory` é opcional mas recomendado para a tela "PACUS" mostrar a linha do tempo.

---

## 3. task_templates

```json
{
  "_id": ObjectId,
  "userId": ObjectId,
  "title": "string",
  "description": "string | null",
  "type": "mandatory" | "expected" | "challenge",
  "period": "morning" | "afternoon" | "evening",
  "points": "int",
  "order": "int",
  "active": "boolean",
  "recurrence": "daily" | "weekday" | "weekend" | "custom",
  "createdBy": ObjectId,
  "createdAt": ISODate,
  "updatedAt": ISODate,
  "deletedAt": ISODate | null
}
```

**Índices:**
- `{ userId: 1, active: 1 }`

**Notas:**
- `deletedAt` implementa soft delete — a tarefa permanente some da geração de novos dias mas o registro é preservado (histórico de eventos referencia o `taskTemplateId`).
- `recurrence` cobre a "rotina padrão" (mistura de rotina fixa + edição diária) descrita na spec.

---

## 4. daily_routines

Documento por dia, com as tarefas **embutidas** (fotografia imutável após fechamento).

```json
{
  "_id": ObjectId,
  "userId": ObjectId,
  "date": "2026-08-24",
  "timezone": "America/Sao_Paulo",
  "status": "open" | "closed",
  "tasks": [
    {
      "id": "uuid",
      "taskTemplateId": ObjectId | null,
      "title": "Escovar os dentes",
      "description": "string | null",
      "type": "mandatory",
      "period": "morning",
      "order": 1,
      "points": 1,
      "status": "pending" | "done",
      "completedAt": ISODate | null,
      "createdBy": ObjectId,
      "origin": "template" | "child" | "adult",
      "deletedAt": ISODate | null,
      "createdAt": ISODate,
      "updatedAt": ISODate
    }
  ],
  "stats": {
    "mandatory": { "done": 4, "total": 5 },
    "expected": { "done": 5, "total": 7 },
    "challenge": { "done": 3, "total": 4 },
    "pointsEarned": 14,
    "completionRate": 0.70
  },
  "pointsEarned": "int",
  "closedAt": ISODate | null,
  "createdAt": ISODate
}
```

**Índices:**
- `{ userId: 1, date: 1 }` único
- `{ userId: 1, status: 1 }`

**Notas:**
- `tasks` embutido (não coleção separada) porque cada dia precisa ser uma cópia independente e imutável após fechado — evita joins e garante que alterar `task_templates` nunca reescreve o passado.
- `stats` é calculado no fechamento e congelado — a tela de Histórico lê direto daqui, sem reprocessar.
- Toggle de conclusão (marcar/desmarcar a qualquer momento) atualiza `tasks.$.status` + `completedAt`, e sempre gera um evento em `task_events` e uma transação em `point_transactions` (award ou reversal).

---

## 5. point_transactions

```json
{
  "_id": ObjectId,
  "userId": ObjectId,
  "date": "2026-08-24",
  "dailyRoutineId": ObjectId,
  "taskId": "uuid",
  "taskTitle": "Escovar os dentes",
  "type": "award" | "reversal" | "adjustment" | "redemption",
  "points": "int",
  "balanceAfter": "int",
  "reason": "string | null",
  "actorId": ObjectId,
  "actorRole": "child" | "adult",
  "createdAt": ISODate
}
```

**Índices:**
- `{ userId: 1, createdAt: -1 }`
- `{ userId: 1, date: 1 }`
- `{ dailyRoutineId: 1 }`

**Notas:**
- `points` é sempre um delta assinado: `award` positivo, `reversal` negativo (espelha exatamente o award revertido), `redemption` negativo (gasto na loja), `adjustment` positivo ou negativo (correção manual do adulto, ex. quando muda o valor de uma tarefa já premiada).
- `balanceAfter` é um snapshot do saldo pós-transação — evita ter que somar tudo toda vez que se precisa mostrar o saldo atual; o saldo "oficial" ainda pode ser recalculado a partir da soma de `points` como fonte da verdade.
- Cobre a regra de "toda mudança de conclusão gera reversão auditável" — nunca apaga histórico.

---

## 6. task_events

Log de auditoria de tudo que acontece com tarefas (não só pontos).

```json
{
  "_id": ObjectId,
  "userId": ObjectId,
  "dailyRoutineId": ObjectId | null,
  "taskId": "uuid | null",
  "taskTemplateId": ObjectId | null,
  "eventType": "created" | "updated" | "deleted" | "completed" | "reopened" | "reordered" | "points_proposed" | "points_adjusted",
  "payload": { "before": {}, "after": {} },
  "actorId": ObjectId,
  "actorRole": "child" | "adult",
  "createdAt": ISODate
}
```

**Índices:**
- `{ userId: 1, createdAt: -1 }`
- `{ taskTemplateId: 1 }`
- `{ dailyRoutineId: 1 }`

**Notas:**
- Registra exclusão de tarefa (mandatória por regra: "fica no histórico de eventos"), reordenação, e a origem de qualquer alteração — quem fez e quando.
- `payload.before`/`after` guarda o diff para telas de auditoria futuras.

---

## 7. store_items

```json
{
  "_id": ObjectId,
  "userId": ObjectId,
  "title": "1 hora de TV",
  "description": "string | null",
  "cost": 100,
  "category": "screen_time" | "toy" | "activity" | "other",
  "icon": "string | null",
  "active": "boolean",
  "stock": "int | null",
  "createdBy": ObjectId,
  "createdAt": ISODate,
  "updatedAt": ISODate
}
```

**Índices:**
- `{ userId: 1, active: 1 }`

**Notas:**
- `stock: null` = ilimitado (ex. "1 hora de TV"); número finito para itens físicos (ex. "Carrinho Hot Wheels", `stock: 1`).
- Exemplos da spec: Hot Wheels (300 PP), 1h de TV (100 PP).

---

## 8. redemptions

```json
{
  "_id": ObjectId,
  "userId": ObjectId,
  "storeItemId": ObjectId,
  "itemTitle": "Carrinho Hot Wheels",
  "cost": 300,
  "status": "pending" | "approved" | "rejected" | "delivered",
  "requestedBy": ObjectId,
  "reviewedBy": ObjectId | null,
  "requestedAt": ISODate,
  "reviewedAt": ISODate | null,
  "pointTransactionId": ObjectId | null
}
```

**Índices:**
- `{ userId: 1, status: 1 }`
- `{ storeItemId: 1 }`

**Notas:**
- Fluxo sugerido: criança solicita (`pending`) → adulto aprova/rejeita → se aprovado, gera a transação `redemption` em `point_transactions` e debita o saldo.
- Mantém coerência com o modelo de negociação de pontos (registrar quem decidiu, sem apagar nada).

---

## 9. habitats

```json
{
  "_id": ObjectId,
  "userId": ObjectId,
  "elements": {
    "water": "boolean",
    "plants": ["string"],
    "rocks": ["string"],
    "hidingSpots": ["string"],
    "bubbles": "boolean"
  },
  "bounds": { "width": "number", "height": "number" },
  "theme": "string | null",
  "createdAt": ISODate,
  "updatedAt": ISODate
}
```

**Índices:**
- `{ userId: 1 }` único

---

## 10. pacus_growth

Log histórico separado de `pacus` (que guarda só o estado atual) — útil para gráficos e para nunca perder o rastro de quando cada crescimento aconteceu, mesmo que `pacus.stage` seja sobrescrito.

```json
{
  "_id": ObjectId,
  "userId": ObjectId,
  "pacusId": ObjectId,
  "date": "2026-08-24",
  "dailyRoutineId": ObjectId,
  "stageBefore": "baby",
  "stageAfter": "baby",
  "sizeBefore": "number",
  "sizeAfter": "number",
  "createdAt": ISODate
}
```

**Índices:**
- `{ userId: 1, date: 1 }` único (garante a proteção contra crescimento duplicado a nível de banco, além do `lastGrowthDate`)
- `{ pacusId: 1, createdAt: -1 }`

---

## 11. settings

```json
{
  "_id": ObjectId,
  "userId": ObjectId,
  "pointToBrlRate": 0.05,
  "growthStages": [
    { "stage": "egg", "date": "2026-08-09" },
    { "stage": "cracking", "date": "2026-08-14" },
    { "stage": "hatching", "date": "2026-08-18" },
    { "stage": "baby", "date": "2026-08-22" },
    { "stage": "young", "date": "2026-08-27" },
    { "stage": "adult", "date": "2026-08-31" }
  ],
  "childPermissions": {
    "canCreateTasks": true,
    "canDeleteTasks": true,
    "canReorderTasks": true,
    "canSetPoints": true
  },
  "createdAt": ISODate,
  "updatedAt": ISODate
}
```

**Índices:**
- `{ userId: 1 }` único

**Notas:**
- `growthStages` configurável em vez de hardcoded no domínio — quando o ciclo atual terminar (Adulto em 31/08), dá para definir um novo calendário sem deploy.
- `childPermissions` documenta as decisões já fechadas (autonomia total sobre tarefas do dia) de forma auditável e ajustável pelo painel adulto.

---

## Resumo de relações

```
users (1) ──< pacus (1)
users (1) ──< daily_routines (N) ──[tasks embutidos]
users (1) ──< task_templates (N)
users (1) ──< point_transactions (N)
users (1) ──< task_events (N)
users (1) ──< store_items (N) ──< redemptions (N)
users (1) ──< habitats (1)
users (1) ──< pacus_growth (N)
users (1) ──< settings (1)
```

## Coleções da spec original não incluídas separadamente

- **history**: não é uma coleção própria — a tela de Histórico consulta `daily_routines` com `status: "closed"` diretamente, já que cada documento já é a fotografia completa do dia.
