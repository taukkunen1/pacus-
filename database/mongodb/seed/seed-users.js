// Cria o primeiro usuario adulto e o perfil da crianca, para poder logar pela primeira vez.
// Rodar: mongosh "$MONGODB_URI" seed-users.js
//
// IMPORTANTE: os hashes abaixo sao PLACEHOLDERS. O formato usado pelo backend e
// PBKDF2: "{iterations}.{saltBase64}.{hashBase64}" (ver Pacus.Infrastructure/Auth/PasswordHasher.cs).
// Gere hashes de verdade chamando IPasswordHasher.Hash(...) a partir de um endpoint
// administrativo de setup, ou de um script C# auxiliar — nao gere hashes compativeis em JS aqui.
db = db.getSiblingDB("pacus");

const familyId = ObjectId();
const adultId = ObjectId();
const childId = ObjectId();

db.users.insertMany([
  {
    _id: adultId,
    role: "adult",
    name: "Pedro",
    email: "pedro@example.com",
    passwordHash: "SUBSTITUIR_PELO_HASH_REAL",
    pinHash: null,
    timezone: "America/Sao_Paulo",
    familyId: familyId,
    createdAt: new Date(),
    updatedAt: new Date(),
  },
  {
    _id: childId,
    role: "child",
    name: "Hector",
    email: null,
    passwordHash: null,
    pinHash: "SUBSTITUIR_PELO_HASH_REAL",
    timezone: "America/Sao_Paulo",
    familyId: familyId,
    createdAt: new Date(),
    updatedAt: new Date(),
  },
]);

print("Usuarios criados. familyId:", familyId.toString(), "| childId (para login por PIN):", childId.toString());
