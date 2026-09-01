using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pacus.Domain.Entities;

public class Settings
{
    public ObjectId Id { get; set; }
    // Renomeado de UserId -> FamilyId (checklist de seguranca, item A4): o valor sempre
    // foi o id da familia, nunca de um usuario individual. BsonElement("userId") preserva
    // o nome do campo ja gravado no Mongo (convencao camelCase), sem precisar de migracao.
    [BsonElement("userId")]
    public ObjectId FamilyId { get; set; }

    // Valor de referencia (nao e uma conversao financeira real -- ver docs/TERMOS_DE_USO.md).
    // Constante exposta porque PointsController usa o mesmo fallback quando a familia
    // ainda nao tem um documento de Settings salvo (GetByUserIdAsync retorna null).
    public const double DefaultPointToBrlRate = 0.06;

    // Taxa antiga, antes da mudanca de 0.05 -> 0.06. Nunca existiu (nem existe) um
    // endpoint pra familia escolher essa taxa -- o valor so era gravado no Mongo de
    // forma implicita, pelo default da propriedade C# no momento em que o documento
    // de Settings era criado (ex.: ao ligar o tempo de jogo pela primeira vez). Toda
    // familia cujo Settings foi criado antes dessa mudanca ficou com 0.05 congelado
    // no banco pra sempre, mesmo depois do default do codigo virar 0.06. Exposta pra
    // PointsController conseguir detectar e curar esse valor antigo na leitura.
    public const double LegacyDefaultPointToBrlRate = 0.05;

    public double PointToBrlRate { get; set; } = DefaultPointToBrlRate;
    public List<GrowthStageConfig> GrowthStages { get; set; } = new();
    public ChildPermissions ChildPermissions { get; set; } = new();

    // Trava das tarefas da manha: ao concluir todas, libera um cronometro de
    // tempo de jogo. Desligado por padrao para toda familia — so quem ativa
    // explicitamente (painel do adulto) tem essa mecanica.
    public bool GameTimerEnabled { get; set; } = false;
    public int GameTimerMinutes { get; set; } = 120;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
