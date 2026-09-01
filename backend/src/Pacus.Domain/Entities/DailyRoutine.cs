using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

// A "fotografia" de um dia. Congela apos o fechamento (status = Closed).
public class DailyRoutine
{
    public ObjectId Id { get; set; }
    // Renomeado de UserId -> FamilyId (checklist de seguranca, item A4): o valor sempre
    // foi o id da familia, nunca de um usuario individual. BsonElement("userId") preserva
    // o nome do campo ja gravado no Mongo (convencao camelCase), sem precisar de migracao.
    [BsonElement("userId")]
    public ObjectId FamilyId { get; set; }
    // Data operacional no timezone do usuario, formato YYYY-MM-DD.
    public string Date { get; set; } = string.Empty;
    public string Timezone { get; set; } = "America/Sao_Paulo";
    public RoutineStatus Status { get; set; } = RoutineStatus.Open;
    public List<DailyTask> Tasks { get; set; } = new();
    public DailyRoutineStats Stats { get; set; } = new();
    public int PointsEarned { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Trava das tarefas da manha: marcado uma vez, no dia, quando todas as
    // tarefas do periodo "morning" ficam concluidas (ver DailyRoutineService).
    public DateTime? GameTimerUnlockedAt { get; set; }

    // Ajuste manual de tempo (+1h/-1h etc), so o adulto pode mexer — soma
    // direto nos minutos totais na hora de calcular o tempo restante.
    // Pode ficar negativo (reduzir tempo), mas o total nunca fica < 0
    // (ver ClampGameTimerExtraMinutes em DailyRoutineService).
    public int GameTimerExtraMinutes { get; set; } = 0;

    // Pausa/despausa (adulto ou crianca). Enquanto pausado, o tempo nao
    // corre: o calculo do restante desconta GameTimerPausedMs (tempo total
    // ja pausado) mais, se ainda pausado agora, o intervalo desde que
    // GameTimerPausedAt foi marcado.
    public DateTime? GameTimerPausedAt { get; set; }
    public long GameTimerPausedMs { get; set; } = 0;

    // Reacao pessoal do adulto sobre o dia (relatedness -- ver docs/PROPOSITO.md e
    // DailyReaction). Null enquanto ninguem reagiu hoje.
    public DailyReaction? Reaction { get; set; }

    // Espelham a configuracao da familia (Settings) so para esta resposta —
    // nao vem do banco nem e persistido aqui, so preenchido na hora de devolver
    // a rotina pra API (evita o frontend precisar de outra chamada).
    [BsonIgnore]
    public bool GameTimerEnabled { get; set; }

    [BsonIgnore]
    public int GameTimerMinutes { get; set; } = 120;

    // Controle de concorrencia otimista (achado #5 da auditoria de API de 2026-09-01 --
    // ver docs/ESTADO_ATUAL.md e DailyRoutineRepository.UpdateAsync). Incrementado a cada
    // update bem-sucedido; documentos existentes sem este campo desserializam como 0, sem
    // precisar de migracao. Nunca exposto na API (nao esta em DailyRoutineResponse) -- e
    // um detalhe interno de persistencia, nao algo que o frontend precise decidir sobre.
    public int Version { get; set; } = 0;
}
