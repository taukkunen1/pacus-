namespace Pacus.Application.DTOs;

public record CreateTaskRequest(
    string Title,
    string? Description,
    string Type,
    string Period,
    int Points
);

public record AdjustPointsRequest(int Points);
