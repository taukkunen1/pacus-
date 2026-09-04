// Executar via mongosh: mongosh "$MONGODB_URI" create-indexes.js
db = db.getSiblingDB("pacus");

db.users.createIndex({ email: 1 }, { unique: true, partialFilterExpression: { email: { $type: "string" } } });
db.users.createIndex({ familyId: 1 });
db.users.createIndex({ familyCode: 1 });

db.pacus.createIndex({ userId: 1 }, { unique: true });

db.task_templates.createIndex({ userId: 1, active: 1 });

db.daily_routines.createIndex({ userId: 1, date: 1 }, { unique: true });
db.daily_routines.createIndex({ userId: 1, status: 1 });

// Melhoria de banco (revisao pos-auditoria): a invariante "no maximo uma rotina
// com status Open por familia" so era garantida pela aplicacao (DayClosingService),
// nunca pelo banco -- ver comentario em IDailyRoutineRepository.cs. Este indice unico
// parcial faz o proprio Mongo recusar uma segunda rotina Open da mesma familia, em vez
// de confiar so na logica do servico. status: 0 = RoutineStatus.Open (enum gravado como
// int, sem [BsonRepresentation(BsonType.String)] -- ver RoutineStatus.cs).
db.daily_routines.createIndex(
  { userId: 1, status: 1 },
  {
    unique: true,
    partialFilterExpression: { status: 0 },
    name: "one_open_routine_per_family",
  }
);

db.point_transactions.createIndex({ userId: 1, createdAt: -1 });
db.point_transactions.createIndex({ userId: 1, date: 1 });
db.point_transactions.createIndex({ dailyRoutineId: 1 });

db.task_events.createIndex({ userId: 1, createdAt: -1 });
db.task_events.createIndex({ taskTemplateId: 1 });
db.task_events.createIndex({ dailyRoutineId: 1 });

db.store_items.createIndex({ userId: 1, active: 1 });

db.redemptions.createIndex({ userId: 1, status: 1 });
db.redemptions.createIndex({ storeItemId: 1 });

db.habitats.createIndex({ userId: 1 }, { unique: true });

db.pacus_growth.createIndex({ userId: 1, date: 1 }, { unique: true });
db.pacus_growth.createIndex({ pacusId: 1, createdAt: -1 });

db.settings.createIndex({ userId: 1 }, { unique: true });

db.audit_logs.createIndex({ familyId: 1, createdAt: -1 });

// Exclusao de conta (LGPD, item B3): logs de auditoria anonimizados sao apagados
// automaticamente quando purgeAt e alcancado (expireAfterSeconds: 0 = expira no
// proprio valor do campo). Logs nao anonimizados nao tem purgeAt (null), entao
// nunca expiram por este indice.
db.audit_logs.createIndex({ purgeAt: 1 }, { expireAfterSeconds: 0 });

print("Indices criados com sucesso.");
