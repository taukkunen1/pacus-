using Pacus.Domain.Enums;

namespace Pacus.Application.DTOs;

// Usado pelo painel do adulto para corrigir manualmente o estado do PACUS —
// por exemplo ao migrar dados de uma versao anterior do app, onde a familia
// ja tinha um PACUS em um estagio/tamanho especifico. Todos os campos sao
// opcionais: so o que for enviado e alterado.
public record UpdatePacusStateRequest(
    PacusStage? Stage,
    double? Size,
    int? TotalClosedDays,
    // 0-359. Enviar null nao muda a cor atual -- pra voltar pra cor derivada
    // automaticamente (ver Pacus.ColorHue), o frontend manda -1 como sinal
    // explicito de "limpar", tratado a parte no controller (nao pode reusar
    // null pra isso porque null aqui ja significa "nao mexer neste campo").
    int? ColorHue
);
