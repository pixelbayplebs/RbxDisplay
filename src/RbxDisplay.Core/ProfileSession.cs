using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;

namespace RbxDisplay;

internal sealed class ProfileSession
{
    private Process? watchdog;

    public SessionData? Data { get; private set; }

    public bool Active => Data != null;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed start must roll the journal back before the original error is reported.")]
    public void Begin(Settings settings, MonitorInfo monitor)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(monitor);
        if (Active || File.Exists(Store.SessionPath))
            throw new InvalidOperationException("The previous session must be restored first.");
        string watchdogPath = Path.Combine(AppContext.BaseDirectory, "RbxDisplay.Watchdog.exe");
        if (!File.Exists(watchdogPath))
            throw new InvalidOperationException("RbxDisplay.Watchdog.exe is missing. Extract the whole RbxDisplay folder before starting a profile.");
        DisplayMode original = Native.Current(monitor.Device);
        DisplayMode game = settings.ProfileMode(original);
        DisplayMode restore = settings.AutoBase ? original : Rules.Desired(original, settings.BaseWidth, settings.BaseHeight, settings.BaseRefresh);
        Native.ValidateMode(monitor.Device, game);
        Native.ValidateMode(monitor.Device, restore);
        if (string.IsNullOrWhiteSpace(monitor.Identity))
            throw new InvalidOperationException("Cannot record the monitor identity.");
        float[]? color = settings.SaturationEnabled ? Native.ReadColor() : null;
        using (Process current = Process.GetCurrentProcess())
        {
            Data = new SessionData
            {
                Token = Guid.NewGuid().ToString("N"),
                MonitorDevice = monitor.Device,
                MonitorIdentity = monitor.Identity,
                ParentPid = current.Id,
                ParentTicks = current.StartTime.ToUniversalTime().Ticks,
                OriginalMode = original,
                RestoreMode = restore,
                GameMode = game,
                LastOwnedMode = original,
                OriginalColor = color,
                GameColor = color == null ? null : Rules.Saturated(color, settings.Saturation)
            };
        }

        try
        {
            Store.Save(Store.SessionPath, Data);
            Heartbeat();
            watchdog = Process.Start(new ProcessStartInfo(watchdogPath, "--watchdog " + Data.Token)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory
            });
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (!File.Exists(Store.PathFor("ready", Data.Token)) && DateTime.UtcNow < deadline)
            {
                if (watchdog == null || watchdog.HasExited)
                    throw new InvalidOperationException("The watchdog process could not start.");
                Thread.Sleep(50);
            }

            if (!File.Exists(Store.PathFor("ready", Data.Token)))
                throw new InvalidOperationException("The watchdog process did not confirm readiness.");
            CheckLease();
            Store.Log("Session ready; baseline " + original + ", profile " + game + ".");
        }
        catch
        {
            End();
            throw;
        }
    }

    public void Heartbeat()
    {
        if (Active)
            Store.Atomic(Store.PathFor("heartbeat", Data!.Token), DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
    }

    public void CheckLease()
    {
        if (!Active)
            return;
        if (File.Exists(Store.PathFor("stop", Data!.Token)) || !File.Exists(Store.SessionPath))
            throw new InvalidOperationException("The watchdog restored the session. Monitoring has stopped.");
        if (watchdog == null || watchdog.HasExited)
            throw new InvalidOperationException("The watchdog process stopped. Monitoring has stopped.");
    }

    public void SetResolution(bool enabled)
    {
        // Journal + display mutation form one operation relative to watchdog recovery.
        using (RecoveryLock.Acquire())
            SetResolutionLocked(enabled);
    }

    private void SetResolutionLocked(bool enabled)
    {
        CheckLease();
        SessionData data = Data!;
        DisplayMode current = Native.Current(data.MonitorDevice);
        string? identity = new WindowsDesktop().Identity(data.MonitorDevice);
        if (identity != data.MonitorIdentity)
            throw new InvalidOperationException("The monitor changed. The profile has stopped.");
        DisplayMode? expected = data.LastOwnedMode;
        if (!Rules.SameMode(current, expected))
            throw new InvalidOperationException("The resolution changed outside RbxDisplay. The profile has stopped to preserve your change.");
        DisplayMode? desired = enabled ? data.GameMode : data.RestoreMode;
        if (Rules.SameMode(current, desired))
            return;
        if (enabled)
        {
            data.ModeArmed = true;
            data.LastOwnedMode = desired;
            Store.Save(Store.SessionPath, data);
            CheckLease();
            Native.SetMode(data.MonitorDevice, desired);
        }
        else
        {
            Native.SetMode(data.MonitorDevice, desired);
            data.ModeArmed = false;
            data.LastOwnedMode = desired;
            Store.Save(Store.SessionPath, data);
        }
    }

    public void SetColors(bool enabled)
    {
        if (Data!.GameColor == null)
            return;
        using (RecoveryLock.Acquire())
            SetColorsLocked(enabled);
    }

    private void SetColorsLocked(bool enabled)
    {
        CheckLease();
        SessionData data = Data!;
        float[] current = Native.ReadColor();
        float[]? expected = data.ColorArmed ? data.GameColor : data.OriginalColor;
        if (!Rules.SameColor(current, expected))
            throw new InvalidOperationException("Another tool changed the color transform. The profile has stopped.");
        float[]? desired = enabled ? data.GameColor : data.OriginalColor;
        if (Rules.SameColor(current, desired))
            return;
        if (enabled)
        {
            data.ColorArmed = true;
            Store.Save(Store.SessionPath, data);
            CheckLease();
            Native.SetColor(desired);
            if (!Rules.SameColor(Native.ReadColor(), desired))
                throw new InvalidOperationException("Windows did not confirm the saturation transform.");
        }
        else
        {
            Native.SetColor(desired);
            data.ColorArmed = false;
            Store.Save(Store.SessionPath, data);
        }
    }

    public void Retarget(Settings settings, MonitorInfo monitor, bool resolutionEnabled, bool colorsEnabled)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(monitor);
        if (!Active)
            throw new InvalidOperationException("No profile session is active.");
        SessionData current = Data!;
        bool sameMonitor = string.Equals(monitor.Device, current.MonitorDevice, StringComparison.Ordinal)
            && string.Equals(monitor.Identity, current.MonitorIdentity, StringComparison.Ordinal);
        if (!sameMonitor)
        {
            string? error = End();
            if (error != null)
                throw new InvalidOperationException(error);
            Begin(settings, monitor);
            SetResolution(resolutionEnabled);
            SetColors(colorsEnabled);
            return;
        }

        using (RecoveryLock.Acquire())
            RetargetLocked(settings, resolutionEnabled, colorsEnabled);
    }

    private void RetargetLocked(Settings settings, bool resolutionEnabled, bool colorsEnabled)
    {
        CheckLease();
        SessionData data = Data!;
        if (data.OriginalMode == null || string.IsNullOrWhiteSpace(data.MonitorDevice))
            throw new InvalidOperationException("The session is missing its baseline.");
        string? identity = new WindowsDesktop().Identity(data.MonitorDevice);
        if (identity != data.MonitorIdentity)
            throw new InvalidOperationException("The monitor changed. The profile has stopped.");
        DisplayMode currentMode = Native.Current(data.MonitorDevice);
        if (!Rules.SameMode(currentMode, data.LastOwnedMode))
            throw new InvalidOperationException("The resolution changed outside RbxDisplay. The profile has stopped to preserve your change.");

        DisplayMode game = settings.ProfileMode(data.OriginalMode);
        DisplayMode restore = settings.AutoBase ? data.OriginalMode : Rules.Desired(data.OriginalMode, settings.BaseWidth, settings.BaseHeight, settings.BaseRefresh);
        Native.ValidateMode(data.MonitorDevice, game);
        Native.ValidateMode(data.MonitorDevice, restore);

        float[]? previousColor = data.GameColor;
        bool colorWasArmed = data.ColorArmed;
        if (previousColor != null || colorWasArmed)
        {
            float[] screen = Native.ReadColor();
            float[]? expected = colorWasArmed ? previousColor : data.OriginalColor;
            if (expected == null || !Rules.SameColor(screen, expected))
                throw new InvalidOperationException("Another tool changed the color transform. The profile has stopped.");
        }

        // Keep the snapshot from session start. A stretched mode must not become the new baseline.
        data.GameMode = game;
        data.RestoreMode = restore;
        Store.Save(Store.SessionPath, data);
        CheckLease();
        SetResolutionLocked(resolutionEnabled);

        if (settings.SaturationEnabled)
        {
            if (data.OriginalColor == null)
            {
                if (colorWasArmed)
                    throw new InvalidOperationException("The session color state is incomplete.");
                data.OriginalColor = Native.ReadColor();
            }

            data.GameColor = Rules.Saturated(data.OriginalColor, settings.Saturation);
            ApplyColor(colorsEnabled, previousColor, colorWasArmed);
            return;
        }

        if (colorWasArmed && data.OriginalColor != null)
        {
            Native.SetColor(data.OriginalColor);
            if (!Rules.SameColor(Native.ReadColor(), data.OriginalColor))
                throw new InvalidOperationException("Windows did not confirm the saturation transform.");
        }

        data.GameColor = null;
        data.ColorArmed = false;
        Store.Save(Store.SessionPath, data);
    }

    private void ApplyColor(bool enabled, float[]? previousGameColor, bool wasArmed)
    {
        SessionData data = Data!;
        float[]? desired = enabled ? data.GameColor : data.OriginalColor;
        float[] screen = Native.ReadColor();
        float[]? expected = wasArmed ? previousGameColor : data.OriginalColor;
        if (expected != null && !Rules.SameColor(screen, expected) && !Rules.SameColor(screen, desired))
            throw new InvalidOperationException("Another tool changed the color transform. The profile has stopped.");
        if (desired == null)
            return;
        if (Rules.SameColor(screen, desired))
        {
            data.ColorArmed = enabled;
            Store.Save(Store.SessionPath, data);
            return;
        }

        if (enabled)
        {
            data.ColorArmed = true;
            Store.Save(Store.SessionPath, data);
            CheckLease();
            Native.SetColor(data.GameColor!);
            if (!Rules.SameColor(Native.ReadColor(), data.GameColor))
                throw new InvalidOperationException("Windows did not confirm the saturation transform.");
            return;
        }

        Native.SetColor(desired);
        data.ColorArmed = false;
        Store.Save(Store.SessionPath, data);
    }

    public string? End()
    {
        if (!Active)
            return null;
        string? token = Data!.Token;
        string? error = Recovery.FromJournal(token);
        if (error == null)
            Store.Delete(Store.PathFor("stop", token));
        Data = null;
        if (watchdog != null)
        {
            watchdog.Dispose();
            watchdog = null;
        }

        return error;
    }
}
