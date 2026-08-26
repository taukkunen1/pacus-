namespace Pacus.Application.DTOs;

public record TaskDto(
    string Id,
    string Title,
    string Type,
    string Period,
    int Order,
    int Points,
    string Status,
    DateTime? CompletedAt
);

public record DailyRoutineDto(
    string Date,
    string Status,
    List<TaskDto> Tasks,
    int PointsEarned,
    double CompletionRate
);
