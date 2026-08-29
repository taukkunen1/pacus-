using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pacus.Api.Auth;
using Pacus.Application.DTOs;
using Pacus.Application.Interfaces;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

// Exclusao de conta (LGPD, item B3 -- art. 18, VI). Restrito ao adulto: a exclusao
// apaga os dados de toda a familia (ambos os papeis), entao so quem tem a senha
// pode confirmar. Exige a senha atual no corpo da requisicao -- reautenticacao
// simples contra sessao esquecida aberta ou token vazado, dado que a operacao e
// irreversivel.
[ApiController]
[Authorize]
[RequireRole(UserRole.Adult)]
[Route("api/v1/account")]
public class AccountController : ControllerBase
{
    private readonly IAccountDeletionService _accountDeletionService;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUser;

    public AccountController(
        IAccountDeletionService accountDeletionService,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUser)
    {
        _accountDeletionService = accountDeletionService;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAccount([FromBody] AccountDeletionRequest request)
    {
        var user = await _userRepository.GetByIdAsync(_currentUser.UserId);

        if (user is null || user.PasswordHash is null
            || !_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            return Unauthorized(new { error = "Senha invalida." });
        }

        await _accountDeletionService.DeleteAccountAsync(_currentUser.FamilyId, _currentUser.UserId);

        return NoContent();
    }
}
