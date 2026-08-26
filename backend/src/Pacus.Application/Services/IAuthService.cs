using Pacus.Application.DTOs;

namespace Pacus.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> AdultLoginAsync(string email, string password);
    Task<AuthResponse> ChildLoginAsync(string userId, string pin);
}
