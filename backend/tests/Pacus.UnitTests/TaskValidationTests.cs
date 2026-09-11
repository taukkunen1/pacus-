using Pacus.Application.Exceptions;
using Pacus.Application.Services;

namespace Pacus.UnitTests;

// Cobre a validacao compartilhada extraida na revisao de API de 2026-09-11
// (achado #6) -- titulo/descricao agora tem limite de tamanho, antes so
// checavam "nao vazio" (titulo) ou nem eram checados (descricao).
public class TaskValidationTests
{
    [Fact]
    public void ValidateTitle_Vazio_LancaValidationException()
    {
        Assert.Throws<ValidationException>(() => TaskValidation.ValidateTitle(""));
        Assert.Throws<ValidationException>(() => TaskValidation.ValidateTitle("   "));
        Assert.Throws<ValidationException>(() => TaskValidation.ValidateTitle(null));
    }

    [Fact]
    public void ValidateTitle_MaiorQueLimite_LancaValidationException()
    {
        var titulo = new string('a', TaskValidation.MaxTitleLength + 1);
        Assert.Throws<ValidationException>(() => TaskValidation.ValidateTitle(titulo));
    }

    [Fact]
    public void ValidateTitle_DentroDoLimite_NaoLancaExcecao()
    {
        var titulo = new string('a', TaskValidation.MaxTitleLength);
        TaskValidation.ValidateTitle(titulo);
    }

    [Fact]
    public void ValidateDescription_Nula_NaoLancaExcecao()
    {
        TaskValidation.ValidateDescription(null);
    }

    [Fact]
    public void ValidateDescription_MaiorQueLimite_LancaValidationException()
    {
        var descricao = new string('a', TaskValidation.MaxDescriptionLength + 1);
        Assert.Throws<ValidationException>(() => TaskValidation.ValidateDescription(descricao));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(-11)]
    public void ValidatePoints_ForaDaFaixa_LancaValidationException(int points)
    {
        Assert.Throws<ValidationException>(() => TaskValidation.ValidatePoints(points));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(-10)]
    public void ValidatePoints_DentroDaFaixa_NaoLancaExcecao(int points)
    {
        TaskValidation.ValidatePoints(points);
    }
}

