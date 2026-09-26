using System.Collections.Concurrent;

namespace Pacus.Application.Services;

// Bloqueio temporario por CONTA apos falhas seguidas de login/PIN/codigo de recuperacao.
// O rate limit por IP (Program.cs) nao basta sozinho: um atacante com varios IPs ainda
// poderia forcar bruta o PIN de 4 digitos de uma crianca especifica. Este controle limita
// as tentativas por alvo, independente do IP de origem.
// Estado em memoria (por instancia): suficiente enquanto a API roda em uma maquina; se
// escalar para varias, mover o contador para o MongoDB.
public interface ILoginAttemptTracker
{
    bool IsLockedOut(string key);
    void RegisterFailure(string key);
    void Reset(string key);
}

public sealed class LoginAttemptTracker : ILoginAttemptTracker
{
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private sealed class Entry
    {
        public int Failures;
        public DateTime LastFailureUtc;
    }

    private readonly ConcurrentDictionary<string, Entry> _entries = new();
    private readonly Func<DateTime> _utcNow;

    public LoginAttemptTracker() : this(() => DateTime.UtcNow) { }

    public LoginAttemptTracker(Func<DateTime> utcNow) => _utcNow = utcNow;

    public bool IsLockedOut(string key)
    {
        if (!_entries.TryGetValue(key, out var entry)) return false;

        lock (entry)
        {
            if (_utcNow() - entry.LastFailureUtc >= LockoutDuration)
            {
                _entries.TryRemove(key, out _);
                return false;
            }

            return entry.Failures >= MaxFailures;
        }
    }

    public void RegisterFailure(string key)
    {
        PurgeExpired();

        var entry = _entries.GetOrAdd(key, _ => new Entry());
        lock (entry)
        {
            var now = _utcNow();
            if (now - entry.LastFailureUtc >= LockoutDuration) entry.Failures = 0;
            entry.Failures++;
            entry.LastFailureUtc = now;
        }
    }

    public void Reset(string key) => _entries.TryRemove(key, out _);

    private void PurgeExpired()
    {
        if (_entries.Count < 1000) return;

        var now = _utcNow();
        foreach (var pair in _entries)
        {
            if (now - pair.Value.LastFailureUtc >= LockoutDuration)
                _entries.TryRemove(pair.Key, out _);
        }
    }
}
