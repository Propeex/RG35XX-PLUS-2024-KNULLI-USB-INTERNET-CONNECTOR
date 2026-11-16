using System.Diagnostics;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// A temporary copy of the old EXE applies a verified portable update after
/// the parent exits. The program EXE/backend are replaced as one logical
/// transaction; Google Platform-Tools and user data are never touched.
/// Failed installs restore the old EXE/backend and keep a backup.
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
            string stage = Path.GetFullPath(args[3]);
            string exeHash = args[4];
            if (!string.Equals(Path.GetFileName(target), PortableUpdatePackage.Executable,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Updater target must be the connector EXE.");
            string root = Path.GetDirectoryName(target)
                ?? throw new InvalidOperationException("Updater root is missing.");
            PortableUpdatePackage.ValidateStageForWorker(stage, root, exeHash);

            // Parent must be fully gone to avoid Windows file locks.
            try
            {
                using var previous = Process.GetProcessById(oldPid);
                if (!previous.WaitForExit(30000))
                    throw new TimeoutException("Old application did not exit in time.");
            }
            catch (ArgumentException)
            {
                // Process already gone; expected.
            }

            PortableUpdatePackage.ValidateStageForWorker(stage, root, exeHash);
            string oldBackend = Path.Combine(root, "backend");
            if (!Directory.Exists(oldBackend))
                throw new InvalidOperationException("Existing backend missing. Update cancelled.");

            string backupFolder = Path.Combine(root, "data", "update-backups",
                DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backupFolder);
            string savedExe = Path.Combine(backupFolder, PortableUpdatePackage.Executable);
            string savedBackend = Path.Combine(backupFolder, "backend");
            File.Copy(target, savedExe);
            bool oldBackendMoved = false;
            bool newBackendMoved = false;
            bool exeReplaced = false;
            try
            {
                Directory.Move(oldBackend, savedBackend);
                oldBackendMoved = true;
                Directory.Move(Path.Combine(stage, "backend"), oldBackend);
                newBackendMoved = true;
                File.Copy(Path.Combine(stage, PortableUpdatePackage.Executable), target,
                    overwrite: true);
                exeReplaced = true;

                string readme = Path.Combine(stage, "README_KR.md");
                if (File.Exists(readme))
                {
                    try { File.Copy(readme, Path.Combine(root, "README_KR.md"), overwrite: true); }
                    catch (IOException) { /* optional documentation; never break upgrade */ }
                }

                using var updated = Process.Start(new ProcessStartInfo(target)
                {
                    UseShellExecute = true,
                    WorkingDirectory = root
                }) ?? throw new InvalidOperationException("Updated EXE could not restart.");

                Directory.CreateDirectory(AppPaths.BaseDirectory);
                File.WriteAllText(logFile,
                    "Portable update launched successfully. Previous EXE and backend backed up.\n");
            }
            catch
            {
                // Restore the original cohesive pair on any failure to replace
                // or launch the new program.
                if (exeReplaced || File.Exists(savedExe))
                    File.Copy(savedExe, target, overwrite: true);
                if (newBackendMoved && Directory.Exists(oldBackend))
                    Directory.Delete(oldBackend, recursive: true);
                if (oldBackendMoved && Directory.Exists(savedBackend))
                    Directory.Move(savedBackend, oldBackend);
                throw;
            }
            finally
            {
                if (Directory.Exists(stage))
                {
                    try { Directory.Delete(stage, recursive: true); } catch (IOException) { }
                }
            }
        }
        catch (Exception ex)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.BaseDirectory);
                File.WriteAllText(logFile,
                    "Portable update failed: " + ex.GetType().Name + " — " +
                    ex.Message + Environment.NewLine);
            }
            catch { /* keep original installation intact when possible */ }
        }
    }
}
