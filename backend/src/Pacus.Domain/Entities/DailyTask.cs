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
}
