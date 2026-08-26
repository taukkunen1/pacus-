namespace Pacus.Application.Utils;

// Abstrai DateTime.UtcNow para permitir testes deterministicos do fechamento do dia
// (que e inteiramente guiado por "que dia e hoje").
public interface IClock
{
    DateTime UtcNow { get; }
}

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
