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

    // "Por que isso importa" (parentalidade autonomo-suportiva -- ver
    // docs/PROPOSITO.md e TaskTemplate.Reason). Copiado do template no momento da
    // geracao, igual Options/SelectedOption.
    public string? Reason { get; set; }

    public TaskType Type { get; set; }
    public TaskPeriod Period { get; set; }
    public int Order { get; set; }
    public int Points { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Pending;

    // Op-in de escolha real pra crianca (Teoria da Autodeterminacao -- ver
    // docs/PROPOSITO.md): 2-4 opcoes que a tarefa pode oferecer em vez de um unico
    // jeito fixo de cumprir. Copiado do TaskTemplate.Options no momento da geracao
    // (mesma imutabilidade do resto da tarefa), vazio quando a tarefa nao usa opcoes.
    public List<string> Options { get; set; } = new();

    // Qual das Options a crianca escolheu (deve ser um valor exatamente igual a um
    // item de Options). Null enquanto nao escolhida, ou quando Options esta vazio.
    public string? SelectedOption { get; set; }

    public DateTime? CompletedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string Origin { get; set; } = "template"; // template | child | adult
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Autonomia e planejamento (2026-09-10, ver docs/ESTADO_ATUAL.md). Copiado do
    // TaskTemplate no momento da geracao (mesma imutabilidade do resto da tarefa):
    // meta minima e facil de comecar, pensada pra tarefas que a crianca costuma
    // deixar pra tras (ex.: "minimo de 5 minutos" numa tarefa de leitura). Null =
    // tarefa sem meta minima definida (comportamento normal).
    public string? MinimumGoalLabel { get; set; }

    // Autodeclarado pela crianca ao iniciar ou concluir a tarefa (chip rapido,
    // "Como voce comecou?"): sozinha, com uma sugestao do proprio PACUS, ou com
    // lembrete de um adulto. Null enquanto nao informado -- nunca obrigatorio,
    // so um convite (ver DailyRoutineService.SetTaskInitiativeAsync).
    public TaskInitiativeLevel? Initiative { get; set; }

    // Autodeclarado pela crianca quando uma tarefa nao foi concluida (nunca usado
    // pra punir -- ver docs/ESTADO_ATUAL.md, "Nao utilizar punicao"). Null enquanto
    // nao perguntado/respondido.
    public TaskSkipReason? SkipReason { get; set; }

    // Texto livre opcional, so relevante quando SkipReason == Other.
    public string? SkipReasonNote { get; set; }
}
