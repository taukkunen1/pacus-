using MongoDB.Bson;
using Pacus.Application.Interfaces;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

public class FamilyTimezoneService : IFamilyTimezoneService
{
    private const string FallbackTimezone = "America/Sao_Paulo";

    private readonly IUserRepository _userRepository;

    public FamilyTimezoneService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    // O adulto e a referencia do fuso da familia (mesmo raciocinio do PointToBrlRate:
    // um valor por familia, nao por usuario individual). Se por algum motivo a familia
    // nao tiver adulto cadastrado (nao deveria acontecer) ou o valor estiver vazio,
    // cai no fallback historico para nunca quebrar o fechamento de dia.
    public async Task<string> GetTimezoneAsync(ObjectId familyId)
    {
        var adults = await _userRepository.GetByFamilyAndRoleAsync(familyId, UserRole.Adult);
        var timezone = adults.FirstOrDefault()?.Timezone;

        return string.IsNullOrWhiteSpace(timezone) ? FallbackTimezone : timezone;
    }
}
