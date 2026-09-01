namespace Pacus.Domain.Entities;

// Uma variante de conteudo pra um dia especifico da semana dentro de um
// TaskTemplate com Recurrence == TaskTemplate.RecurrenceWeekdayRotation (ex.:
// "Momento Criativo" -- Detetive na segunda, Engenheiro na terca, etc). Type e
// Period continuam vindo do template (sao os mesmos todo dia); Title/Description
// sempre mudam por variante. Points e opcional: null usa o valor do template
// (mesma pontuacao todo dia, como era antes desta mudanca); preenchido, essa
// variante especifica vale diferente -- ex.: Chef por um Dia exige supervisao de
// adulto e vale mais que Missao 20 Minutos.
public class TaskTemplateVariant
{
    public DayOfWeek DayOfWeek { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Points { get; set; }
}
