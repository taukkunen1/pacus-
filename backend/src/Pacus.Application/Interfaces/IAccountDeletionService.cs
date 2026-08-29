using MongoDB.Bson;

namespace Pacus.Application.Interfaces;

public interface IAccountDeletionService
{
    // Exclui permanentemente a conta (toda a familia) que pediu a exclusao (LGPD,
    // item B3 / art. 18, VI). requestedBy e quem confirmou a operacao (para o log de
    // auditoria final, que e o unico registro anonimizado -- nao excluido -- ao fim).
    Task DeleteAccountAsync(ObjectId familyId, ObjectId requestedBy);
}
