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

    private AuthResponse BuildResponse(Domain.Entities.User user, TimeSpan lifetime)
    {
        var (token, expiresAt) = _tokenService.GenerateToken(user, lifetime);
        return new AuthResponse(token, user.Id.ToString(), user.Role.ToString(), user.Name, expiresAt);
    }
}
