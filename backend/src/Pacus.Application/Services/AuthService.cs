using MongoDB.Bson;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    // Expiracao do token — curta para adulto (sessao administrativa), mais longa para
    // a crianca (evita pedir PIN toda hora num tablet compartilhado da familia).
    private static readonly TimeSpan AdultTokenLifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan ChildTokenLifetime = TimeSpan.FromDays(7);

    public AuthService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse> AdultLoginAsync(string email, string password)
    {
        var user = await _userRepository.GetByEmailAsync(email);

        // Mensagem generica de proposito — nao revela se o email existe ou se a senha e que esta errada.
        if (user is null || user.Role != UserRole.Adult || user.PasswordHash is null
            || !_passwordHasher.Verify(user.PasswordHash, password))
        {
            throw new UnauthorizedAccessException("Email ou senha invalidos.");
        }

        return BuildResponse(user, AdultTokenLifetime);
    }

    public async Task<AuthResponse> ChildLoginAsync(string userId, string pin)
    {
        if (!ObjectId.TryParse(userId, out var parsedId))
            throw new UnauthorizedAccessException("Perfil ou PIN invalidos.");

        var user = await _userRepository.GetByIdAsync(parsedId);

        if (user is null || user.Role != UserRole.Child || user.PinHash is null
            || !_passwordHasher.Verify(user.PinHash, pin))
        {
            throw new UnauthorizedAccessException("Perfil ou PIN invalidos.");
        }

        return BuildResponse(user, ChildTokenLifetime);
    }

    public async Task<string> ResetAdultPasswordAsync(string email, string recoveryCode, string newPassword)
    {
        var user = await _userRepository.GetByEmailAsync(email.Trim().ToLowerInvariant());

        // Mensagem generica, mesmo padrao do login -- nao revela se o email existe ou
        // se so o codigo esta errado.
        if (user is null || user.Role != UserRole.Adult || user.RecoveryCodeHash is null
            || !_passwordHasher.Verify(user.RecoveryCodeHash, recoveryCode.Trim().ToUpperInvariant()))
        {
            throw new UnauthorizedAccessException("Email ou codigo de recuperacao invalidos.");
        }

        user.PasswordHash = _passwordHasher.Hash(newPassword);

        // Uso unico: gera e grava um codigo novo, o antigo nunca mais funciona --
        // mesmo raciocinio dos codigos de backup de 2FA.
        var newRecoveryCode = GenerateRecoveryCode();
        user.RecoveryCodeHash = _passwordHasher.Hash(newRecoveryCode);
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user);

        return newRecoveryCode;
    }

    // 10 caracteres em base32-like (sem 0/O/1/I, que se confundem visualmente) -- pensado
    // pra ser anotado/guardado por uma pessoa, tipo "K7QX-9F3M-2Z". Usado tambem pelo
    // BootstrapService ao criar a familia.
    public static string GenerateRecoveryCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        Span<char> buffer = stackalloc char[10];
        for (var i = 0; i < buffer.Length; i++)
            buffer[i] = alphabet[Random.Shared.Next(alphabet.Length)];

        return new string(buffer);
    }

    private AuthResponse BuildResponse(Domain.Entities.User user, TimeSpan lifetime)
    {
        var (token, expiresAt) = _tokenService.GenerateToken(user, lifetime);
        return new AuthResponse(token, user.Id.ToString(), user.Role.ToString(), user.Name, expiresAt);
    }
}
