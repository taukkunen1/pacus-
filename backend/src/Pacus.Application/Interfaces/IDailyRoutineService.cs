using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

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

    // Vinculo (relatedness -- ver docs/PROPOSITO.md e DailyReaction). Restrito a adulto;
    // um por dia (reagir de novo substitui a reacao anterior, nao acumula).
    Task<DailyRoutine> SetReactionAsync(ObjectId userId, string icon, string? message, ObjectId actorId, string actorRole);

    // Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md).

    // A crianca monta o "combinado" da tarde/noite: ordem e/ou momento aproximado
    // das tarefas restantes. Substitui o plano anterior do dia inteiro (nao da pra
    // acumular planos parciais); items vazio limpa o plano.
    Task<DailyRoutine> SetEveningPlanAsync(ObjectId userId, List<EveningPlanItemRequest> items, ObjectId actorId, string actorRole);

    // Autodeclaracao de como a tarefa foi comecada -- ver TaskInitiativeLevel. Concede
    // um pequeno bonus de pontos quando a iniciativa foi da propria crianca ou veio de
    // uma sugestao do app (nunca quando precisou de lembrete de adulto -- ver
    // DailyRoutineService.InitiativeBonusPoints). Pode ser chamado antes ou depois de
    // concluir a tarefa.
    Task<DailyRoutine> SetTaskInitiativeAsync(ObjectId userId, string taskId, TaskInitiativeLevel initiative, ObjectId actorId, string actorRole);

    // Autodeclaracao de por que uma tarefa nao foi feita -- nunca afeta pontos nem
    // gera nenhuma penalidade (ver docs/ESTADO_ATUAL.md, "Nao utilizar punicao").
    Task<DailyRoutine> SetTaskSkipReasonAsync(ObjectId userId, string taskId, TaskSkipReason reason, string? note, ObjectId actorId, string actorRole);
}
