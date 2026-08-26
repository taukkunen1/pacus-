namespace Pacus.Infrastructure.Auth;

// Populado via variavel de ambiente (JWT_SECRET) — nunca hardcoded, nunca commitado.
public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "pacus-api";
    public string Audience { get; set; } = "pacus-clients";
}
