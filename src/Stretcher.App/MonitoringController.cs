using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Stretcher;

internal interface IProfileHost
{
    bool IsClosing { get; }
    bool DialogOpen { get; }
    bool Initialized { get; }
    AppSettings Settings { get; }
    DisplayBrowser Display { get; }
    void SaveUi();
    void SetStatus(string value);
    void UpdateControls();
    void ReloadModes(bool refreshMonitors);
    void ReportError(Exception exception);
}

internal sealed class MonitoringController
{
    private readonly RobloxWatcher watcher = new();
    private bool ticking;
    private DateTime testDeadline;
    private DateTime nextPoll;
    private int boundPid;
    private long boundTicks;
    private long boundGeneration;
    private GameObservation? lastObservation;

    public ProfileSession Session { get; } = new();
    public bool Monitoring { get; private set; }
    public bool Testing { get; private set; }
    public Settings? Runtime { get; set; }
    public bool IsBusy => Monitoring || Testing || Session.Active;

    public void ResetPoll()
    {
        watcher.Reset();
        nextPoll = DateTime.MinValue;
    }

    public string? StopCore(IProfileHost host)
    {
        Monitoring = Testing = false;
        string? error = Session.End();
        if (error == null && File.Exists(Store.SessionPath))
            error = Recovery.FromJournal(null);
        watcher.Reset();
        lastObservation = null;
        host.Display.ModesDirty = true;
        host.UpdateControls();
        return error;
    }

    public void StopProfile(IProfileHost host, string message)
    {
        string? error = StopCore(host);
        if (error != null)
            throw new InvalidOperationException(error);
        host.ReloadModes(false);
        host.SetStatus(message);
    }

    public void ToggleMonitoring(IProfileHost host)
    {
        if (Monitoring || Testing)
        {
            StopProfile(host, "Monitoring stopped. Display settings restored.");
            return;
        }

        if (File.Exists(Store.SessionPath))
            throw new InvalidOperationException("Restore the previous session first.");
        host.SaveUi();
        Monitoring = true;
        host.UpdateControls();
        nextPoll = DateTime.MinValue;
        host.SetStatus("Monitoring " + host.Settings.SelectedGame().Name + ". The profile activates after a confirmed game connection.");
    }

    public void TestProfile(IProfileHost host)
    {
        string? error = StopCore(host);
        if (error != null)
            throw new InvalidOperationException(error);
        if (host.Display.ModesDirty)
            host.ReloadModes(false);
        host.SaveUi();
        Settings runtime = Runtime!;
        Session.Begin(runtime, host.Display.SelectedMonitor());
        Session.SetResolution(true);
        Session.SetColors(true);
        Testing = true;
        testDeadline = DateTime.UtcNow.AddSeconds(15);
        host.UpdateControls();
        host.SetStatus("Testing. Restoring in 15 seconds. Ctrl+Alt+F12 restores immediately.");
    }

    public void LaunchGame(IProfileHost host)
    {
        if (!Monitoring)
            ToggleMonitoring(host);
        Process.Start(new ProcessStartInfo(host.Settings.SelectedGame().LaunchUrl()) { UseShellExecute = true });
        host.SetStatus("Launching " + host.Settings.SelectedGame().Name + ". Waiting for a confirmed connection.");
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A monitoring failure must stop the profile and report restoration in the window.")]
    public void Tick(IProfileHost host)
    {
        if (ticking || host.IsClosing || !host.Initialized || host.DialogOpen)
            return;
        ticking = true;
        try
        {
            if (Session.Active)
            {
                Session.CheckLease();
                Session.Heartbeat();
            }

            if (host.Display.ModesDirty && !Session.Active)
            {
                host.ReloadModes(true);
                if (Monitoring && host.Display.GameResolution.SelectedItem == null)
                {
                    StopCore(host);
                    host.SetStatus("The saved resolution is unavailable at the current baseline Hz. Choose a supported mode and restart monitoring.");
                    return;
                }
            }

            if (Testing)
            {
                int remaining = (int)Math.Ceiling((testDeadline - DateTime.UtcNow).TotalSeconds);
                if (remaining <= 0)
                    StopProfile(host, "Test completed. Display settings restored.");
                else
                    host.SetStatus("Testing. Restoring in " + remaining + " seconds. Ctrl+Alt+F12 restores immediately.");
                return;
            }

            if (!Monitoring)
                return;
            Settings runtime = Runtime!;
            if (runtime.Targets().Count == 0)
            {
                if (Session.Active)
                {
                    string? error = Session.End();
                    if (error != null)
                        throw new InvalidOperationException(error);
                    host.ReloadModes(false);
                    host.UpdateControls();
                }

                host.SetStatus("Universe ID is not saved for this game. Edit the game while online and save it again.");
                return;
            }
            if (DateTime.UtcNow >= nextPoll)
            {
                lastObservation = watcher.Poll(runtime.LogPath());
                nextPoll = DateTime.UtcNow.AddMilliseconds(runtime.PollMilliseconds);
            }

            GameObservation? observation = lastObservation;
            if (observation == null)
                return;
            bool target = observation.IsTarget(runtime.Targets());
            if (Session.Active && (!target || observation.Pid != boundPid || observation.ProcessTicks != boundTicks || observation.Generation != boundGeneration))
            {
                string? error = Session.End();
                if (error != null)
                    throw new InvalidOperationException(error);
                host.ReloadModes(false);
                host.UpdateControls();
            }

            if (!Session.Active && target && Native.ForegroundPid() == observation.Pid)
            {
                MonitorInfo monitor = host.Display.SelectedMonitor();
                DisplayMode live = host.Display.ReadCurrent(monitor.Device);
                if (runtime.AutoBase && live.Refresh != (host.Display.SelectedRefresh() ?? 0))
                    throw new InvalidOperationException("The baseline refresh rate changed. Review the supported modes before restarting monitoring.");
                Session.Begin(runtime, monitor);
                boundPid = observation.Pid;
                boundTicks = observation.ProcessTicks;
                boundGeneration = observation.Generation;
                host.Display.Actual.Text = "Session baseline: " + Session.Data!.RestoreMode;
                host.UpdateControls();
            }

            if (Session.Active)
            {
                bool focused = Native.ForegroundPid() == boundPid;
                Session.SetResolution(!runtime.ResolutionOnFocusOnly || focused);
                Session.SetColors(focused);
                host.SetStatus(focused
                    ? host.Settings.SelectedGame().Name + " profile active: " + Session.Data!.GameMode + "\nSaturation: " +
                        (runtime.SaturationEnabled ? Math.Round(runtime.Saturation * 100) + "%" : "disabled")
                    : "Game in background. Colors restored. " + (runtime.ResolutionOnFocusOnly ? "Resolution restored." : "Resolution stays stretched."));
            }
            else
            {
                host.SetStatus(observation.Detail + (target ? "\nFocus your game to activate the profile." : ""));
            }
        }
        catch (Exception ex)
        {
            host.ReportError(ex);
        }
        finally
        {
            ticking = false;
        }
    }
}
