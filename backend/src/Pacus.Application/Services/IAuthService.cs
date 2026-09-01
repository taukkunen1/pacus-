using Pacus.Application.DTOs;

namespace Pacus.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> AdultLoginAsync(string email, string password);
    Task<AuthResponse> ChildLoginAsync(string userId, string pin);

    // "Esqueci minha senha" sem e-mail: valida o recovery code gerado no bootstrap,
    // define a nova senha e devolve um recovery code novo (o antigo e invalidado --
    // uso unico, como um codigo de backup de 2FA). Lanca UnauthorizedAccessException
    // se email/codigo nao baterem.
    Task<string> ResetAdultPasswordAsync(string email, string recoveryCode, string newPassword);
}
