using System.Diagnostics;
using System.Threading;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Probes app-dedicated port 5056 first, then the standard shared port 5037
/// when needed. Parsing accepts tabs OR spaces in ADB devices -l output.
/// The chosen port is reused by SSH/PowerShell; never kill shared 5037.
/// Never include a device serial in diagnostic logs.
/// </summary>
internal static class AdbProbe
{
    private const string DedicatedPort = "5056";
    private const string SharedPort = "5037";
    private static string _selectedPort = LoadPreviousTunnelPort();

    private static string LoadPreviousTunnelPort()
    {
        string state = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RG35XX_USB_FullNet");
        if (!File.Exists(Path.Combine(state, "ssh.pid")))
            return DedicatedPort;
        try
        {
            string port = File.ReadAllText(Path.Combine(state, "adb-port.txt")).Trim();
            return port is DedicatedPort or SharedPort ? port : DedicatedPort;
        }
        catch (IOException) { return DedicatedPort; }
        catch (UnauthorizedAccessException) { return DedicatedPort; }
    }
    private static string _lastDiagnostic = "";

    internal static string ServerPort => Volatile.Read(ref _selectedPort);
    internal static string LastDiagnostic => Volatile.Read(ref _lastDiagnostic);

    internal static bool HasReadyDevice(string listing)
    {
        return ParseState(listing) == "device";
    }

    private static string ParseState(string listing)
    {
        string result = "none";
        foreach (string line in listing.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 2) continue;
            string state = columns[1].Trim();
            if (state == "device") return "device";
            if (state == "unauthorized") result = "unauthorized";
            if (state == "offline" && result != "unauthorized") result = "offline";
        }
        return result;
    }

    private sealed record ProbeResult(bool Ready, string State);

    private static async Task<ProbeResult> QueryAsync(string port)
    {
        if (!File.Exists(AppPaths.AdbPath)) return new ProbeResult(false, "ADB not installed");

        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(9));
        var start = new ProcessStartInfo(AppPaths.AdbPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("-P");
        start.ArgumentList.Add(port);
        start.ArgumentList.Add("devices");
        start.ArgumentList.Add("-l");

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Unable to start official adb.exe.");
        try
        {
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancel.Token);
            Task<string> stderr = process.StandardError.ReadToEndAsync(cancel.Token);
            await process.WaitForExitAsync(cancel.Token);
            string listing = await stdout;
            _ = await stderr; // do not log a raw serial or private path
            if (process.ExitCode != 0)
                return new ProbeResult(false, "adb returned exit code " + process.ExitCode);
            string state = ParseState(listing);
            return new ProbeResult(state == "device", state);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new ProbeResult(false, "adb timed out");
        }
    }

    private static async Task StopDedicatedAsync()
    {
        if (!File.Exists(AppPaths.AdbPath)) return;
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var start = new ProcessStartInfo(AppPaths.AdbPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in new[] { "-P", DedicatedPort, "kill-server" })
            start.ArgumentList.Add(arg);
        try
        {
            using var proc = Process.Start(start);
            if (proc is null) return;
            Task<string> output = proc.StandardOutput.ReadToEndAsync(cancel.Token);
            Task<string> error = proc.StandardError.ReadToEndAsync(cancel.Token);
            await proc.WaitForExitAsync(cancel.Token);
            await Task.WhenAll(output, error);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException
                or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // ADB may already have exited; a failed stop must not crash the GUI.
        }
    }

    internal static async Task<bool> HasReadyDeviceAsync(bool keepServer = false)
    {
        string preferred = ServerPort;
        var first = await QueryAsync(preferred);
        if (first.Ready)
        {
            if (!keepServer && preferred == DedicatedPort)
                await StopDedicatedAsync();
            Volatile.Write(ref _lastDiagnostic,
                "ADB device ready (server " + preferred +
                (preferred == SharedPort ? ", shared)" : ", app-owned)"));
            return true;
        }

        if (!keepServer && preferred == DedicatedPort)
            await StopDedicatedAsync();

        string alternative = preferred == DedicatedPort ? SharedPort : DedicatedPort;
        var second = await QueryAsync(alternative);
        if (second.Ready)
        {
            Volatile.Write(ref _selectedPort, alternative);
            if (alternative == DedicatedPort && !keepServer)
                await StopDedicatedAsync();
            Volatile.Write(ref _lastDiagnostic,
                "ADB device ready (server " + alternative +
                (alternative == SharedPort ? ", shared; reused existing ADB)" : ", app-owned)"));
            return true;
        }
        if (alternative == DedicatedPort && !keepServer)
            await StopDedicatedAsync();

        string reason = first.State == "unauthorized" || second.State == "unauthorized"
            ? "ADB device unauthorized. Check device authorization."
            : first.State == "offline" || second.State == "offline"
                ? "ADB device offline. Check USB cable and KNULLI ADB mode."
                : "No authorized ADB device on ports 5056 or 5037. Check KNULLI USB ADB mode, Windows driver, and cable.";
        Volatile.Write(ref _lastDiagnostic, reason);
        return false;
    }
}
