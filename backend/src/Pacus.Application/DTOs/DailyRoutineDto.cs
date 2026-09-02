using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Application.DTOs;

// DTOs de resposta pra DailyRoutine/DailyTask (achado #3 da auditoria de API de
// 2026-09-01 -- ver docs/ESTADO_ATUAL.md): antes os controllers devolviam a entidade
// de dominio crua (Ok(routine)), o que significa que qualquer campo novo adicionado
// em DailyRoutine/DailyTask por conveniencia interna (ex.: os campos [BsonIgnore]
// calculados, ou um campo de auditoria futuro) vazava pra API sem ninguem decidir
// isso de proposito. Agora o shape da resposta e explicito aqui, e os extension
// methods ToResponse() abaixo sao o unico lugar que faz a conversao -- os
// controllers nao tocam mais nos campos da entidade diretamente.
//
// O shape em si (nomes de campo, quais campos aparecem) foi mantido igual ao que a
// serializacao direta da entidade ja produzia, pra nao quebrar o frontend nem os
// testes de integracao existentes que checam campos especificos (ex.: "origin" e
// "deletedAt" continuam aparecendo -- tarefas deletadas continuam na lista de Tasks
// com DeletedAt preenchido, o frontend que filtra `!task.deletedAt`; ver
// docs/ESTADO_ATUAL.md se decidirmos mudar esse contrato depois).

public record TaskTypeStatResponse(int Done, int Total);

public record DailyRoutineStatsResponse(
    TaskTypeStatResponse Mandatory,
    TaskTypeStatResponse Expected,
    TaskTypeStatResponse Challenge,
    int PointsEarned,
    double CompletionRate
);

public record DailyReactionResponse(
    string Icon,
    string? Message,
    string CreatedBy,
    DateTime CreatedAt
);

public record DailyTaskResponse(
    string Id,
    string? TaskTemplateId,
    string Title,
    string? Description,
    string? Reason,
    TaskType Type,
    TaskPeriod Period,
    int Order,
    int Points,
    TaskItemStatus Status,
    List<string> Options,
    string? SelectedOption,
    DateTime? CompletedAt,
    string CreatedBy,
    string Origin,
    DateTime? DeletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record DailyRoutineResponse(
    string Id,
    string FamilyId,
    string Date,
    string Timezone,
    RoutineStatus Status,
    List<DailyTaskResponse> Tasks,
    DailyRoutineStatsResponse Stats,
    int PointsEarned,
    DateTime? ClosedAt,
    DateTime CreatedAt,
    DateTime? GameTimerUnlockedAt,
    int GameTimerExtraMinutes,
    DateTime? GameTimerPausedAt,
    long GameTimerPausedMs,
    DailyReactionResponse? Reaction,
    bool GameTimerEnabled,
    int GameTimerMinutes
);

public static class DailyRoutineMappingExtensions
{
    public static DailyTaskResponse ToResponse(this DailyTask task) => new(
        task.Id,
        task.TaskTemplateId,
        task.Title,
        task.Description,
        task.Reason,
        task.Type,
        task.Period,
        task.Order,
        task.Points,
        task.Status,
        task.Options,
        task.SelectedOption,
        task.CompletedAt,
        task.CreatedBy,
        task.Origin,
        task.DeletedAt,
        task.CreatedAt,
        task.UpdatedAt);

    public static DailyRoutineResponse ToResponse(this DailyRoutine routine) => new(
        routine.Id.ToString(),
        routine.FamilyId.ToString(),
        routine.Date,
        routine.Timezone,
        routine.Status,
        routine.Tasks.Select(t => t.ToResponse()).ToList(),
        routine.Stats.ToResponse(),
        routine.PointsEarned,
        routine.ClosedAt,
        routine.CreatedAt,
        routine.GameTimerUnlockedAt,
        routine.GameTimerExtraMinutes,
        routine.GameTimerPausedAt,
        routine.GameTimerPausedMs,
        routine.Reaction?.ToResponse(),
        routine.GameTimerEnabled,
        routine.GameTimerMinutes);

    public static DailyRoutineStatsResponse ToResponse(this DailyRoutineStats stats) => new(
        new TaskTypeStatResponse(stats.Mandatory.Done, stats.Mandatory.Total),
        new TaskTypeStatResponse(stats.Expected.Done, stats.Expected.Total),
        new TaskTypeStatResponse(stats.Challenge.Done, stats.Challenge.Total),
        stats.PointsEarned,
        stats.CompletionRate);

    public static DailyReactionResponse ToResponse(this DailyReaction reaction) => new(
        reaction.Icon,
        reaction.Message,
        reaction.CreatedBy.ToString(),
        reaction.CreatedAt);
}
