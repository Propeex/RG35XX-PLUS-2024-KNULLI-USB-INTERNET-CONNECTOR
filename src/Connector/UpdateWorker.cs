using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Runs from a temporary COPY of the old EXE, so the actual target EXE is
/// not locked. Launches a verified replacement and retains a backup.
/// All operations use the same Windows user (no elevation).
/// </summary>
internal static class UpdateWorker
{
    internal static void Apply(string[] args)
    {
        string logFile = Path.Combine(AppPaths.BaseDirectory, "updater-last.log");
        try
        {
            if (args.Length != 5 || !int.TryParse(args[1], out int oldPid) || oldPid <= 0)
                throw new ArgumentException("Invalid updater arguments.");

            string target = Path.GetFullPath(args[2]);
            string downloaded = Path.GetFullPath(args[3]);
            string expectedSha = args[4];
            string downloadDir = Path.GetFullPath(Path.Combine(AppPaths.BaseDirectory, "updates"));
            string downloadPrefix = downloadDir + Path.DirectorySeparatorChar;
            if (!string.Equals(Path.GetFileName(target), "RG35XXUsbInternetConnector.exe",
                    StringComparison.OrdinalIgnoreCase)
                || !downloaded.StartsWith(downloadPrefix, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetExtension(downloaded), ".exe",
                    StringComparison.OrdinalIgnoreCase)
                || expectedSha.Length != 64 || !expectedSha.All(Uri.IsHexDigit))
                throw new InvalidOperationException("Updater target validation failed.");

            try
            {
                using var old = Process.GetProcessById(oldPid);
                if (!old.WaitForExit(30000))
                    throw new TimeoutException("Application did not exit before update.");
            }
            catch (ArgumentException)
            {
                // Parent is already gone, which is an expected success case.
            }

            // Revalidate before replacing, even though the parent already checked it.
            using (var file = File.OpenRead(downloaded))
            {
                var hash = SHA256.HashData(file);
                if (!CryptographicOperations.FixedTimeEquals(hash,
                        Convert.FromHexString(expectedSha)))
                    throw new InvalidDataException("Update SHA-256 mismatch.");
            }

            string backup = target + ".previous";
            // Keep exactly one previous working version for manual recovery.
            File.Copy(target, backup, overwrite: true);

            bool replaced = false;
            try
            {
                File.Move(downloaded, target, overwrite: true);
                replaced = true;
                using var relaunched = Process.Start(new ProcessStartInfo(target)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(target)!
                }) ?? throw new InvalidOperationException("Could not relaunch updated EXE.");
                File.WriteAllText(logFile, "Update applied and updated EXE launched.\n");
            }
            catch
            {
                // If the new process failed to start, restore the known-good EXE.
                if (replaced) File.Copy(backup, target, overwrite: true);
                throw;
            }
        }
        catch (Exception ex)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.BaseDirectory);
                File.WriteAllText(logFile, "Update failed: " + ex.GetType().Name +
                    " — " + ex.Message + Environment.NewLine);
            }
            catch { /* cannot show a console from a WinExe worker */ }
            // The original EXE remains available for manual retry.
        }
    }
}
