using System.IO.Compression;
using System.Security.Cryptography;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Validates the complete official portable Release ZIP before allowing the
/// old app process to exit. Only the EXE, backend and README are updateable.
/// Platform-Tools, data/settings and user SSH identities are never extracted
/// from a Release ZIP and are never touched by the update worker.
/// </summary>
internal static class PortableUpdatePackage
{
    internal const string ReleaseAsset = "RG35XX-USB-Internet-Connector.zip";
    internal const string Executable = "RG35XXUsbInternetConnector.exe";
    internal const string Launcher = "KNULLI USB Link.exe";

    internal static string StageAndValidate(string zipFile)
    {
        if (!File.Exists(zipFile)) throw new FileNotFoundException("Update ZIP missing.");
        string root = AppPaths.PortableRoot;
        string staging = Path.Combine(root, ".update-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        bool success = false;
        try
        {
            string prefix = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
            long totalBytes = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var archive = ZipFile.OpenRead(zipFile))
            {
                foreach (var entry in archive.Entries)
                {
                    string relative = entry.FullName;
                    if (string.IsNullOrWhiteSpace(relative) || relative.Contains('\\')
                        || relative.StartsWith('/') || relative.Contains(':')
                        || relative.Contains("..", StringComparison.Ordinal)
                        || Path.IsPathRooted(relative))
                        throw new InvalidDataException("Unsafe or unexpected ZIP entry: " + relative);
                    if (!relative.Equals(Executable, StringComparison.Ordinal)
                        && !relative.Equals(Launcher, StringComparison.Ordinal)
                        && !relative.Equals("README_KR.md", StringComparison.Ordinal)
                        && !relative.StartsWith("backend/", StringComparison.Ordinal))
                        throw new InvalidDataException("Release ZIP contains an unsupported file: " + relative);

                    // Do not allow duplicate entries or Unix-style symbolic links.
                    if (!seen.Add(relative))
                        throw new InvalidDataException("Duplicate Release ZIP entry.");
                    if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                        throw new InvalidDataException("Symbolic links are not allowed in Release ZIP.");

                    totalBytes = checked(totalBytes + entry.Length);
                    if (entry.Length > 250_000_000 || totalBytes > 400_000_000)
                        throw new InvalidDataException("Release ZIP exceeds safe extraction limit.");
                    string target = Path.GetFullPath(Path.Combine(staging, relative));
                    if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Release ZIP path escapes staging folder.");
                }
                archive.ExtractToDirectory(staging);
            }

            string nextExe = Path.Combine(staging, Executable);
            if (!File.Exists(nextExe) || new FileInfo(nextExe).Length < 20_000)
                throw new InvalidDataException("Release ZIP is missing the Windows EXE.");
            string launcher = Path.Combine(staging, Launcher);
            if (!File.Exists(launcher) || new FileInfo(launcher).Length < 4_000)
                throw new InvalidDataException("Release ZIP is missing the lightweight launcher.");
            if (!UsbBackendBundle.VerifyFolder(Path.Combine(staging, "backend"), out string reason))
                throw new InvalidDataException("Release backend hash verification failed: " + reason);

            success = true;
            return staging;
        }
        finally
        {
            if (!success)
            {
                try { Directory.Delete(staging, recursive: true); } catch (IOException) { }
            }
        }
    }

    internal static string ExeHash(string stage)
    {
        using var stream = File.OpenRead(Path.Combine(stage, Executable));
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    internal static void ValidateStageForWorker(string stage, string targetRoot, string expectedExeHash)
    {
        string fullRoot = Path.GetFullPath(targetRoot).TrimEnd(Path.DirectorySeparatorChar);
        string fullStage = Path.GetFullPath(stage);
        if (!fullStage.StartsWith(fullRoot + Path.DirectorySeparatorChar + ".update-stage-",
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                Path.GetDirectoryName(fullStage)?.TrimEnd(Path.DirectorySeparatorChar),
                fullRoot, StringComparison.OrdinalIgnoreCase)
            || expectedExeHash.Length != 64 || !expectedExeHash.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Updater staging path or checksum is invalid.");

        string actual = ExeHash(fullStage);
        if (!actual.Equals(expectedExeHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Staged EXE hash mismatch.");
        if (!File.Exists(Path.Combine(fullStage, Launcher)))
            throw new InvalidDataException("Staged launcher is missing.");
        if (!UsbBackendBundle.VerifyFolder(Path.Combine(fullStage, "backend"), out string reason))
            throw new InvalidDataException("Staged backend integrity failed: " + reason);
    }
}
