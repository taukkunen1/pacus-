using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class User
{
    public ObjectId Id { get; set; }
    public UserRole Role { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public string? PinHash { get; set; }
    public string Timezone { get; set; } = "America/Sao_Paulo";
    public ObjectId FamilyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
