using MongoDB.Bson;

namespace Pacus.Domain.Entities;

public class RoutineChangeProposal
{
    public ObjectId Id { get; set; }
    public ObjectId FamilyId { get; set; }
    public ObjectId RequestedBy { get; set; }
    public ObjectId TaskTemplateId { get; set; }

    // update | remove
    public string Action { get; set; } = "update";

    // Snapshot para o adulto entender o que esta sendo proposto.
    public string CurrentTitle { get; set; } = string.Empty;
    public string? CurrentDescription { get; set; }
    public string CurrentPeriod { get; set; } = string.Empty;

    public string? ProposedTitle { get; set; }
    public string? ProposedDescription { get; set; }
    public string? ProposedPeriod { get; set; }
    public string? MemberReason { get; set; }

    // pending | approved | rejected
    public string Status { get; set; } = "pending";
    public ObjectId? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
