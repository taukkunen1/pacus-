using Microsoft.AspNetCore.Diagnostics;
using Pacus.Application.Exceptions;

namespace Pacus.Api.Middleware;

// Tratamento de excecao global (achado #1 da auditoria de API de 2026-09-01):
// antes disso, praticamente toda action de escrita repetia o mesmo bloco try/catch
// mapeando InvalidOperationException -> 400 e UnauthorizedAccessException -> 403,
// duplicado em 6 controllers. Registrado em Program.cs via
// builder.Services.AddExceptionHandler<AppExceptionHandler>() + app.UseExceptionHandler().
// Formato de erro sempre o mesmo: { "error": "mensagem" } -- igual ao que os
// controllers ja devolviam manualmente, pra nao mudar o contrato que o frontend espera.
public class AppExceptionHandler : IExceptionHandler
{
    private readonly ILogger<AppExceptionHandler> _logger;

    public AppExceptionHandler(ILogger<AppExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, message) = exception switch
        {
            ValidationException ex => (StatusCodes.Status400BadRequest, ex.Message),
            NotFoundException ex => (StatusCodes.Status404NotFound, ex.Message),
            ConflictException ex => (StatusCodes.Status409Conflict, ex.Message),
            UnauthorizedAccessException ex => (StatusCodes.Status403Forbidden, ex.Message),
            // Qualquer outra excecao e um bug de verdade (ou coisa que a gente ainda nao
            // classificou) -- nunca vaza a mensagem/stack real pro cliente, so loga.
            _ => (StatusCodes.Status500InternalServerError, "Erro interno. Tente novamente em instantes."),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Erro nao tratado em {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(
            new { error = message },
            cancellationToken);

        return true;
    }
}
