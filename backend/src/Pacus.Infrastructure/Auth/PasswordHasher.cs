using System.Security.Cryptography;
using Pacus.Application.Interfaces;

namespace Pacus.Infrastructure.Auth;

// PBKDF2 via Rfc2898DeriveBytes — biblioteca padrao do .NET, sem dependencia externa.
// Formato armazenado: {iterations}.{saltBase64}.{hashBase64}
public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public string Hash(string plainText)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(plainText, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string hash, string plainText)
    {
        var parts = hash.Split('.', 3);
        if (parts.Length != 3) return false;

        if (!int.TryParse(parts[0], out var iterations)) return false;

        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);

        var actual = Rfc2898DeriveBytes.Pbkdf2(plainText, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

        // Comparacao em tempo constante — evita vazar informacao por timing attack.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
