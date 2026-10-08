using Pacus.Application.Exceptions;

namespace Pacus.Application.Services;

// V4: recursos opcionais para as tres missoes selecionadas pelo adulto.
// Nenhuma classificacao automatica por titulo e feita.
public static class TaskSupportConfiguration
{
    public const string Reading = "reading";
    public const string Homework = "homework";
    public const string Handwriting = "handwriting";
    public const string Bathing = "bathing";
    public const string MoodBoard = "mood_board";
    // 08/10/2026 e o primeiro dia de lavar o cabelo; 07/10 foi dia sem lavar.
    private static readonly DateOnly HairWashAnchor = new(2026, 10, 8);

    public static readonly IReadOnlyDictionary<string, string[]> DefaultSteps =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [Reading] = new[] { "Escolher o livro", "Ler o trecho combinado", "Contar algo sobre a leitura" },
            [Homework] = new[] { "Separar o material da escola ou do ingles", "Ver quais atividades foram pedidas", "Escolher por qual atividade comecar", "Fazer a primeira atividade (pode comecar com 10 minutos)", "Continuar a licao, fazer pausas e pedir ajuda se precisar", "Conferir o que foi feito e guardar o material" },
            [Handwriting] = new[] { "Separar o caderno e lapis", "Escrever a primeira linha", "Concluir a quantidade combinada" },
            [Bathing] = new[] { "Pegar a toalha", "Abrir a janela do banheiro", "Lavar o cabelo", "Tomar banho", "Tirar as roupas do chao", "Guardar a toalha" },
            [MoodBoard] = new[] { "Pensar em como estou me sentindo hoje", "Escolher uma cor ou um desenho que combine com esse sentimento", "Desenhar na lousa", "Olhar meu desenho e, se quiser, contar algo sobre ele" },
        };

    private static readonly string[] LegacyHomeworkSteps =
    {
        "Abrir o caderno", "Ler a primeira questao",
        "Resolver a primeira questao", "Concluir a licao prevista",
    };

    public static bool IsLegacyHomeworkSteps(IReadOnlyList<string> steps) =>
        steps.SequenceEqual(LegacyHomeworkSteps);

    public static bool IsHairWashDay(string date)
    {
        var day = DateOnly.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        // Ancoragem na data local da rotina, nao no relogio UTC.
        return (day.DayNumber - HairWashAnchor.DayNumber) % 2 == 0;
    }

    public static List<string> StepsForDay(string? kind, List<string> steps, string date)
    {
        if (kind == Homework && IsLegacyHomeworkSteps(steps))
            return new List<string>(DefaultSteps[Homework]);
        if (kind != Bathing) return new List<string>(steps);
        var selected = new List<string>(DefaultSteps[Bathing]);
        if (!IsHairWashDay(date)) selected.Remove("Lavar o cabelo");
        return selected;
    }

    public static (string? Kind, List<string> Steps) Parse(string? kind, List<string>? steps)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            if (steps is { Count: > 0 })
                throw new ValidationException("Etapas exigem uma missao de estudo habilitada.");
            return (null, new List<string>());
        }

        kind = kind.Trim().ToLowerInvariant();
        if (!DefaultSteps.TryGetValue(kind, out var defaults))
            throw new ValidationException("Tipo de missao invalido.");

        if (kind == Bathing && steps is { Count: > 0 } &&
            !steps.SequenceEqual(defaults))
            throw new ValidationException("O roteiro do banho e fixo para preservar a alternancia do cabelo.");

        var cleaned = steps is { Count: > 0 }
            ? steps.Select(s => s?.Trim() ?? "").ToList()
            : defaults.ToList();
        if (cleaned.Count < 2 || cleaned.Count > 6 || cleaned.Any(s => s.Length is < 1 or > 140))
            throw new ValidationException("A missao deve ter entre 2 e 6 etapas de ate 140 caracteres.");
        return (kind, cleaned);
    }
}
