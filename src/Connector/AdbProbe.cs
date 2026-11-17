using System.Diagnostics;
using System.Threading;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Uses one shared ADB server (standard port 5037) for all new USB sessions.
/// Passive socket queries do not spawn a second server, and the GUI never
/// terminates shared ADB. A pre-existing legacy SSH tunnel can stay pinned
/// to port 5056 for safe manual disconnect.
/// </summary>
internal static class AdbProbe
{
    // ADB Explorer uses the standard 5037 daemon. Beginning with 5056 and
    // subsequently launching ADB Explorer created competing USB owners.
    // Always use a single shared 5037 server for new discovery and sessions.
    // A *pre-existing* live tunnel stays pinned to its persisted port so that
    // disconnect can still clean up a session created by an older build.
    private const string SharedPort = "5037";
    private const string LegacyPort = "5056";
    private static string _selectedPort = LoadPreviousTunnelPort();
    private static string _lastDiagnostic = "";
    private static bool _selectedServerPresent;
    private static DateTimeOffset _nextStartAttempt = DateTimeOffset.MinValue;

    private static readonly SemaphoreSlim DiscoveryGate = new(1, 1);
    private static readonly object TrackingGate = new();
    private static CancellationTokenSource? _trackingCancellation;
    private static Task? _trackingTask;
    private static string? _trackingPort;
    private static DateTimeOffset _nextTrackingAttempt = DateTimeOffset.MinValue;

    internal static string ServerPort => Volatile.Read(ref _selectedPort);
    internal static string LastDiagnostic => Volatile.Read(ref _lastDiagnostic);

    private static string LoadPreviousTunnelPort()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RG35XX_USB_FullNet");
        if (!File.Exists(Path.Combine(folder, "ssh.pid")))
            return SharedPort;

        try
        {
            string port = File.ReadAllText(Path.Combine(folder, "adb-port.txt")).Trim();
            return port is LegacyPort or SharedPort ? port : SharedPort;
        }
        catch (IOException) { return SharedPort; }
        catch (UnauthorizedAccessException) { return SharedPort; }
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

    // Never start a second ADB server when an old isolated 5056 daemon still
    // exists. We do not automatically terminate an unowned process.
    internal static bool CanStartShared(bool sharedPresent, bool legacyPresent,
        bool portPinned) => !sharedPresent && !legacyPresent && !portPinned;

    internal static string SelectDiscoveryPort(bool pinned, string persisted)
        => pinned && persisted == LegacyPort ? LegacyPort : SharedPort;

    internal static async Task<bool> HasReadyDeviceAsync(
        bool keepServer = false, bool allowFallback = true)
    {
        await DiscoveryGate.WaitAsync().ConfigureAwait(false);
        try
        {
            bool pinned = keepServer || !allowFallback;
            string chosen = SelectDiscoveryPort(pinned, ServerPort);
            if (pinned)
            {
                // Never auto-start or switch away from the server that owns
                // the active USB forward (including a legacy 5056 session).
                var existing = await AdbServerProtocol.QueryAsync(chosen).ConfigureAwait(false);
                Volatile.Write(ref _selectedPort, chosen);
                Volatile.Write(ref _selectedServerPresent, existing.ServerPresent);
                Volatile.Write(ref _lastDiagnostic, existing.Ready
                    ? DescribeReady(chosen)
                    : $"ADB server {chosen}: {DescribeState(existing)} (tunnel port pinned).");
                return existing.Ready;
            }

            // Passive socket queries do not launch adb.exe or contend for USB.
            var shared = await AdbServerProtocol.QueryAsync(SharedPort).ConfigureAwait(false);
            if (!shared.ServerPresent)
            {
                var legacy = await AdbServerProtocol.QueryAsync(LegacyPort).ConfigureAwait(false);
                if (legacy.ServerPresent)
                {
                    Volatile.Write(ref _selectedPort, SharedPort);
                    Volatile.Write(ref _selectedServerPresent, false);
                    Volatile.Write(ref _lastDiagnostic,
                        "Legacy ADB server 5056 is still running. " +
                        "Close the previous connector instance; refusing to start competing 5037.");
                    return false;
                }

                if (CanStartShared(shared.ServerPresent, legacy.ServerPresent, pinned) &&
                    DateTimeOffset.UtcNow >= _nextStartAttempt)
                {
                    // One shared daemon, regardless of which app starts first.
                    _nextStartAttempt = DateTimeOffset.UtcNow.AddSeconds(8);
                    await StartSharedAsync().ConfigureAwait(false);
                    shared = await AdbServerProtocol.QueryAsync(SharedPort).ConfigureAwait(false);
                }
            }

            Volatile.Write(ref _selectedPort, SharedPort);
            Volatile.Write(ref _selectedServerPresent, shared.ServerPresent);
            Volatile.Write(ref _lastDiagnostic, shared.Ready
                ? DescribeReady(SharedPort)
                : $"ADB server {SharedPort}: {DescribeState(shared)}. " +
                  "Waiting for KNULLI USB ADB (USB mode reset not requested).");
            return shared.Ready;
        }
        finally
        {
            DiscoveryGate.Release();
        }
    }

    private static string DescribeReady(string port)
        => "ADB device ready (server " + port +
            (port == SharedPort ? ", shared with ADB Explorer" : ", existing legacy session") + ").";

    private static string DescribeState(AdbServerProtocol.Snapshot result)
        => result.ServerPresent ? result.State : "not running";

    private static async Task StartSharedAsync()
    {
        if (!File.Exists(AppPaths.AdbPath))
        {
            Volatile.Write(ref _lastDiagnostic, "Official Google ADB is not installed.");
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(7));
        var info = new ProcessStartInfo(AppPaths.AdbPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in new[] { "-P", SharedPort, "start-server" })
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
        // Even if we were first to start 5037, it is now shared. Closing
        // KNULLI USB Link must not kill a server relied upon by ADB Explorer.
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
                    or ObjectDisposedException or InvalidDataException) { }
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
