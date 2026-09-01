using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Pacus.Application.DTOs;
using Pacus.Application.Services;

namespace Pacus.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    // Os try/catch abaixo continuam de proposito (ver Pacus.Api.Middleware.
    // AppExceptionHandler pro tratamento padrao do resto da API, achado #1 da
    // auditoria de API de 2026-09-01): aqui o UnauthorizedAccessException do
    // AuthService precisa virar 401 (nao autenticado -- credencial errada), nao o
    // 403 (autenticado mas sem permissao) que o handler global usa por padrao pro
    // resto dos controllers. Ambos os status sao "acesso negado" em ingles comum,
    // mas o significado HTTP e diferente, entao esta e a excecao a regra.
    [AllowAnonymous]
    [HttpPost("adult/login")]
    public async Task<IActionResult> AdultLogin([FromBody] AdultLoginRequest request)
    {
        try
        {
            var result = await _authService.AdultLoginAsync(request.Email, request.Password);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("child/login")]
    public async Task<IActionResult> ChildLogin([FromBody] ChildLoginRequest request)
    {
        try
        {
            var result = await _authService.ChildLoginAsync(request.UserId, request.Pin);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    // "Esqueci minha senha" do adulto -- sem provedor de e-mail configurado neste projeto,
    // usa o recovery code mostrado uma unica vez no bootstrap (ver BootstrapService).
    [AllowAnonymous]
    [HttpPost("adult/reset-password")]
    public async Task<IActionResult> ResetAdultPassword([FromBody] ResetAdultPasswordRequest request)
    {
        try
        {
            var newRecoveryCode = await _authService.ResetAdultPasswordAsync(
                request.Email, request.RecoveryCode, request.NewPassword);

            return Ok(new ResetAdultPasswordResponse(
                "Senha redefinida com sucesso. Guarde o novo codigo de recuperacao.",
                newRecoveryCode));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }
}
