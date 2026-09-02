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

    // Motivo por tras da tarefa, mostrado pra crianca (nao so "como fazer", como
    // Description, mas "por que isso importa") -- parentalidade autonomo-suportiva
    // (Joussemet, Landry & Koestner 2008; ver docs/PROPOSITO.md): dar a razao por
    // tras da regra, nao so a regra, muda como a crianca internaliza a
    // responsabilidade.
    //
    // Legado (pre-2026-09-02): um unico texto fixo, copiado sempre igual pra cada
    // DailyTask gerado -- a mesma frase todo santo dia. Mantido so pra nao quebrar
    // leitura de documentos antigos do Mongo que nunca foram regravados; nao e mais
    // escrito por TaskTemplateService (ver Reasons abaixo e EffectiveReasons).
    public string? Reason { get; set; }

    // Pool de frases pertinentes pra essa tarefa -- fonte de verdade atual do "por
    // que importa" (substitui Reason acima). DailyRoutineService sorteia UMA destas
    // a cada DailyTask gerado (ver PickReason), pra nao repetir sempre a mesma frase
    // todo dia mantendo a explicacao sempre relevante pra tarefa. Vazio = sem motivo
    // explicito (nao bloqueia nada, so deixa de aparecer o card). Cada tarefa pode
    // ter 1 a 8 frases; uma unica frase tambem e valida (comportamento igual ao
    // Reason legado, so que guardado na lista nova).
    public List<string> Reasons { get; set; } = new();

    // Le Reasons quando presente; cai pro Reason legado (como lista de 1 item)
    // quando o documento e antigo e nunca foi regravado desde esta mudanca. Nao
    // serializado -- e so uma leitura conveniente, gravar sempre vai por Reasons.
    [BsonIgnore]
    public List<string> EffectiveReasons =>
        Reasons.Count > 0
            ? Reasons
            : (string.IsNullOrEmpty(Reason) ? new List<string>() : new List<string> { Reason });

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
    // "Dia sim, dia nao" (ou qualquer intervalo de N dias) ancorado numa data
    // especifica -- diferente de RecurrenceCustom, que e sempre os MESMOS dias da
    // semana toda semana. Um intervalo de 2 dias desliza pelos dias da semana com
    // o tempo (ex.: ancorado numa quarta, cai em quarta/sexta/domingo/terca/
    // quinta/sabado/segunda/quarta/... -- nunca duas semanas seguidas iguais).
    // Ver AnchorDate/IntervalDays abaixo e ResolveTemplateForDay em
    // DailyRoutineService.
    public const string RecurrenceInterval = "interval";

    public string Recurrence { get; set; } = RecurrenceDaily;

    // So usado quando Recurrence == RecurrenceWeekdayRotation.
    public List<TaskTemplateVariant> Variants { get; set; } = new();

    // So usado quando Recurrence == RecurrenceCustom.
    public List<DayOfWeek> CustomDays { get; set; } = new();

    // So usados quando Recurrence == RecurrenceInterval. AnchorDate e a primeira
    // data em que a tarefa aparece (formato "yyyy-MM-dd", mesmo formato de
    // DailyRoutine.Date); IntervalDays e de quantos em quantos dias ela repete
    // a partir dali (2 = dia sim dia nao). Datas antes de AnchorDate nunca
    // incluem a tarefa, mesmo que a diferenca de dias "batesse" matematicamente.
    public string? AnchorDate { get; set; }
    public int IntervalDays { get; set; } = 2;

    public ObjectId CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
