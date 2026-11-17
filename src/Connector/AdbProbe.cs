using System.Diagnostics;
using System.Threading;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Discovers an existing ADB server WITHOUT starting a second server during
/// "devices" checks. The app starts its isolated 5056 daemon only if neither
/// 5037 nor 5056 is listening. One selected server is used by all USB actions.
/// Never stop a shared 5037 server or an unowned 5056 server.
/// </summary>
internal static class AdbProbe
{
    private const string DedicatedPort = "5056";
    private const string SharedPort = "5037";
    private static string _selectedPort = LoadPreviousTunnelPort();
    private static string _lastDiagnostic = "";
    private static bool _selectedServerPresent;
    private static bool _ownsDedicated;
    private static DateTimeOffset _nextStartAttempt = DateTimeOffset.MinValue;

    private static readonly SemaphoreSlim DiscoveryGate = new(1, 1);
    private static readonly object TrackingGate = new();
    private static CancellationTokenSource? _trackingCancellation;
    private static Task? _trackingTask;
    private static string? _trackingPort;
    private static DateTimeOffset _nextTrackingAttempt = DateTimeOffset.MinValue;

    internal static string ServerPort => Volatile.Read(ref _selectedPort);
    internal static string LastDiagnostic => Volatile.Read(ref _lastDiagnostic);
    internal static bool OwnsDedicatedServer => Volatile.Read(ref _ownsDedicated);

    private static string LoadPreviousTunnelPort()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RG35XX_USB_FullNet");
        if (!File.Exists(Path.Combine(folder, "ssh.pid")))
            return DedicatedPort;
        try
        {
            string port = File.ReadAllText(Path.Combine(folder, "adb-port.txt")).Trim();
            return port is DedicatedPort or SharedPort ? port : DedicatedPort;
        }
        catch (IOException) { return DedicatedPort; }
        catch (UnauthorizedAccessException) { return DedicatedPort; }
    }

    internal static bool HasReadyDevice(string listing)
        => ParseState(listing) == "device";

    internal static string ParseState(string listing)
    {
        int ready = 0;
        string state = "none";
        foreach (string line in listing.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 2) continue;
            string value = columns[1].Trim();
            if (value == "device") ready++;
            else if (value == "unauthorized") state = "unauthorized";
            else if (value == "offline" && state != "unauthorized") state = "offline";
        }
        if (ready > 1) return "multiple";
        return ready == 1 ? "device" : state;
    }

    // Never create a second USB-claiming server to "look for" a device
    // already potentially owned by another ADB server.
    internal static bool CanStartDedicated(bool dedicatedPresent, bool sharedPresent,
        bool portPinned) => !dedicatedPresent && !sharedPresent && !portPinned;

    internal static async Task<bool> HasReadyDeviceAsync(
        bool keepServer = false, bool allowFallback = true)
    {
        await DiscoveryGate.WaitAsync().ConfigureAwait(false);
        try
        {
            bool pinned = keepServer || !allowFallback;
            string chosen = ServerPort;
            if (pinned)
            {
                var existing = await AdbServerProtocol.QueryAsync(chosen).ConfigureAwait(false);
                Volatile.Write(ref _selectedServerPresent, existing.ServerPresent);
                Volatile.Write(ref _lastDiagnostic, existing.Ready
                    ? DescribeReady(chosen)
                    : $"ADB server {chosen}: {DescribeState(existing)} (tunnel port pinned).");
                return existing.Ready;
            }

            // Observe both existing ports with passive TCP host:devices-l.
            // This does not launch adb.exe, unlike "adb devices -l".
            var dedicated = await AdbServerProtocol.QueryAsync(DedicatedPort).ConfigureAwait(false);
            var shared = await AdbServerProtocol.QueryAsync(SharedPort).ConfigureAwait(false);

            // Another application opened the shared server while the GUI was
            // idle. Retire only our own dedicated server before selecting
            // shared, so USB is not contested by two server processes.
            if (shared.ServerPresent && dedicated.ServerPresent && OwnsDedicatedServer)
            {
                await StopDedicatedCoreAsync().ConfigureAwait(false);
                dedicated = await AdbServerProtocol.QueryAsync(DedicatedPort).ConfigureAwait(false);
                shared = await AdbServerProtocol.QueryAsync(SharedPort).ConfigureAwait(false);
            }

            string selected;
            AdbServerProtocol.Snapshot result;
            if (shared.Ready)
            {
                selected = SharedPort;
                result = shared;
            }
            else if (dedicated.Ready)
            {
                selected = DedicatedPort;
                result = dedicated;
            }
            else if (shared.ServerPresent)
            {
                selected = SharedPort;
                result = shared;
            }
            else if (dedicated.ServerPresent)
            {
                selected = DedicatedPort;
                result = dedicated;
            }
            else if (CanStartDedicated(dedicated.ServerPresent, shared.ServerPresent, pinned))
            {
                selected = DedicatedPort;
                if (DateTimeOffset.UtcNow >= _nextStartAttempt)
                {
                    _nextStartAttempt = DateTimeOffset.UtcNow.AddSeconds(8);
                    await StartDedicatedAsync().ConfigureAwait(false);
                }
                result = await AdbServerProtocol.QueryAsync(DedicatedPort).ConfigureAwait(false);
            }
            else
            {
                selected = chosen;
                result = new(false, "not running", false);
            }

            Volatile.Write(ref _selectedPort, selected);
            Volatile.Write(ref _selectedServerPresent, result.ServerPresent);
            Volatile.Write(ref _lastDiagnostic, result.Ready
                ? DescribeReady(selected)
                : $"ADB server {selected}: {DescribeState(result)}. " +
                  "Waiting for KNULLI USB ADB; no device USB reset was performed.");
            return result.Ready;
        }
        finally
        {
            DiscoveryGate.Release();
        }
    }

    private static string DescribeReady(string port)
        => "ADB device ready (server " + port +
            (port == SharedPort ? ", existing shared)" : ", dedicated)") + ".";

    private static string DescribeState(AdbServerProtocol.Snapshot result)
        => result.ServerPresent ? result.State : "not running";

    private static async Task StartDedicatedAsync()
    {
        if (!File.Exists(AppPaths.AdbPath))
        {
            Volatile.Write(ref _lastDiagnostic, "Official Google ADB is not installed.");
            return;
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(7));
        var start = new ProcessStartInfo(AppPaths.AdbPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in new[] { "-P", DedicatedPort, "start-server" })
            start.ArgumentList.Add(arg);

        using Process? child = Process.Start(start);
        if (child is null) return;
        try
        {
            Task<string> output = child.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> error = child.StandardError.ReadToEndAsync(timeout.Token);
            await child.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await Task.WhenAll(output, error).ConfigureAwait(false);
            // Only a server launched in this GUI lifetime may be stopped by
            // our GUI. Adopted or shared servers are deliberately left alone.
            if (child.ExitCode == 0)
                Volatile.Write(ref _ownsDedicated, true);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!child.HasExited) child.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
    }

    internal static async Task StopDedicatedAsync()
    {
        await DiscoveryGate.WaitAsync().ConfigureAwait(false);
        try { await StopDedicatedCoreAsync().ConfigureAwait(false); }
        finally { DiscoveryGate.Release(); }
    }

    private static async Task StopDedicatedCoreAsync()
    {
        if (!OwnsDedicatedServer || !File.Exists(AppPaths.AdbPath)) return;
        // Detach tracker before taking down its server.
        StopTracking();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var info = new ProcessStartInfo(AppPaths.AdbPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in new[] { "-P", DedicatedPort, "kill-server" })
            info.ArgumentList.Add(arg);
        try
        {
            using Process? child = Process.Start(info);
            if (child is null) return;
            try
            {
                Task<string> output = child.StandardOutput.ReadToEndAsync(timeout.Token);
                Task<string> error = child.StandardError.ReadToEndAsync(timeout.Token);
                await child.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                await Task.WhenAll(output, error).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!child.HasExited) child.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
        catch (System.ComponentModel.Win32Exception) { }
        catch (IOException) { }
        finally
        {
            Volatile.Write(ref _ownsDedicated, false);
            if (ServerPort == DedicatedPort)
                Volatile.Write(ref _selectedServerPresent, false);
        }
    }

    /// <summary>
    /// Subscribe to events from an already-running selected server.
    /// The native host:track-devices stream has no adb.exe child process.
    /// </summary>
    internal static void EnsureTracking(Action onChanged)
    {
        if (!Volatile.Read(ref _selectedServerPresent)) return;
        string port = ServerPort;
        lock (TrackingGate)
        {
            if (_trackingPort == port && _trackingTask is { IsCompleted: false })
                return;
            // If an external ADB server rejects tracking, do not spin in
            // immediate restart cycles; the timer will retry later.
            if (_trackingPort == port && DateTimeOffset.UtcNow < _nextTrackingAttempt)
                return;
            _nextTrackingAttempt = DateTimeOffset.UtcNow.AddSeconds(4);
            _trackingCancellation?.Cancel();
            _trackingCancellation?.Dispose();

            var cancellation = new CancellationTokenSource();
            _trackingCancellation = cancellation;
            _trackingPort = port;
            _trackingTask = Task.Run(async () =>
            {
                try
                {
                    await AdbServerProtocol.TrackAsync(port, onChanged,
                        cancellation.Token);
                }
                catch (Exception ex) when (ex is OperationCanceledException
                    or IOException or System.Net.Sockets.SocketException
                    or ObjectDisposedException) { }
                finally
                {
                    if (!cancellation.IsCancellationRequested)
                        onChanged(); // lost server; re-discover without USB reset
                }
            });
        }
    }

    internal static void StopTracking()
    {
        lock (TrackingGate)
        {
            _trackingCancellation?.Cancel();
            _trackingCancellation?.Dispose();
            _trackingCancellation = null;
            _trackingTask = null;
            _trackingPort = null;
            _nextTrackingAttempt = DateTimeOffset.MinValue;
        }
    }
}
