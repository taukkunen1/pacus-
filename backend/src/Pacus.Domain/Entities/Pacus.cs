using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class Pacus
{
    public ObjectId Id { get; set; }
    public ObjectId FamilyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public PacusStage Stage { get; set; }
    public double Size { get; set; }
    public int TotalClosedDays { get; set; }

    // Matiz (0-359) que sobrescreve a cor derivada do id (ver
    // frontend/js/pacus/color.js getBirthHue) -- null significa "usar a cor
    // derivada automaticamente", o comportamento padrao desde sempre. So
    // existe pra o painel do adulto poder corrigir/escolher a cor manualmente
    // (ver Pacus.Api.Controllers.PacusController.UpdateState).
    public int? ColorHue { get; set; }
    public string? LastGrowthDate { get; set; }
    public List<PacusStageHistoryEntry> StageHistory { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
