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
            [Homework] = new[] { "Abrir o caderno", "Ler a primeira questao", "Resolver a primeira questao", "Concluir a licao prevista" },
            [Handwriting] = new[] { "Separar o caderno e lapis", "Escrever a primeira linha", "Concluir a quantidade combinada" },
            [Bathing] = new[] { "Pegar a toalha", "Abrir a janela do banheiro", "Lavar o cabelo", "Tomar banho", "Tirar as roupas do chao", "Guardar a toalha" },
            [MoodBoard] = new[] { "Escolher o humor de hoje", "Escolher o desafio criativo", "Fazer o primeiro traco", "Transformar o desenho", "Dar um nome a criacao (se quiser)" },
        };

    private static readonly string[] MoodChallenges =
    {
        "Desafio: se seu humor fosse um monstro, como seria?",
        "Desafio: qual seria a previsao do tempo dentro da sua cabeca?",
        "Desafio: invente um planeta para o seu humor",
        "Desafio: se seu dia fosse um personagem de videogame, como seria?",
        "Desafio: desenhe um animal que represente seu dia",
        "Desafio: transforme seu humor em um veiculo",
        "Desafio: crie uma historia com tres desenhos sobre seu dia",
    };

    public static bool IsHairWashDay(string date)
    {
        var day = DateOnly.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        // Ancoragem na data local da rotina, nao no relogio UTC.
        return (day.DayNumber - HairWashAnchor.DayNumber) % 2 == 0;
    }

    public static List<string> StepsForDay(string? kind, List<string> steps, string date)
    {
        if (kind == MoodBoard)
        {
            var day = DateOnly.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var selected = new List<string>(steps);
            if (selected.SequenceEqual(DefaultSteps[MoodBoard]))
                selected[1] = MoodChallenges[(day.DayNumber % MoodChallenges.Length + MoodChallenges.Length) % MoodChallenges.Length];
            return selected;
        }
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
