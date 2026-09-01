using MongoDB.Bson;
using Pacus.Application.Interfaces;

namespace Pacus.UnitTests.Fakes;

// Fake simples para os testes que nao se importam com fuso horario especifico --
// devolve sempre o mesmo valor, equivalente ao fallback historico do backend.
public class FakeFamilyTimezoneService : IFamilyTimezoneService
{
    public string Timezone { get; set; } = "America/Sao_Paulo";

    public Task<string> GetTimezoneAsync(ObjectId familyId) => Task.FromResult(Timezone);
}
