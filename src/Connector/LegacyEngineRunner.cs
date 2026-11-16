using System;
using System.Diagnostics;
using System.Text;

namespace RG35XXUsbInternetConnector;

internal enum EngineAction { Start, Stop, Status }

internal sealed record EngineResult(bool Success, string DiagnosticLog, int ExitCode);

/// <summary>
/// Runs the preserved v1.6 PowerShell backend with NO visible console and with
/// application-managed Google adb.exe. This is a transitional, hardware-test
/// integration: underlying v1.6 automation has not yet passed real-device QA.
/// </summary>
internal sealed class LegacyEngineRunner
{
    private readonly SemaphoreSlim _actionGate = new(1, 1);

    internal async Task<EngineResult> RunAsync(
        EngineAction action, Action<string> log, CancellationToken cancellation)
    {
        if (!LegacyEngineBundle.Installed)
            throw new InvalidOperationException("No verified USB backend bundle is installed.");
        if (!File.Exists(AppPaths.AdbPath))
            throw new FileNotFoundException("Google adb.exe not installed.", AppPaths.AdbPath);

        await _actionGate.WaitAsync(cancellation);
        try
        {
            string token = action switch
            {
                EngineAction.Start => "start",
                EngineAction.Stop => "stop",
                EngineAction.Status => "status",
                _ => throw new ArgumentOutOfRangeException(nameof(action))
            };

            // The diagnostics are kept in Windows user data. The GUI never
            // displays the legacy backend's untranslated logs.
            string logs = Path.Combine(AppPaths.BaseDirectory, "backend-logs");
            Directory.CreateDirectory(logs);
            string logfile = Path.Combine(logs,
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + token + ".log");

            log("USB backend operation started: " + token);
            var psi = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = LegacyEngineBundle.Folder,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string arg in new[]
            {
                "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", LegacyEngineBundle.Script, "-Action", token, "-LogFile", logfile
            })
                psi.ArgumentList.Add(arg);

            // Keep app-managed ADB on the same isolated port as device polling.
            // Explicit STOP shuts down only this ADB server, not other tools on 5037.
            psi.Environment["RG35XX_ADB_EXE"] = AppPaths.AdbPath;
            psi.Environment["ADB_SERVER_PORT"] = AdbProbe.ServerPort;
            psi.Environment["RG35XX_GUI"] = "1";

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Could not start background PowerShell.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(action == EngineAction.Status
                ? TimeSpan.FromSeconds(75)
                : TimeSpan.FromMinutes(5));

            try
            {
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
                Task<string> errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                string stdout = await outputTask;
                string stderr = await errorTask;
                if (process.ExitCode == 0)
                {
                    log("USB backend operation succeeded: " + token);
                }
                else
                {
                    log("USB backend operation failed: " + token +
                        " (exit code " + process.ExitCode + ").");
                    log("Detailed technical log saved to " + logfile);
                    if (!string.IsNullOrWhiteSpace(stderr))
                        log("PowerShell exited with diagnostic stderr (see technical log).");
                }
                return new EngineResult(process.ExitCode == 0, logfile, process.ExitCode);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                log("USB backend action exceeded its deadline or was cancelled: " + token);
                throw;
            }
        }
        finally
        {
            _actionGate.Release();
        }
    }

    /// <summary>
    /// Native internet test, not a stale active flag. Uses adb port 5056 and
    /// the existing TUN. Keep the ADB daemon alive while SSH depends on it.
    /// </summary>
    internal static async Task<bool> NativeInternetHealthyAsync(CancellationToken cancellation)
    {
        if (!File.Exists(AppPaths.AdbPath)) return false;
        const string command = "test -e /userdata/system/rgusb-fullnet/active && " +
            "ip link show rgusb0 >/dev/null 2>&1 && " +
            "curl --noproxy '*' -fsSI --max-time 7 https://example.com >/dev/null 2>&1";
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(13));
            var psi = new ProcessStartInfo(AppPaths.AdbPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string arg in new[] { "-P", AdbProbe.ServerPort, "shell", command })
                psi.ArgumentList.Add(arg);
            using var process = Process.Start(psi);
            if (process is null) return false;
            Task<string> readOut = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> readErr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(readOut, readErr);
            return process.ExitCode == 0;
        }
        catch (Exception e) when (e is TimeoutException or IOException or OperationCanceledException)
        {
            return false;
        }
    }
}
