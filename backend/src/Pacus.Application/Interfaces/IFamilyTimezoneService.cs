using MongoDB.Bson;

namespace Pacus.Application.Interfaces;

// Resolve o fuso horario real da familia em vez do "America/Sao_Paulo" fixo que
// estava espalhado por DailyRoutinesController, PointsController e StoreService.
// User.Timezone ja existia no schema (gravado no bootstrap) mas nunca era lido de
// volta -- so o adulto tem endpoint para altera-lo (ver UsersController), e ele
// e tratado como o fuso "canonico" da familia inteira.
public interface IFamilyTimezoneService
{
    Task<string> GetTimezoneAsync(ObjectId familyId);

    // Revisao de API (2026-09-11, achado #1): GetTimezoneAsync e cacheado em
    // memoria (ver FamilyTimezoneService) porque e chamado em quase toda
    // requisicao (AutonomyController, DailyRoutinesController, FamilyController,
    // PointsController, StoreService) so pra ler um valor que quase nunca muda.
    // UpdateTimezone (FamilyController) chama isto logo apos gravar o novo fuso
    // pra nao deixar a familia presa no valor antigo ate o cache expirar sozinho.
    void InvalidateCache(ObjectId familyId);
}
