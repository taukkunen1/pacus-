using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

// Copia independente e imutavel de uma tarefa dentro de um DailyRoutine.
// Alterar o TaskTemplate de origem nunca reescreve tarefas ja geradas.
public class DailyTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? TaskTemplateId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType Type { get; set; }
    public TaskPeriod Period { get; set; }
    public int Order { get; set; }
    public int Points { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Pending;
    public DateTime? CompletedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string Origin { get; set; } = "template"; // template | child | adult
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
