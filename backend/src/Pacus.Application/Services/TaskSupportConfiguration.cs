using Pacus.Application.Exceptions;

namespace Pacus.Application.Services;

// Missoes opcionais de apoio, habilitadas explicitamente pelo adulto.
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
            [MoodBoard] = new[] { "Escolher o que quero desenhar: meu humor ou uma ideia livre", "Escolher o desafio criativo do dia (ou minha propria ideia)", "Fazer o primeiro traco na lousa", "Criar meu desenho do meu jeito", "Olhar minha criacao e concluir (dar nome ou contar e opcional)" },
        };

    // Templates criados antes desta versao continuam validos e serao atualizados
    // somente nas novas rotinas ou pela acao explicita do adulto na rotina de hoje.
    private static readonly string[] LegacyMoodSteps =
    {
        "Pensar em como estou me sentindo hoje",
        "Escolher uma cor ou um desenho que combine com esse sentimento",
        "Desenhar na lousa",
        "Olhar meu desenho e, se quiser, contar algo sobre ele",
    };

    // Sugestoes abertas e ludicas. Nenhuma exige revelar sentimentos ou
    // demonstrar qualidade artistica; a crianca sempre pode escolher sua ideia.
    private static readonly string[] MoodChallenges =
    {
        "invente um monstro gentil",
        "imagine a previsao do tempo de um planeta",
        "crie um planeta que ninguem visitou",
        "invente um personagem com um poder diferente",
        "crie um animal com formas geometricas",
        "desenhe um veiculo que ainda nao existe",
        "conte uma historia com tres desenhos",
        "transforme um rabisco em uma criatura",
        "invente uma casa em uma arvore gigante",
        "crie um robo que ajuda alguem",
        "desenhe uma ilha com uma regra engracada",
        "transforme uma fruta em personagem",
        "crie um mapa de um lugar imaginario",
        "junte dois animais e invente um terceiro",
        "invente algo para um dia de chuva",
        "desenhe uma maquina que cria sorrisos",
        "invente um jardim em outro planeta",
        "desenhe uma escola voadora",
        "crie um personagem com um unico circulo",
        "imagine o que existe no fundo de um oceano novo",
        "desenhe algo pequeno que ficou gigante",
    };

    public static bool IsLegacyMoodSteps(IReadOnlyList<string> steps) =>
        steps.SequenceEqual(LegacyMoodSteps);

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
            // Rotina nao sobrescreve etapas personalizadas pelo adulto.
            if (!steps.SequenceEqual(DefaultSteps[MoodBoard]) && !IsLegacyMoodSteps(steps))
                return new List<string>(steps);

            var day = DateOnly.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var selected = new List<string>(DefaultSteps[MoodBoard]);
            var suggestion = MoodChallenges[day.DayNumber % MoodChallenges.Length];
            selected[1] = $"Desafio de hoje: {suggestion} (ou escolha sua propria ideia)";
            return selected;
        }

        if (kind != Bathing) return new List<string>(steps);
        var bathSteps = new List<string>(DefaultSteps[Bathing]);
        if (!IsHairWashDay(date)) bathSteps.Remove("Lavar o cabelo");
        return bathSteps;
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
