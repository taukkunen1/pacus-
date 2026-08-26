using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

// A "fotografia" de um dia. Congela apos o fechamento (status = Closed).
public class DailyRoutine
{
    public ObjectId Id { get; set; }
    public ObjectId UserId { get; set; }
    // Data operacional no timezone do usuario, formato YYYY-MM-DD.
    public string Date { get; set; } = string.Empty;
    public string Timezone { get; set; } = "America/Sao_Paulo";
    public RoutineStatus Status { get; set; } = RoutineStatus.Open;
    public List<DailyTask> Tasks { get; set; } = new();
    public DailyRoutineStats Stats { get; set; } = new();
    public int PointsEarned { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
