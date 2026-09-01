using MongoDB.Bson;

namespace Pacus.Domain.Entities;

// Vinculo (relatedness -- terceira necessidade da Teoria da Autodeterminacao, ver
// docs/PROPOSITO.md): uma reacao curta e pessoal do adulto sobre o dia da crianca, nao
// um elogio automatico do sistema. Um por DailyRoutine (granularidade "por dia" —
// reagir de novo no mesmo dia substitui a reacao anterior, nao acumula). So o adulto
// registra (ver IDailyRoutineService.SetReactionAsync); a crianca so le, atraves do
// PACUS (ver frontend/js/pacus/habitat.js).
public class DailyReaction
{
    // Chave semantica de um icone pre-definido (ex.: "heart", "clap", "star", "hug") —
    // nao o emoji em si, pra manter o mapeamento pro emoji real no frontend, nao no
    // banco. Validado em DailyRoutineService.SetReactionAsync.
    public string Icon { get; set; } = string.Empty;

    // Texto opcional. O frontend pre-preenche com uma frase padrao pro icone escolhido,
    // mas o adulto pode editar ou apagar (null/vazio = so o icone, sem texto).
    public string? Message { get; set; }

    public ObjectId CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
