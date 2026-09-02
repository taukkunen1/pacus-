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
}
