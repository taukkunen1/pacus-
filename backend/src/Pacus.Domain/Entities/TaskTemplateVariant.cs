namespace Pacus.Domain.Entities;

// Uma variante de conteudo pra um dia especifico da semana dentro de um
// TaskTemplate com Recurrence == TaskTemplate.RecurrenceWeekdayRotation (ex.:
// "Momento Criativo" -- Detetive na segunda, Engenheiro na terca, etc). Type,
// Period e Points continuam vindo do template (sao os mesmos todo dia); so
// Title/Description mudam por variante.
public class TaskTemplateVariant
{
    public DayOfWeek DayOfWeek { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}
