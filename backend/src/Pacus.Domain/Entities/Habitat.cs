using MongoDB.Bson;

namespace Pacus.Domain.Entities;

public class HabitatElements
{
    public bool Water { get; set; } = true;
    public List<string> Plants { get; set; } = new();
    public List<string> Rocks { get; set; } = new();
    public List<string> HidingSpots { get; set; } = new();
    public bool Bubbles { get; set; } = true;
}

public class HabitatBounds
{
    public double Width { get; set; }
    public double Height { get; set; }
}

public class Habitat
{
    public ObjectId Id { get; set; }
    public ObjectId FamilyId { get; set; }
    public HabitatElements Elements { get; set; } = new();
    public HabitatBounds Bounds { get; set; } = new();
    public string? Theme { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
