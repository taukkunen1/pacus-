using Pacus.Application.Exceptions;

namespace Pacus.Application.Services;

// V4: recursos opcionais para as tres missoes selecionadas pelo adulto.
// Nenhuma classificacao automatica por titulo e feita.
public static class TaskSupportConfiguration
{
    public const string Reading = "reading";
    public const string Homework = "homework";
    public const string Handwriting = "handwriting";

    public static readonly IReadOnlyDictionary<string, string[]> DefaultSteps =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [Reading] = new[] { "Escolher o livro", "Ler o trecho combinado", "Contar algo sobre a leitura" },
            [Homework] = new[] { "Abrir o caderno", "Ler a primeira questao", "Resolver a primeira questao", "Concluir a licao prevista" },
            [Handwriting] = new[] { "Separar o caderno e lapis", "Escrever a primeira linha", "Concluir a quantidade combinada" },
        };

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

        var cleaned = steps is { Count: > 0 }
            ? steps.Select(s => s?.Trim() ?? "").ToList()
            : defaults.ToList();
        if (cleaned.Count < 2 || cleaned.Count > 6 || cleaned.Any(s => s.Length is < 1 or > 140))
            throw new ValidationException("A missao deve ter entre 2 e 6 etapas de ate 140 caracteres.");
        return (kind, cleaned);
    }
}
