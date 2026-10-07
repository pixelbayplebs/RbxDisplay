using Xunit;

namespace Stretcher;

public sealed class RecoveryTests
{
    [Fact(DisplayName = "normal restore succeeds")]
    public void NormalRestoreSucceeds()
    {
        SessionData session = Samples.Example();
        FakeDesktop desktop = Armed(session);
        Assert.Empty(Recovery.Restore(session, desktop));
        Assert.True(Rules.SameMode(desktop.Mode, session.OriginalMode));
        Assert.Equal(1, desktop.ModeWrites);
        Assert.True(Rules.SameColor(desktop.Color, session.OriginalColor));
        Assert.Equal(1, desktop.ColorWrites);
    }

    [Fact(DisplayName = "restore idempotent")]
    public void SecondRestoreDoesNotWriteAgain()
    {
        SessionData session = Samples.Example();
        FakeDesktop desktop = Armed(session);
        Assert.Empty(Recovery.Restore(session, desktop));
        Assert.Empty(Recovery.Restore(session, desktop));
        Assert.Equal(1, desktop.ModeWrites);
        Assert.Equal(1, desktop.ColorWrites);
    }

    [Fact(DisplayName = "manual external changes never overwritten")]
    public void ExternalChangesAreLeftAlone()
    {
        SessionData session = Samples.Example();
        FakeDesktop desktop = new FakeDesktop
        {
            Mode = Samples.Mode(2560, 1440, 180),
            Color = Rules.Saturated(Rules.Identity(), 1.6)
        };
        Recovery.Restore(session, desktop);
        Assert.Equal(0, desktop.ModeWrites);
        Assert.Equal(0, desktop.ColorWrites);
    }

    [Fact(DisplayName = "wrong monitor rejected and journal retained")]
    public void WrongMonitorKeepsTheJournal()
    {
        SessionData session = Samples.Example();
        FakeDesktop desktop = Armed(session);
        desktop.MonitorIdentity = "monitor-B";
        Assert.Single(Recovery.Restore(session, desktop));
        Assert.Equal(0, desktop.ModeWrites);
        Assert.True(session.ModeArmed);
        Assert.Equal(1, desktop.ColorWrites);
    }

    [Fact(DisplayName = "partial failure keeps unresolved recovery only")]
    public void ModeFailureKeepsModeArmed()
    {
        SessionData session = Samples.Example();
        FakeDesktop desktop = Armed(session);
        desktop.FailMode = true;
        Assert.Single(Recovery.Restore(session, desktop));
        Assert.True(session.ModeArmed);
        Assert.False(session.ColorArmed);
    }

    [Fact(DisplayName = "retry restores remaining mode")]
    public void RetryRestoresTheRemainingMode()
    {
        SessionData session = Samples.Example();
        FakeDesktop desktop = Armed(session);
        desktop.FailMode = true;
        Recovery.Restore(session, desktop);
        desktop.FailMode = false;
        Assert.Empty(Recovery.Restore(session, desktop));
        Assert.False(session.ModeArmed);
    }

    [Fact(DisplayName = "color failure doesn't block resolution restore")]
    public void ColorFailureStillRestoresResolution()
    {
        SessionData session = Samples.Example();
        FakeDesktop desktop = Armed(session);
        desktop.FailColor = true;
        Assert.Single(Recovery.Restore(session, desktop));
        Assert.Equal(1, desktop.ModeWrites);
        Assert.True(session.ColorArmed);
    }

    [Fact(DisplayName = "crash before mutation doesn't apply settings")]
    public void UnarmedCurrentStateIsNotRewritten()
    {
        SessionData session = Samples.Example();
        FakeDesktop desktop = new FakeDesktop { Mode = session.OriginalMode, Color = session.OriginalColor };
        Recovery.Restore(session, desktop);
        Assert.Equal(0, desktop.ModeWrites);
        Assert.Equal(0, desktop.ColorWrites);
    }

    [Fact(DisplayName = "explicit manual baseline honored")]
    public void ExplicitBaselineIsRestored()
    {
        SessionData session = Samples.Example();
        session.RestoreMode = Samples.Mode(2560, 1440, 180);
        FakeDesktop desktop = Armed(session);
        Recovery.Restore(session, desktop);
        Assert.True(Rules.SameMode(desktop.Mode, session.RestoreMode));
    }

    private static FakeDesktop Armed(SessionData session)
    {
        return new FakeDesktop { Mode = session.GameMode, Color = session.GameColor };
    }
}
