// Executar via mongosh: mongosh "$MONGODB_URI" apply-validators.js
//
// Melhoria de banco (revisao pos-auditoria de seguranca/LGPD): hoje a unica garantia de
// formato dos documentos e a camada C# (entidades tipadas) -- o Mongo aceita qualquer
// shape. Um script de correcao manual, uma migracao malfeita ou um bug numa service
// poderiam gravar um documento fora do formato esperado sem nenhum aviso. Este arquivo
// adiciona uma segunda barreira, no proprio banco, comecando por `users` (a collection
// mais critica: autenticacao e a chave de agrupamento familyId).
//
// validationAction: "warn" de proposito, nao "error" -- primeiro rodar por um tempo
// observando os logs do Mongo (nenhum warning esperado, ja que o schema abaixo reflete
// exatamente o que a aplicacao grava hoje), e so trocar para "error" depois de confirmar
// isso em producao. Trocar cedo demais pode derrubar uma escrita legitima se este schema
// tiver algum campo errado que eu nao tenha pego.
//
// validationLevel: "moderate" -- aplica em inserts e em updates de documentos que ja
// batem com o schema; nao trava updates em documentos antigos que porventura nao batam
// (evita quebrar dado historico que a validacao nao previu).
//
// Tipos conferidos direto no codigo (User.cs, UserRole.cs) antes de escrever isto:
// nao ha [BsonRepresentation(BsonType.String)] em nenhum enum deste projeto, entao
// `role` e gravado como inteiro (0 = Adult, 1 = Child), nao como string.

db = db.getSiblingDB("pacus");

db.runCommand({
  collMod: "users",
  validator: {
    $jsonSchema: {
      bsonType: "object",
      required: ["_id", "role", "name", "timezone", "familyId", "createdAt", "updatedAt"],
      properties: {
        _id: { bsonType: "objectId" },
        role: {
          bsonType: "int",
          enum: [0, 1],
          description: "0 = Adult, 1 = Child (UserRole.cs, sem representacao string)",
        },
        name: { bsonType: "string", minLength: 1 },
        email: {
          bsonType: ["string", "null"],
          description: "so para adulto -- null/ausente para crianca",
        },
        passwordHash: {
          bsonType: ["string", "null"],
          description: "so para adulto -- null/ausente para crianca",
        },
        pinHash: {
          bsonType: ["string", "null"],
          description: "so para crianca -- null/ausente para adulto",
        },
        timezone: { bsonType: "string", minLength: 1 },
        familyId: { bsonType: "objectId" },
        createdAt: { bsonType: "date" },
        updatedAt: { bsonType: "date" },
      },
    },
  },
  validationAction: "warn",
  validationLevel: "moderate",
});

print("Validador de 'users' aplicado (validationAction: warn -- so registra em log, nao bloqueia).");
