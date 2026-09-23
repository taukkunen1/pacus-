namespace Pacus.Api.Security;

public static class CorsOriginPolicy
{
    public const string OfficialOrigin = "https://www.pacus.com.br";
    public const string DevelopmentFallbackOrigin = "http://localhost:5500";

    public static string[] Resolve(
        bool isDevelopment,
        string? configuredOrigins)
    {
        // Em producao, o dominio oficial e a unica origem web aceita.
        // Isso evita que configuracoes antigas no provedor de hospedagem
        // reabram CORS para frontends aposentados.
        if (!isDevelopment)
        {
            return [OfficialOrigin];
        }

        var source = string.IsNullOrWhiteSpace(configuredOrigins)
            ? DevelopmentFallbackOrigin
            : configuredOrigins;

        return source
            .Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
