using Pacus.Application.Exceptions;

namespace Pacus.Application.Services;

// Validacao compartilhada de titulo/descricao/pontos de tarefa (revisao de API,
// 2026-09-11, achado #6: nenhum DTO usa data annotations -- a validacao e toda
// manual nos services, mas antes disso ficava inconsistente na pratica: pontos
// tinham faixa checada, titulo/descricao nao tinham limite de tamanho nenhum, e
// TaskTemplateService (tarefas permanentes) nao validava nada disso, so
// DailyRoutineService (tarefas do dia). Centraliza aqui pra manter as duas
// frentes com a mesma regra.
public static class TaskValidation
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;

    public static void ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ValidationException("O titulo da tarefa e obrigatorio.");
        if (title.Length > MaxTitleLength)
            throw new ValidationException(
                $"O titulo da tarefa deve ter no maximo {MaxTitleLength} caracteres.");
    }

    public static void ValidateDescription(string? description)
    {
        if (description is not null && description.Length > MaxDescriptionLength)
            throw new ValidationException(
                $"A descricao da tarefa deve ter no maximo {MaxDescriptionLength} caracteres.");
    }

    // Mensagem mantida identica a regra historica (DailyRoutineService.ValidatePoints)
    // -- ver DailyTasksHttpIntegrationTests, que confere este texto literalmente.
    public static void ValidatePoints(int points)
    {
        if (points == 0 || points < -10 || points > 10)
            throw new ValidationException(
                "Cada tarefa deve valer entre 1 e 10 Pacus Points, ou entre -1 e -10 (penalidade). Zero nao e permitido.");
    }
}

