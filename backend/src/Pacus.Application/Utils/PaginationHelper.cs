using Pacus.Application.Exceptions;

namespace Pacus.Application.Utils;

// Paginacao pra endpoints de listagem que crescem sem limite com o tempo (historico de
// rotinas encerradas, extrato de pontos, fila de resgates -- achado #4 da auditoria de
// API de 2026-09-01, ver docs/ESTADO_ATUAL.md). Valida os parametros vindos da query
// string antes de bater no banco, pra devolver 400 (ValidationException, achado #1) em
// vez de deixar um valor absurdo (page=0, pageSize=100000) chegar ate a query do Mongo.
public static class PaginationHelper
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static void Validate(int page, int pageSize)
    {
        if (page < 1)
            throw new ValidationException("O parametro 'page' deve ser 1 ou maior.");

        if (pageSize < 1 || pageSize > MaxPageSize)
            throw new ValidationException($"O parametro 'pageSize' deve estar entre 1 e {MaxPageSize}.");
    }
}
