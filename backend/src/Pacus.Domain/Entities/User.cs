using MongoDB.Bson;
using Pacus.Domain.Enums;

namespace Pacus.Domain.Entities;

public class User
{
    public ObjectId Id { get; set; }
    public UserRole Role { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public string? PinHash { get; set; }

    // So o adulto tem (reset de senha "esqueci minha senha" sem depender de e-mail --
    // este projeto nao tem provedor de e-mail configurado). Gerado uma vez no bootstrap
    // e mostrado em texto puro so naquele momento; nunca gravado em texto puro, so o hash.
    // Rotacionado (novo codigo gerado e devolvido) a cada reset bem-sucedido.
    public string? RecoveryCodeHash { get; set; }

    public string Timezone { get; set; } = "America/Sao_Paulo";

    // Codigo curto da familia (formato "XXX-YYY", gerado uma vez no bootstrap e
    // copiado pro adulto + crianca, mesmo padrao denormalizado do Timezone acima).
    // Usado pela crianca pra encontrar a familia dela ao logar num aparelho novo,
    // sem precisar colar um ObjectId do Mongo -- ver AuthService.GenerateFamilyCode
    // e FamilyController.GetChildrenByFamilyCode.
    public string FamilyCode { get; set; } = string.Empty;

    public ObjectId FamilyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
