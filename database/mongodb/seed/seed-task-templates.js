// Seed de exemplo com as tarefas da especificacao original.
// Ajustar userId antes de rodar: mongosh "$MONGODB_URI" seed-task-templates.js
db = db.getSiblingDB("pacus");

const userId = ObjectId("000000000000000000000000"); // TODO: substituir

db.task_templates.insertMany([
  { userId, title: "Escovar os dentes", type: "mandatory", period: "morning", points: 1, order: 1, active: true, recurrence: "daily", createdAt: new Date(), updatedAt: new Date() },
  { userId, title: "Tomar banho", type: "mandatory", period: "evening", points: 1, order: 2, active: true, recurrence: "daily", createdAt: new Date(), updatedAt: new Date() },
  { userId, title: "Arrumar a cama", type: "mandatory", period: "morning", points: 1, order: 3, active: true, recurrence: "daily", createdAt: new Date(), updatedAt: new Date() },
  { userId, title: "Guardar brinquedos", type: "expected", period: "evening", points: 2, order: 4, active: true, recurrence: "daily", createdAt: new Date(), updatedAt: new Date() },
  { userId, title: "Ler livro", type: "expected", period: "evening", points: 3, order: 5, active: true, recurrence: "daily", createdAt: new Date(), updatedAt: new Date() },
]);

print("Seed de task_templates concluido.");
