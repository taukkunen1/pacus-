namespace Pacus.Domain.Entities;

public class TaskTypeStat
{
    public int Done { get; set; }
    public int Total { get; set; }
}

public class DailyRoutineStats
{
    public TaskTypeStat Mandatory { get; set; } = new();
    public TaskTypeStat Expected { get; set; } = new();
    public TaskTypeStat Challenge { get; set; } = new();
    public int PointsEarned { get; set; }
    public double CompletionRate { get; set; }
}
