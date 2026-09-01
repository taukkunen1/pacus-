using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class TaskTemplate
{
    public ObjectId Id { get; set; }
    // Renomeado de UserId -> FamilyId (checklist de seguranca, item A4): o valor sempre
    // foi o id da familia, nunca de um usuario individual. BsonElement("userId") preserva
    // o nome do campo ja gravado no Mongo (convencao camelCase), sem precisar de migracao.
    [BsonElement("userId")]
    public ObjectId FamilyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskType Type { get; set; }
    public TaskPeriod Period { get; set; }
    public int Points { get; set; }
    public int Order { get; set; }
    public bool Active { get; set; } = true;

    // Op-in de escolha real pra crianca (Teoria da Autodeterminacao -- ver
    // docs/PROPOSITO.md): 2-4 opcoes que a crianca escolhe entre si antes de concluir
    // a tarefa (ex.: "Escolha UMA destas: torre de copos / ponte de papel / abrigo").
    // Vazio = tarefa sem opcoes, comportamento normal. Copiado como esta para cada
    // DailyTask gerado (DailyTask.Options) -- nao muda quando o template muda depois.
    public List<string> Options { get; set; } = new();

    // Ate esta mudanca este campo existia no banco (toda tarefa permanente sempre
    // gravou "daily" aqui) mas nunca era lido em lugar nenhum -- a materializacao
    // diaria (DailyRoutineService) sempre criava a tarefa em TODOS os dias,
    // ignorando o valor. Corrigido: DailyRoutineService.ResolveDailyTaskForDate
    // agora usa este campo pra decidir se/como a tarefa aparece em cada dia.
    public const string RecurrenceDaily = "daily";
    public const string RecurrenceWeekday = "weekday"; // so segunda a sexta, mesmo conteudo
    public const string RecurrenceWeekend = "weekend"; // so sabado e domingo
    // So segunda a sexta, com titulo/descricao diferentes por dia (ver Variants).
    // Dia sem variante correspondente = tarefa nao aparece naquele dia.
    public const string RecurrenceWeekdayRotation = "weekday_rotation";
    // Qualquer combinacao de dias da semana escolhida na criacao (ver CustomDays),
    // mesmo conteudo em todos eles -- ex.: "Ingles" so terca e quarta, "Escoteiro"
    // so sabado. RecurrenceWeekday/RecurrenceWeekend sao atalhos pros dois casos
    // mais comuns; este cobre qualquer outra combinacao.
    public const string RecurrenceCustom = "custom";

    public string Recurrence { get; set; } = RecurrenceDaily;

    // So usado quando Recurrence == RecurrenceWeekdayRotation.
    public List<TaskTemplateVariant> Variants { get; set; } = new();

    // So usado quando Recurrence == RecurrenceCustom.
    public List<DayOfWeek> CustomDays { get; set; } = new();

    public ObjectId CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
