using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace RbxDisplay;

internal interface ISessionHost
{
    bool IsClosing { get; }
    bool DialogOpen { get; }
    bool Initialized { get; }
    AppSettings Settings { get; }
    bool ModesDirty { get; set; }
    void Publish(LiveState state);
    void Notify(string message);
    void ReportSessionFailure(Exception exception);
    MonitorInfo ResolveMonitor(GameProfile profile);
    void ReloadModes(bool refreshMonitors);
    void SetTray(string tooltip);
}

internal sealed class MonitoringController
{
    private readonly RobloxWatcher watcher = new();
    private bool ticking;
    private DateTime testDeadline;
    private DateTime nextPoll;
    private DateTime beginAfter = DateTime.MinValue;
    private int boundPid;
    private long boundTicks;
    private long boundGeneration;
    private int suppressPid;
    private long suppressTicks;
    private string? holdMessage;
    private string? beginFailure;
    private GameObservation? lastObservation;

    public ProfileSession Session { get; } = new();
    public bool Monitoring { get; private set; }
    public bool Testing { get; private set; }
    public Settings? Runtime { get; private set; }
    public string? ActiveProfileId { get; private set; }

    public void KeepWatching()
    {
        Monitoring = true;
    }

    public void Arm(ISessionHost host)
    {
        Monitoring = true;
        Testing = false;
        nextPoll = DateTime.MinValue;
        beginAfter = DateTime.MinValue;
        beginFailure = null;
        Publish(host, Waiting("Monitoring is on. A saved profile applies when that game is in the foreground."));
    }

    public void SetMonitoring(ISessionHost host, bool enabled)
    {
        if (enabled)
        {
            if (!Monitoring)
                Arm(host);
            return;
        }

        if (!Monitoring && !Session.Active && !Testing)
            return;
        Testing = false;
        ClearSuppress();
        string? error = Session.End();
        if (error == null && File.Exists(Store.SessionPath))
            error = Recovery.FromJournal(null);
        ActiveProfileId = null;
        boundPid = 0;
        boundTicks = 0;
        boundGeneration = 0;
        Runtime = null;
        Monitoring = false;
        if (error != null)
            Store.Log(error);
        Publish(host, new LiveState
        {
            Badge = "Paused",
            Title = "Monitoring is off",
            Message = error == null ? "Monitoring is paused. Turn it on to apply profiles again." : error,
            Tone = error == null ? LiveTone.Idle : LiveTone.Warning,
            Tray = "RbxDisplay — paused"
        });
    }

    public void ToggleMonitoring(ISessionHost host)
    {
        SetMonitoring(host, !Monitoring);
    }

    public string? Shutdown()
    {
        Monitoring = Testing = false;
        ClearSuppress();
        string? error = Session.End();
        if (error == null && File.Exists(Store.SessionPath))
            error = Recovery.FromJournal(null);
        watcher.Reset();
        lastObservation = null;
        ActiveProfileId = null;
        Runtime = null;
        return error;
    }

    public void EndSession(ISessionHost host)
    {
        Testing = false;
        ClearSuppress();
        string? error = Session.End();
        ActiveProfileId = null;
        boundPid = 0;
        boundTicks = 0;
        boundGeneration = 0;
        Runtime = null;
        if (error != null)
            throw new InvalidOperationException(error);
        try
        {
            host.ReloadModes(false);
        }
        catch (InvalidOperationException ex)
        {
            Store.Log(ex.ToString());
        }

        if (Monitoring)
            Publish(host, Waiting("Monitoring is on. A saved profile applies when that game is in the foreground."));
    }

    public void RestoreDisplay(ISessionHost host, string message)
    {
        if (boundPid > 0)
        {
            suppressPid = boundPid;
            suppressTicks = boundTicks;
        }
        else if (lastObservation is { Pid: > 0 } observed && Native.ForegroundPid() == observed.Pid)
        {
            suppressPid = observed.Pid;
            suppressTicks = observed.ProcessTicks;
        }

        Testing = false;
        string? error = Session.End();
        if (error == null && File.Exists(Store.SessionPath))
            error = Recovery.FromJournal(null);
        ActiveProfileId = null;
        Runtime = null;
        holdMessage = error == null ? message : message + "\n" + error;
        if (error != null)
            Store.Log(error);
        host.ReloadModes(false);
        Publish(host, new LiveState
        {
            Badge = "Restored",
            Title = "Display restored",
            Message = holdMessage,
            Tone = error == null ? LiveTone.Idle : LiveTone.Warning,
            Tray = "RbxDisplay — monitoring"
        });
    }

    public void Apply(ISessionHost host, GameProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (!Session.Active || ActiveProfileId != profile.Id)
            return;
        Settings runtime = host.Settings.Runtime(profile);
        runtime.Validate();
        Runtime = runtime;
        MonitorInfo screen = host.ResolveMonitor(profile);
        if (Testing)
        {
            Session.Retarget(runtime, screen, true, runtime.SaturationEnabled);
            PublishTesting(host, profile);
            return;
        }

        bool focused = Native.ForegroundPid() == boundPid;
        Session.Retarget(runtime, screen, !runtime.ResolutionOnFocusOnly || focused, focused);
        PublishActive(host, profile, focused);
    }

    public void TestProfile(ISessionHost host, GameProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        string? error = Session.End();
        if (error != null)
            throw new InvalidOperationException(error);
        ClearSuppress();
        boundPid = 0;
        boundTicks = 0;
        boundGeneration = 0;
        if (host.ModesDirty)
            host.ReloadModes(false);
        Settings runtime = host.Settings.Runtime(profile);
        runtime.Validate();
        Runtime = runtime;
        MonitorInfo screen = host.ResolveMonitor(profile);
        Session.Begin(runtime, screen);
        Session.SetResolution(true);
        Session.SetColors(runtime.SaturationEnabled);
        Testing = true;
        Monitoring = true;
        ActiveProfileId = profile.Id;
        testDeadline = DateTime.UtcNow.AddSeconds(15);
        PublishTesting(host, profile);
    }

    public static void Launch(GameProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Process.Start(new ProcessStartInfo(profile.LaunchUrl()) { UseShellExecute = true });
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A monitoring failure must stop the applied profile and report restoration in the window.")]
    public void Tick(ISessionHost host)
    {
        if (ticking || host.IsClosing || !host.Initialized)
            return;
        ticking = true;
        try
        {
            if (Session.Active)
            {
                Session.CheckLease();
                Session.Heartbeat();
            }

            if (host.DialogOpen)
                return;

            if (host.ModesDirty)
            {
                try
                {
                    host.ReloadModes(true);
                }
                catch (Exception ex)
                {
                    host.ModesDirty = false;
                    Store.Log(ex.ToString());
                    host.Notify(ex.Message);
                }
            }

            if (Testing)
            {
                int remaining = (int)Math.Ceiling((testDeadline - DateTime.UtcNow).TotalSeconds);
                GameProfile? testing = FindActive(host);
                if (remaining <= 0)
                {
                    string? error = Session.End();
                    Testing = false;
                    ActiveProfileId = null;
                    Runtime = null;
                    if (error != null)
                        throw new InvalidOperationException(error);
                    host.ReloadModes(false);
                    nextPoll = DateTime.MinValue;
                    Publish(host, Waiting("Test completed. Display settings restored."));
                }
                else if (testing != null)
                {
                    Publish(host, new LiveState
                    {
                        Badge = "Testing",
                        Title = testing.Name,
                        Message = "Restoring in " + remaining + " seconds. Ctrl+Alt+F12 restores immediately.",
                        Resolution = Session.Data?.GameMode?.ToString() ?? "—",
                        Saturation = SaturationText(Runtime),
                        Monitor = Session.Data?.MonitorDevice ?? "—",
                        Focus = "Preview",
                        Tone = LiveTone.Active,
                        Tray = "RbxDisplay — testing",
                        GameId = testing.Id
                    });
                }

                return;
            }

            if (!Monitoring)
                return;

            Settings sample = new()
            {
                LogsFolder = host.Settings.LogsFolder,
                PollMilliseconds = host.Settings.PollMilliseconds
            };
            if (DateTime.UtcNow >= nextPoll)
            {
                lastObservation = watcher.Poll(sample.LogPath());
                nextPoll = DateTime.UtcNow.AddMilliseconds(sample.PollMilliseconds);
            }

            GameObservation? observation = lastObservation;
            if (observation == null)
                return;

            if (suppressPid != 0)
            {
                bool sameProcess = observation.Pid == suppressPid && observation.ProcessTicks == suppressTicks;
                bool focused = observation.Pid > 0 && Native.ForegroundPid() == observation.Pid;
                if (!sameProcess || !focused)
                    ClearSuppress();
            }

            GameProfile? profile = observation.UniverseId.HasValue ? host.Settings.Match(observation.UniverseId) : null;
            bool target = profile != null && observation.Pid > 0;
            if (Session.Active && (!target || profile!.Id != ActiveProfileId || observation.Pid != boundPid || observation.ProcessTicks != boundTicks || observation.Generation != boundGeneration))
            {
                string? error = Session.End();
                ActiveProfileId = null;
                Runtime = null;
                if (error != null)
                    throw new InvalidOperationException(error);
                host.ReloadModes(false);
            }

            if (suppressPid != 0 && observation.Pid == suppressPid && Native.ForegroundPid() == observation.Pid)
            {
                Publish(host, new LiveState
                {
                    Badge = "Restored",
                    Title = profile?.Name ?? "Display restored",
                    Message = holdMessage ?? "Display restored. The profile applies again after the game loses focus.",
                    Tone = LiveTone.Idle,
                    Tray = "RbxDisplay — monitoring",
                    GameId = profile?.Id
                });
                return;
            }

            if (!Session.Active && target && Native.ForegroundPid() == observation.Pid)
            {
                if (DateTime.UtcNow < beginAfter)
                {
                    Publish(host, new LiveState
                    {
                        Badge = "Waiting",
                        Title = profile!.Name,
                        Message = beginFailure ?? "The profile could not be applied yet.",
                        Tone = LiveTone.Warning,
                        Tray = "RbxDisplay — monitoring",
                        GameId = profile!.Id
                    });
                    return;
                }

                try
                {
                    Settings runtime = host.Settings.Runtime(profile!);
                    runtime.Validate();
                    MonitorInfo screen = host.ResolveMonitor(profile!);
                    Session.Begin(runtime, screen);
                    Runtime = runtime;
                    boundPid = observation.Pid;
                    boundTicks = observation.ProcessTicks;
                    boundGeneration = observation.Generation;
                    ActiveProfileId = profile!.Id;
                    beginFailure = null;
                }
                catch (InvalidOperationException ex)
                {
                    beginAfter = DateTime.UtcNow.AddSeconds(2);
                    beginFailure = ex.Message;
                    Store.Log(ex.ToString());
                    Publish(host, new LiveState
                    {
                        Badge = "Waiting",
                        Title = profile!.Name,
                        Message = ex.Message,
                        Tone = LiveTone.Warning,
                        Tray = "RbxDisplay — monitoring",
                        GameId = profile!.Id
                    });
                    return;
                }

                try
                {
                    host.ReloadModes(false);
                }
                catch (InvalidOperationException ex)
                {
                    Store.Log(ex.ToString());
                }
            }

            if (Session.Active)
            {
                GameProfile? active = FindActive(host);
                Settings runtime = Runtime!;
                bool focused = Native.ForegroundPid() == boundPid;
                Session.SetResolution(!runtime.ResolutionOnFocusOnly || focused);
                Session.SetColors(focused);
                if (active != null)
                    PublishActive(host, active, focused);
            }
            else if (observation.Pid > 0 && observation.UniverseId.HasValue && profile == null)
            {
                Publish(host, new LiveState
                {
                    Badge = "No profile",
                    Title = "Unknown game",
                    Message = "Universe ID " + observation.UniverseId.Value + " has no saved profile. Add it in Games.",
                    Tone = LiveTone.Warning,
                    Tray = "RbxDisplay — monitoring"
                });
            }
            else
            {
                Publish(host, Waiting(observation.Detail));
            }
        }
        catch (Exception ex)
        {
            host.ReportSessionFailure(ex);
        }
        finally
        {
            ticking = false;
        }
    }

    private GameProfile? FindActive(ISessionHost host)
    {
        return host.Settings.Games.FirstOrDefault(game => game.Id == ActiveProfileId);
    }

    private void PublishActive(ISessionHost host, GameProfile profile, bool focused)
    {
        Settings? runtime = Runtime;
        bool focusOnly = runtime?.ResolutionOnFocusOnly == true;
        Publish(host, new LiveState
        {
            Badge = focused ? "Profile active" : "In background",
            Title = profile.Name,
            Message = focused
                ? profile.Name + " is stretched while it is in front."
                : focusOnly
                    ? "The game is in the background. Resolution and colors are restored."
                    : "The game is in the background. Colors are restored. Resolution stays stretched.",
            Resolution = Session.Data?.GameMode?.ToString() ?? "—",
            Saturation = SaturationText(runtime),
            Monitor = Session.Data?.MonitorDevice ?? profile.MonitorDevice,
            Focus = focusOnly ? "Resolution follows focus" : "Resolution stays for this visit",
            Tone = focused ? LiveTone.Active : LiveTone.Idle,
            Tray = "RbxDisplay — " + profile.Name,
            GameId = profile.Id
        });
    }

    private void PublishTesting(ISessionHost host, GameProfile profile)
    {
        int remaining = (int)Math.Ceiling((testDeadline - DateTime.UtcNow).TotalSeconds);
        Publish(host, new LiveState
        {
            Badge = "Testing",
            Title = profile.Name,
            Message = "Restoring in " + Math.Max(remaining, 0) + " seconds. Ctrl+Alt+F12 restores immediately.",
            Resolution = Session.Data?.GameMode?.ToString() ?? "—",
            Saturation = SaturationText(Runtime),
            Monitor = Session.Data?.MonitorDevice ?? profile.MonitorDevice,
            Focus = "Preview",
            Tone = LiveTone.Active,
            Tray = "RbxDisplay — testing",
            GameId = profile.Id
        });
    }

    private static string SaturationText(Settings? runtime)
    {
        if (runtime == null)
            return "—";
        return runtime.SaturationEnabled ? Math.Round(runtime.Saturation * 100) + "%" : "Off";
    }

    private static LiveState Waiting(string message)
    {
        return new LiveState
        {
            Badge = "Monitoring",
            Title = "No game detected",
            Message = message,
            Tray = "RbxDisplay — monitoring"
        };
    }

    private static void Publish(ISessionHost host, LiveState state)
    {
        host.Publish(state);
        host.SetTray(state.Tray);
    }

    private void ClearSuppress()
    {
        suppressPid = 0;
        suppressTicks = 0;
        holdMessage = null;
    }
}
