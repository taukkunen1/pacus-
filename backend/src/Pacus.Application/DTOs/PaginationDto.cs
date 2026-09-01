namespace Pacus.Application.DTOs;

// Envelope generico de paginacao (achado #4 da auditoria de API de 2026-09-01 -- ver
// docs/ESTADO_ATUAL.md), usado pelos endpoints de listagem que crescem sem limite com
// o tempo: historico de rotinas encerradas, extrato de Pacus Points, fila de resgates.
public record PagedResult<T>(List<T> Items, int Page, int PageSize, long TotalCount)
{
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
