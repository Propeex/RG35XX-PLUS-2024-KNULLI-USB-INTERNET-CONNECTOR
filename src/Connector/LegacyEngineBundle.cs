using System;
using System.IO.Compression;
using System.Security.Cryptography;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Transitional bridge to the existing, user-tested USB engine family.
/// No compiled third-party binary is committed to this public repo.
/// An exact SHA-256 pinned v1.6 ZIP can be imported locally for hardware testing.
/// It is NOT automatically downloaded and it is not redistributed with the EXE.
/// </summary>
internal static class LegacyEngineBundle
{
    internal const string Filename = "RG35XX_2024_USB_Full_Internet_v1_6_GUI.zip";
    internal const string ExpectedArchiveSha256 =
        "3f69756af1825eb6738dca25e0c9222dc43d17991fbc051670fce7f5778d015d";
    internal static string Folder => Path.Combine(AppPaths.BaseDirectory, "legacy-engine-v16");
    private static string Marker => Path.Combine(Folder, ".verified-archive-sha256");

    internal static string Script => Path.Combine(Folder, "USB-FullInternet.ps1");
    internal static bool Installed => File.Exists(Script)
        && File.Exists(Path.Combine(Folder, "device", "start-fullnet.sh"))
        && File.Exists(Path.Combine(Folder, "device", "stop-fullnet.sh"))
        && File.Exists(Path.Combine(Folder, "device", "compat-fullnet.sh"))
        && File.Exists(Path.Combine(Folder, "bin", "dns-bridge-arm64"))
        && File.Exists(Path.Combine(Folder, "bin", "daemon-launch-arm64"))
        && File.Exists(Path.Combine(Folder, "bin", "tunctl-arm64"))
        && File.Exists(Marker)
        && string.Equals(File.ReadAllText(Marker).Trim(), ExpectedArchiveSha256,
            StringComparison.OrdinalIgnoreCase);

    internal static string LocateArchive()
    {
        string? exeDir = Path.GetDirectoryName(Environment.ProcessPath);
        if (string.IsNullOrEmpty(exeDir)) return "";
        string adjacent = Path.Combine(exeDir, Filename);
        return File.Exists(adjacent) ? adjacent : "";
    }

    internal static async Task<string> EnsureLocalImportAsync(
        Action<string> log, CancellationToken ct)
    {
        if (Installed)
        {
            log("Local legacy USB engine found (SHA-256 verified at import).");
            return "Installed";
        }

        string archive = LocateArchive();
        if (string.IsNullOrEmpty(archive))
        {
            log("Legacy backend archive not found. USB connection button will remain disabled.");
            log("For development testing, place the original " + Filename + " next to the EXE.");
            return "Missing";
        }

        await using (var stream = File.OpenRead(archive))
        {
            string computed = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
            if (!computed.Equals(ExpectedArchiveSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Legacy USB engine ZIP has an unexpected SHA-256.");
        }

        // Never overwrite or delete an existing imported engine while it may be
        // running. This stage only imports when the target is absent.
        if (Directory.Exists(Folder))
            throw new IOException("A partial legacy engine directory already exists; refusing automatic overwrite.");

        string parent = AppPaths.BaseDirectory;
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, "engine-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            using (var zip = ZipFile.OpenRead(archive))
            {
                long totalBytes = 0;
                string root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                foreach (var entry in zip.Entries)
                {
                    totalBytes = checked(totalBytes + entry.Length);
                    if (totalBytes > 50_000_000)
                        throw new InvalidDataException("Legacy bundle exceeds the extraction size limit.");
                    if (entry.FullName.Contains('\\') || entry.FullName.StartsWith('/'))
                        throw new InvalidDataException("Unsafe path in legacy bundle.");
                    string fullPath = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                    if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Path traversal in legacy bundle.");
                    if (string.Equals(entry.FullName, "adb.exe", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("ADB must come only from official Google Platform-Tools.");
                }
                zip.ExtractToDirectory(staging);
            }

            string ps = Path.Combine(staging, "USB-FullInternet.ps1");
            if (!File.Exists(ps)) throw new InvalidDataException("Legacy backend script missing.");
            foreach (string name in new[]
            {
                "device/start-fullnet.sh", "device/stop-fullnet.sh",
                "device/compat-fullnet.sh", "device/setup-ssh-key.sh",
                "bin/dns-bridge-arm64", "bin/tunctl-arm64", "bin/daemon-launch-arm64"
            })
                if (!File.Exists(Path.Combine(staging, name.Replace('/', Path.DirectorySeparatorChar))))
                    throw new InvalidDataException("Legacy backend is incomplete: " + name);

            // Minimal, checked adapter change. The original v1.6 script can
            // use auto-managed Platform-Tools instead of requiring adb.exe
            // inside its own directory.
            string text = File.ReadAllText(ps);
            const string previous = "$script:adb=Join-Path $script:root 'adb.exe'";
            const string replacement = "$script:adb=$env:RG35XX_ADB_EXE; if([string]::IsNullOrWhiteSpace($script:adb)){throw 'Missing RG35XX_ADB_EXE environment variable'}";
            if (!text.Contains(previous, StringComparison.Ordinal))
                throw new InvalidDataException("Unrecognized legacy script; refusing to apply an unverified patch.");
            text = text.Replace(previous, replacement, StringComparison.Ordinal);
            File.WriteAllText(ps, text, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            File.WriteAllText(Path.Combine(staging, ".verified-archive-sha256"),
                ExpectedArchiveSha256 + Environment.NewLine);
            Directory.Move(staging, Folder);
            log("Verified local legacy engine imported. Original ZIP was not altered.");
            return "Imported";
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                try { Directory.Delete(staging, recursive: true); } catch (IOException) { }
            }
        }
    }
}
