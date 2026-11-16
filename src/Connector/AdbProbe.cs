using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Real ADB readiness probe. Uses an isolated, short-lived ADB server port and
/// requests shutdown in a finally block to avoid locking platform-tools.
/// Does not infer Internet Connected from ADB presence alone.
/// </summary>
internal static class AdbProbe
{
    internal const string ServerPort = "5056";

    internal static async Task<bool> HasReadyDeviceAsync(bool keepServer = false)
    {
        if (!File.Exists(AppPaths.AdbPath)) return false;
        try
        {
            string result = await RunAdbAsync("devices", "-l");
            return result.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Any(line => line.Contains("\tdevice", StringComparison.Ordinal));
        }
        finally
        {
            if (!keepServer)
            {
                try { await RunAdbAsync("kill-server"); }
                catch { /* no daemon to clean up, or already stopped */ }
            }
        }
    }

    private static async Task<string> RunAdbAsync(params string[] args)
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var start = new ProcessStartInfo(AppPaths.AdbPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add("-P");
        start.ArgumentList.Add(ServerPort);
        foreach (string item in args) start.ArgumentList.Add(item);
        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Unable to start adb.exe.");
        try
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync(cancel.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(cancel.Token);
            await process.WaitForExitAsync(cancel.Token);
            // Errors are read so the pipe cannot block the child process.
            string stdout = await output;
            _ = await error;
            if (process.ExitCode != 0) return string.Empty;
            return stdout;
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("ADB readiness check timed out.");
        }
    }
}
