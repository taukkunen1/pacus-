using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface IDailyRoutineService
{
    // Garante que a rotina do dia existe; cria a partir dos task_templates ativos se necessario.
    Task<DailyRoutine> GetOrCreateTodayAsync(ObjectId userId, string timezone);

    // Cria a rotina de uma data especifica (nao necessariamente "hoje") a partir dos
    // task_templates ativos. Usado tanto pelo GetOrCreateTodayAsync quanto pelo
    // fechamento do dia ao avancar por dias em que o usuario nao abriu o app.
    Task<DailyRoutine> CreateRoutineForDateAsync(ObjectId userId, string date, string timezone);

    // Toggle de conclusao: gera evento + transacao de pontos (award ou reversal).
    // Pode ser chamado a qualquer momento, mesmo depois de ja concluida — sem restricao de janela de tempo.
    Task<DailyRoutine> ToggleTaskAsync(ObjectId userId, string taskId, bool completed, ObjectId actorId, string actorRole);

    // Escolhe (ou limpa, se selectedOption for null) qual das Options da tarefa a
    // crianca vai seguir. Nao trava conclusao -- so registra a escolha, se houver.
    Task<DailyRoutine> SelectTaskOptionAsync(ObjectId userId, string taskId, string? selectedOption, ObjectId actorId, string actorRole);

    // Cria uma tarefa nova so para o dia atual — autonomia da crianca (ou do adulto) sobre a
    // rotina de hoje. Por baixo, tambem cria um TaskTemplate inativo com os mesmos dados:
    // isso garante que a tarefa SEMPRE tem um caminho pronto para ser replicada em outro dia
    // (o adulto so precisa ativar o template — ver ITaskTemplateService/PromoteToPermanentAsync),
    // mesmo que por padrao ela suma ao virar o dia, como a spec pede.
    Task<DailyRoutine> CreateAdHocTaskAsync(ObjectId userId, CreateTaskRequest request, ObjectId actorId, string actorRole);

    // Reordena as tarefas da rotina ABERTA (hoje). A mudanca fica registrada somente
    // naquele dia — historico anterior nao muda (regra da spec). orderedTaskIds deve
    // conter todos os ids da rotina atual; a ordem da lista vira o novo campo Order.
    Task<DailyRoutine> ReorderTasksAsync(ObjectId userId, List<string> orderedTaskIds, ObjectId actorId, string actorRole);

    // Ajusta os pontos de uma tarefa do dia atual (ex. adulto revendo o valor que a
    // crianca propos). Se a tarefa ja estava concluida, gera uma transacao Adjustment
    // com o delta — nunca edita silenciosamente um award ja registrado.
    Task<DailyRoutine> AdjustTaskPointsAsync(ObjectId userId, string taskId, int newPoints, ObjectId actorId, string actorRole);
    Task<DailyRoutine> UpdateTaskAsync(ObjectId userId, string taskId, DailyTaskUpdateRequest request, ObjectId actorId, string actorRole);
    Task<DailyRoutine> DeleteTaskAsync(ObjectId userId, string taskId, ObjectId actorId, string actorRole);

    // Pausa/despausa o game timer do dia atual — qualquer papel pode chamar
    // (adulto ou crianca). No-op se ja estiver no estado pedido, ou se o
    // timer nunca foi liberado (GameTimerUnlockedAt null).
    Task<DailyRoutine> PauseGameTimerAsync(ObjectId userId, ObjectId actorId, string actorRole);
    Task<DailyRoutine> ResumeGameTimerAsync(ObjectId userId, ObjectId actorId, string actorRole);

    // Ajusta o tempo total (+1h/-1h etc) — restrito a adulto; o controller
    // ja aplica [RequireRole(Adult)], mas o service tambem confere por
    // seguranca (nunca confiar so no frontend/controller).
    Task<DailyRoutine> AdjustGameTimerAsync(ObjectId userId, int deltaMinutes, ObjectId actorId, string actorRole);
}
