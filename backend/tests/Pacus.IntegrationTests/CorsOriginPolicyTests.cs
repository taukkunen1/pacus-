using Pacus.Api.Security;

namespace Pacus.IntegrationTests;

public class CorsOriginPolicyTests
{
    [Fact]
    public void Production_ShouldAllowOnlyOfficialDomain()
    {
        var origins = CorsOriginPolicy.Resolve(
            useDevelopmentOrigins: false,
            configuredOrigins:
                "https://pacus-1.onrender.com,https://taukkunen1.github.io,http://localhost:5500");

        Assert.Equal(new[] { CorsOriginPolicy.OfficialOrigin }, origins);
    }

    [Fact]
    public void Development_ShouldRespectConfiguredOrigins()
    {
        var origins = CorsOriginPolicy.Resolve(
            useDevelopmentOrigins: true,
            configuredOrigins:
                "http://localhost:5500,http://localhost:3000");

        Assert.Equal(
            new[] { "http://localhost:5500", "http://localhost:3000" },
            origins);
    }

    [Fact]
    public void Development_ShouldFallbackToLocalhost()
    {
        var origins = CorsOriginPolicy.Resolve(
            useDevelopmentOrigins: true,
            configuredOrigins: null);

        Assert.Equal(
            new[] { CorsOriginPolicy.DevelopmentFallbackOrigin },
            origins);
    }
}
