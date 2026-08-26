namespace Pacus.Application.Utils;

// A rotina opera em "dias" no timezone do usuario, nao em UTC.
// O servidor guarda timestamps em UTC, mas toda decisao de "que dia e hoje" passa por aqui.
public static class TimezoneHelper
{
    // Retorna a data operacional (YYYY-MM-DD) de "agora" no timezone informado.
    public static string GetOperationalDate(string timezone, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var tz = ResolveTimeZone(timezone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(now, tz);
        return local.ToString("yyyy-MM-dd");
    }

    // Proximo dia em formato YYYY-MM-DD, dado um dia no mesmo formato.
    public static string NextDate(string date)
    {
        var parsed = DateTime.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return parsed.AddDays(1).ToString("yyyy-MM-dd");
    }

    // Compara duas datas YYYY-MM-DD como texto (funciona por serem zero-padded e ISO 8601).
    public static bool IsBefore(string date, string other) =>
        string.Compare(date, other, StringComparison.Ordinal) < 0;

    private static TimeZoneInfo ResolveTimeZone(string timezone)
    {
        try
        {
            // IDs IANA (ex. "America/Sao_Paulo") funcionam nativamente no .NET em Linux/macOS.
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
