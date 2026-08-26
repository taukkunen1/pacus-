using MongoDB.Bson;

namespace Pacus.Application.Services;

// Implementa o algoritmo de fechamento do dia da especificacao:
// 1. determina data operacional no timezone do usuario
// 2. busca/gera a rotina do dia anterior se ainda aberta e vencida
// 3. idempotente: se ja fechada, nao reprocessa
// 4-6. marca fechada, registra estatisticas e pontos
// 7-9. verifica lastGrowthDate e cresce o PACUS uma unica vez
// 10-11. disponibiliza o novo dia com tarefas pendentes
public interface IDayClosingService
{
    Task CloseIfDueAsync(ObjectId userId, string timezone);
}
