namespace Pacus.Application.DTOs;

public record CreateRoutineChangeProposalRequest(
    string TaskTemplateId,
    string Action,
    string? ProposedTitle = null,
    string? ProposedDescription = null,
    string? ProposedPeriod = null,
    string? Reason = null
);

public record ReviewRoutineChangeProposalRequest(string? Note = null);

public record RoutineSuggestionResponse(
    string Id,
    string Kind,
    string Title,
    string Message,
    string? TaskTemplateId = null,
    string? ProposedPeriod = null,
    string? SuggestedTitle = null
);
