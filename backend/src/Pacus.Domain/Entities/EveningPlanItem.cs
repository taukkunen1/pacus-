namespace Pacus.Domain.Entities;

// Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md): um item do
// "combinado" que a propria crianca monta no inicio da tarde/noite, respondendo
// "como voce quer organizar sua noite?". Nao e uma trava nem uma nova sequencia
// obrigatoria -- so o compromisso que ela mesma escolheu, mostrado de volta pra
// ela (ex.: "Depois do banho -> vou ler por 5 minutos"). TaskId aponta pra um
// DailyTask da rotina do dia; ApproxLabel e o texto livre e opcional que a
// crianca associa aquele momento (ex.: "depois do banho"), sem hora exata.
public class EveningPlanItem
{
    public string TaskId { get; set; } = string.Empty;
    public string? ApproxLabel { get; set; }
    public int Order { get; set; }
}

