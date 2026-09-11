using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using Pacus.Application.Interfaces;
using Pacus.Domain.Enums;

namespace Pacus.Application.Services;

public class FamilyTimezoneService : IFamilyTimezoneService
{
    private const string FallbackTimezone = "America/Sao_Paulo";

    // Revisao de API (2026-09-11, achado #1): o fuso da familia quase nunca muda,
    // mas GetTimezoneAsync era chamado sem cache em praticamente toda requisicao
    // (AutonomyController, DailyRoutinesController, FamilyController,
    // PointsController, StoreService), gerando uma query ao Mongo por chamada so
    // pra reler o mesmo valor. TTL curto como rede de seguranca -- a invalidacao
    // explicita em UpdateTimezone (FamilyController) e o caminho normal.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly IUserRepository _userRepository;
    private readonly IMemoryCache _cache;

    public FamilyTimezoneService(IUserRepository userRepository, IMemoryCache cache)
    {
        _userRepository = userRepository;
        _cache = cache;
    }

    private static string CacheKey(ObjectId familyId) => $"family-timezone:{familyId}";

    // O adulto e a referencia do fuso da familia (mesmo raciocinio do PointToBrlRate:
    // um valor por familia, nao por usuario individual). Se por algum motivo a familia
    // nao tiver adulto cadastrado (nao deveria acontecer) ou o valor estiver vazio,
    // cai no fallback historico para nunca quebrar o fechamento de dia.
    public async Task<string> GetTimezoneAsync(ObjectId familyId)
    {
        var cacheKey = CacheKey(familyId);
        if (_cache.TryGetValue<string>(cacheKey, out var cached) && cached is not null)
            return cached;

        var adults = await _userRepository.GetByFamilyAndRoleAsync(familyId, UserRole.Adult);
        var timezone = adults.FirstOrDefault()?.Timezone;
        var resolved = string.IsNullOrWhiteSpace(timezone) ? FallbackTimezone : timezone;

        _cache.Set(cacheKey, resolved, CacheDuration);
        return resolved;
    }

    public void InvalidateCache(ObjectId familyId) => _cache.Remove(CacheKey(familyId));
}
