using System;
using System.Diagnostics;
using System.Text;

namespace RG35XXUsbInternetConnector;

internal enum EngineAction { Start, Stop, Status }

internal sealed record EngineResult(bool Success, string DiagnosticLog, int ExitCode);

/// <summary>
/// Runs the source-tracked USB PowerShell backend with NO visible console and with
/// application-managed Google adb.exe. This is a transitional, hardware-test
/// integration: underlying v1.6 automation has not yet passed real-device QA.
/// </summary>
internal sealed class UsbEngineRunner
{
    private readonly SemaphoreSlim _actionGate = new(1, 1);

    internal async Task<EngineResult> RunAsync(
        EngineAction action, Action<string> log, CancellationToken cancellation)
    {
        if (!UsbBackendBundle.Installed)
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

            // The diagnostics are kept in Windows user data. The GUI receives bounded, English-only activity logs.
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
                WorkingDirectory = UsbBackendBundle.Folder,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string arg in new[]
            {
                "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass",
                "-File", UsbBackendBundle.Script, "-Action", token, "-LogFile", logfile
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
                // Stream bounded English log lines as they are written. Waiting
                // until process exit made a slow SSH/TUN start look frozen.
                Task completed = process.WaitForExitAsync(timeout.Token);
                int nextLine = 0;
                while (!completed.IsCompleted)
                {
                    nextLine = StreamNewLogLines(logfile, nextLine, log);
                    await Task.WhenAny(completed, Task.Delay(400, timeout.Token));
                }
                await completed;
                nextLine = StreamNewLogLines(logfile, nextLine, log);
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

    private static int StreamNewLogLines(string file, int nextLine, Action<string> log)
    {
        try
        {
            if (!File.Exists(file)) return nextLine;
            // The log is small and only one writer exists for this action.
            // Reopening it allows us to tolerate temporary Windows file locks.
            foreach (string line in File.ReadLines(file).Skip(nextLine).Take(200))
            {
                nextLine++;
                if (!string.IsNullOrWhiteSpace(line) && line.Length < 1200
                    && !line.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase))
                    log(line);
            }
        }
        catch (IOException) { /* Retry on the next tick if PowerShell owns the file. */ }
        return nextLine;
    }

    /// <summary>
    /// Native Internet must be proven by an explicit shell output marker:
    /// older adbd versions can report exit 0 for a failing remote shell test.
    /// Never trust a stale active flag or adb process exit code alone.
    /// </summary>
    internal static bool NativeProbeSucceeded(int exitCode, string output)
        => exitCode == 0 && output.Trim() == "RGUSB_NATIVE_OK";

    internal static async Task<bool> NativeInternetHealthyAsync(CancellationToken cancellation)
    {
        if (!File.Exists(AppPaths.AdbPath)) return false;
        const string command = "if test -e /userdata/system/rgusb-fullnet/active && " +
            "ip link show rgusb0 >/dev/null 2>&1 && " +
            "curl --noproxy '*' -fsSI --max-time 7 https://example.com >/dev/null 2>&1; " +
            "then echo RGUSB_NATIVE_OK; else echo RGUSB_NATIVE_FAIL; fi";
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
            try
            {
                Task<string> readOut = process.StandardOutput.ReadToEndAsync(timeout.Token);
                Task<string> readErr = process.StandardError.ReadToEndAsync(timeout.Token);
                await process.WaitForExitAsync(timeout.Token);
                string stdout = await readOut;
                await readErr;
                return NativeProbeSucceeded(process.ExitCode, stdout);
            }
            catch (OperationCanceledException)
            {
                // Dispose() does not terminate a running adb.exe. Reap the
                // timed-out probe so repeated 15-second checks cannot pile up.
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None);
                }
                catch (InvalidOperationException) { /* already exited */ }
                catch (System.ComponentModel.Win32Exception) { /* process already gone */ }
                return false;
            }
        }
        catch (Exception e) when (e is TimeoutException or IOException or OperationCanceledException
            or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
