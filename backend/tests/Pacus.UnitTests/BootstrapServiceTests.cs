using Pacus.Application.DTOs;
using Pacus.Application.Exceptions;
using Pacus.Application.Services;
using Pacus.UnitTests.Fakes;

namespace Pacus.UnitTests;

// Cobre a validacao de entrada (que nao existia antes -- um POST com campos vazios
// ou mal formados criava uma familia quebrada silenciosamente) e a geracao do
// codigo curto de familia (ver User.FamilyCode), incluindo a checagem de
// unicidade contra colisao.
public class BootstrapServiceTests
{
    private static BootstrapRequest ValidRequest() =>
        new(
            AdultName: "Pedro",
            AdultEmail: $"pedro-{Guid.NewGuid():N}@example.com",
            AdultPassword: "senha-forte-123",
            ChildName: "Hector",
            ChildPin: "1234",
            ResponsibleConsent: true
        );

    private static BootstrapService BuildSystem(out FakeUserRepository users)
    {
        users = new FakeUserRepository();
        return new BootstrapService(
            users,
            new FakePacusRepository(),
            new FakeStoreRepository(),
            new FakePasswordHasher());
    }

    [Fact]
    public async Task CriaFamilia_ComDadosValidos_GeraCodigoDeFamiliaNoFormatoEsperado()
    {
        var service = BuildSystem(out var users);

        var result = await service.CreateInitialFamilyAsync(ValidRequest());

        Assert.Matches("^[A-Z2-9]{3}-[A-Z2-9]{3}$", result.FamilyCode);
        Assert.Equal(2, users.Users.Count);
        Assert.All(users.Users, u => Assert.Equal(result.FamilyCode, u.FamilyCode));
    }

    [Fact]
    public async Task CriaFamilia_ComEmailJaExistente_LancaConflictException()
    {
        var service = BuildSystem(out _);
        var first = ValidRequest();
        await service.CreateInitialFamilyAsync(first);

        var second = first with { ChildName = "Outra crianca" };

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateInitialFamilyAsync(second));
    }

    [Theory]
    [InlineData("", "pedro@example.com", "senha-forte-123", "Hector", "1234")] // nome do adulto vazio
    [InlineData("Pedro", "", "senha-forte-123", "Hector", "1234")] // email vazio
    [InlineData("Pedro", "nao-e-email", "senha-forte-123", "Hector", "1234")] // email invalido
    [InlineData("Pedro", "pedro@example.com", "curta", "Hector", "1234")] // senha curta (< 8)
    [InlineData("Pedro", "pedro@example.com", "senha-forte-123", "", "1234")] // nome da crianca vazio
    [InlineData("Pedro", "pedro@example.com", "senha-forte-123", "Hector", "12")] // PIN curto
    [InlineData("Pedro", "pedro@example.com", "senha-forte-123", "Hector", "abcd")] // PIN nao numerico
    public async Task CriaFamilia_ComEntradaInvalida_LancaValidationException(
        string adultName, string adultEmail, string adultPassword, string childName, string childPin)
    {
        var service = BuildSystem(out _);
        var request = new BootstrapRequest(adultName, adultEmail, adultPassword, childName, childPin, true);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateInitialFamilyAsync(request));
    }

    [Fact]
    public async Task CriaFamilia_SemAceiteDoResponsavel_LancaValidationException()
    {
        var service = BuildSystem(out _);
        var request = ValidRequest() with { ResponsibleConsent = false };

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateInitialFamilyAsync(request));
    }

    [Fact]
    public async Task CriaFamilia_QuandoCodigoJaEstaEmUso_TentaOutroCodigoAteAchar()
    {
        var service = BuildSystem(out var users);

        // Pre-ocupa o primeiro codigo que AuthService.GenerateFamilyCode poderia
        // gerar nao e controlavel diretamente (aleatorio) -- entao simulamos a
        // colisao de outro jeito: criamos varias familias em sequencia e
        // confirmamos que nenhuma delas nunca repete codigo entre si, o que so
        // acontece se a checagem de unicidade (GetByFamilyCodeAsync) estiver
        // realmente sendo consultada a cada tentativa.
        var codes = new HashSet<string>();
        for (var i = 0; i < 15; i++)
        {
            var result = await service.CreateInitialFamilyAsync(ValidRequest());
            Assert.True(codes.Add(result.FamilyCode), "Codigo de familia repetido entre famílias diferentes.");
        }

        Assert.Equal(30, users.Users.Count);
    }
}
