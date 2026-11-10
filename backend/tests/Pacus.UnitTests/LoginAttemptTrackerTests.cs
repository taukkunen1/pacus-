using Pacus.Application.Services;

namespace Pacus.UnitTests;

// Bloqueio temporario por conta apos falhas seguidas (protege o PIN de 4 digitos
// contra forca bruta distribuida em varios IPs).
public class LoginAttemptTrackerTests
{
    private sealed class FakeClock
    {
        public DateTime Now { get; set; } = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public void ContaNova_NaoEstaBloqueada()
    {
        var tracker = new LoginAttemptTracker();
        Assert.False(tracker.IsLockedOut("child:a"));
    }

    [Fact]
    public void AposMaxFalhas_ContaFicaBloqueada()
    {
        var tracker = new LoginAttemptTracker();
        for (var i = 0; i < LoginAttemptTracker.MaxFailures - 1; i++)
        {
            tracker.RegisterFailure("child:a");
            Assert.False(tracker.IsLockedOut("child:a"));
        }

        tracker.RegisterFailure("child:a");
        Assert.True(tracker.IsLockedOut("child:a"));
    }

    [Fact]
    public void BloqueioDeUmaConta_NaoAfetaOutra()
    {
        var tracker = new LoginAttemptTracker();
        for (var i = 0; i < LoginAttemptTracker.MaxFailures; i++)
            tracker.RegisterFailure("child:a");

        Assert.True(tracker.IsLockedOut("child:a"));
        Assert.False(tracker.IsLockedOut("child:b"));
    }

    [Fact]
    public void LoginComSucesso_ZeraContador()
    {
        var tracker = new LoginAttemptTracker();
        for (var i = 0; i < LoginAttemptTracker.MaxFailures - 1; i++)
            tracker.RegisterFailure("adult:x@y.com");

        tracker.Reset("adult:x@y.com");
        tracker.RegisterFailure("adult:x@y.com");

        Assert.False(tracker.IsLockedOut("adult:x@y.com"));
    }

    [Fact]
    public void BloqueioExpira_ApósADuracao()
    {
        var clock = new FakeClock();
        var tracker = new LoginAttemptTracker(() => clock.Now);
        for (var i = 0; i < LoginAttemptTracker.MaxFailures; i++)
            tracker.RegisterFailure("child:a");

        Assert.True(tracker.IsLockedOut("child:a"));

        clock.Now += LoginAttemptTracker.LockoutDuration + TimeSpan.FromSeconds(1);

        Assert.False(tracker.IsLockedOut("child:a"));
    }
}
