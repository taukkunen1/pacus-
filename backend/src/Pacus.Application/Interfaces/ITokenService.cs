using Pacus.Domain.Entities;

namespace Pacus.Application.Interfaces;

public interface ITokenService
{
    // Retorna o token junto com o exp real embutido nele — evita que o chamador calcule
    // uma expiracao "hipotetica" que pode divergir do que o token realmente carrega.
    (string Token, DateTime ExpiresAt) GenerateToken(User user, TimeSpan lifetime);
}
