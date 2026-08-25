namespace Pacus.Application.DTOs;

public record DailyTaskUpdateRequest(
    string Title,
    string? Description,
    string Type,
    string Period,
    int Points
);
