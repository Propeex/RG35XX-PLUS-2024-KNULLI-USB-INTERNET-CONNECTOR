using System;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Extracts the SHA-256 validated, CI-built backend resource bundled inside
/// the EXE. No historical user ZIP and no runtime third-party downloads.
/// Each EXE build uses a content-addressed private directory to avoid
/// overwriting binaries belonging to a currently active SSH/TUN session.
/// </summary>
internal static class UsbBackendBundle
{
    private const string ResourceName = "RG35XXUsbInternetConnector.EmbeddedBackend";
    private static readonly string[] Required =
    {
        "USB-FullInternet.ps1",
        "bin/daemon-launch-arm64", "bin/dns-bridge-arm64",
        "bin/tunctl-arm64", "bin/tun2socks-arm64",
        "device/start-fullnet.sh", "device/stop-fullnet.sh",
        "device/check-fullnet.sh", "device/compat-fullnet.sh",
        "device/watch-fullnet.sh", "device/setup-ssh-key.sh",
        "third_party/tun2socks-LICENSE.txt"
    };

    private static readonly Lazy<string?> BundleHash = new(() =>
    {
        using Stream? resource = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(ResourceName);
        if (resource is null) return null;
        return Convert.ToHexString(SHA256.HashData(resource)).ToLowerInvariant();
    });

    internal static string? ResourceHash => BundleHash.Value;
    internal static string Folder => Path.Combine(
        AppPaths.BaseDirectory, "backend",
        (ResourceHash ?? "missing").Substring(0, 12));
    internal static string Script => Path.Combine(Folder, "USB-FullInternet.ps1");
    internal static bool Installed => ResourceHash is not null && VerifyInstalled(Folder);

    internal static bool VerifyEmbedded(out string error)
    {
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(ResourceName);
            if (stream is null)
                throw new FileNotFoundException("Compiled EXE has no embedded USB backend.");
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            ValidateZip(zip);
            error = "";
            return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException
                or ArgumentException or OverflowException)
        {
            error = e.Message;
            return false;
        }
    }

    private static Dictionary<string, string> ReadManifest(ZipArchive zip)
    {
        var entry = zip.GetEntry("SHA256SUMS")
            ?? throw new InvalidDataException("Missing signed-build checksum manifest.");
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        using var input = new StreamReader(entry.Open(), Encoding.ASCII);
        while (input.ReadLine() is string line)
        {
            if (line.Length < 67 || line[64] != ' ' || line[65] != ' ')
                throw new InvalidDataException("Bad checksum manifest line.");
            string hash = line[..64];
            string path = line[66..];
            if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)
                || path.Contains('\\') || path.StartsWith('/')
                || path.Contains("..", StringComparison.Ordinal)
                || path.Contains(':'))
                throw new InvalidDataException("Unsafe checksum manifest entry.");
            if (!map.TryAdd(path, hash))
                throw new InvalidDataException("Duplicate checksum manifest entry.");
        }
        return map;
    }

    private static void ValidateZip(ZipArchive zip)
    {
        var hashes = ReadManifest(zip);
        long aggregate = 0;
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            string path = entry.FullName;
            if (path.EndsWith('/')) continue;
            if (path == "SHA256SUMS") continue;
            if (!hashes.TryGetValue(path, out string? expected))
                throw new InvalidDataException("Payload file not listed in manifest: " + path);
            if (!found.Add(path))
                throw new InvalidDataException("Duplicate ZIP entry.");
            aggregate = checked(aggregate + entry.Length);
            if (aggregate > 110_000_000)
                throw new InvalidDataException("Backend archive exceeds extraction size limit.");
            using var source = entry.Open();
            string actual = Convert.ToHexString(SHA256.HashData(source));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Backend checksum mismatch: " + path);
        }
        if (found.Count != hashes.Count || Required.Any(x => !found.Contains(x)))
            throw new InvalidDataException("Incomplete backend ZIP or checksum manifest.");
    }

    private static bool VerifyInstalled(string directory)
    {
        try
        {
            string manifest = Path.Combine(directory, "SHA256SUMS");
            if (!File.Exists(manifest)) return false;
            foreach (string line in File.ReadLines(manifest))
            {
                if (line.Length < 67 || line[64] != ' ' || line[65] != ' ')
                    return false;
                string expected = line[..64];
                string relative = line[66..];
                if (!expected.All(Uri.IsHexDigit) || relative.Contains('\\')
                    || relative.Contains("..", StringComparison.Ordinal)
                    || relative.Contains(':') || relative.StartsWith('/'))
                    return false;
                string file = Path.Combine(directory, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(file)) return false;
                using var stream = File.OpenRead(file);
                string actual = Convert.ToHexString(SHA256.HashData(stream));
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return Required.All(p => File.Exists(Path.Combine(directory,
                p.Replace('/', Path.DirectorySeparatorChar))));
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    internal static async Task EnsureInstalledAsync(Action<string> log, CancellationToken ct)
    {
        if (ResourceHash is null)
        {
            log("No embedded USB backend in this local development build.");
            return;
        }
        if (Installed)
        {
            log("Verified embedded backend ready.");
            return;
        }
        if (!VerifyEmbedded(out string reason))
            throw new InvalidDataException("Embedded backend validation failed: " + reason);
        if (Directory.Exists(Folder))
            throw new InvalidDataException(
                "Extracted backend has changed or is incomplete. Refusing to overwrite active helper files.");

        string stage = Path.Combine(AppPaths.BaseDirectory,
            "backend-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        try
        {
            ct.ThrowIfCancellationRequested();
            using var embedded = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(ResourceName)!;
            using (var archive = new ZipArchive(embedded, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith('/')) continue;
                    string destination = Path.GetFullPath(Path.Combine(stage, entry.FullName));
                    string prefix = Path.GetFullPath(stage) + Path.DirectorySeparatorChar;
                    if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        || entry.FullName.Contains('\\') || entry.FullName.Contains("..")
                        || entry.FullName.Contains(':') || entry.FullName.StartsWith('/'))
                        throw new InvalidDataException("Unsafe backend ZIP path.");
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    entry.ExtractToFile(destination);
                }
            }
            ct.ThrowIfCancellationRequested();
            if (!VerifyInstalled(stage))
                throw new InvalidDataException("Extracted backend content verification failed.");
            Directory.CreateDirectory(Path.GetDirectoryName(Folder)!);
            Directory.Move(stage, Folder);
            log("Embedded USB backend verified and installed locally.");
        }
        finally
        {
            if (Directory.Exists(stage))
            {
                try { Directory.Delete(stage, recursive: true); } catch (IOException) { }
            }
        }
    }
}
