namespace Pacus.Application.DTOs;

public record MemberRoutineUpdateRequest(
    string Title,
    string? Description,
    string Period,
    string? Reason = null
);

public record RoutineSuggestionResponse(
    string Id,
    string Kind,
    string Title,
    string Message,
    string? TaskTemplateId = null,
    string? SuggestedPeriod = null,
    string? SuggestedTitle = null
);
