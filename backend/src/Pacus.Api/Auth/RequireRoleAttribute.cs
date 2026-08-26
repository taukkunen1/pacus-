using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Pacus.Domain.Enums;

namespace Pacus.Api.Auth;

// Autorizacao por papel — complementa [Authorize] (que so garante "esta autenticado").
// Uso: [RequireRole(UserRole.Adult)] em controllers/actions administrativas.
// A criança tentando acessar uma acao marcada assim recebe 403, nunca 200 com dados
// escondidos no frontend — "o frontend nao deve ser o mecanismo de seguranca" (spec).
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly UserRole _role;

    public RequireRoleAttribute(UserRole role) => _role = role;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var roleClaim = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        if (roleClaim is null || !roleClaim.Equals(_role.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            context.Result = new ObjectResult(new { error = "Acao restrita ao painel adulto." })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
    }
}
