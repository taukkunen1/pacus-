using Pacus.Domain.Enums;

namespace Pacus.Application.DTOs;

// Usado pelo painel do adulto para corrigir manualmente o estado do PACUS —
// por exemplo ao migrar dados de uma versao anterior do app, onde a familia
// ja tinha um PACUS em um estagio/tamanho especifico. Todos os campos sao
// opcionais: so o que for enviado e alterado.
public record UpdatePacusStateRequest(
    PacusStage? Stage,
    double? Size,
    int? TotalClosedDays
);
