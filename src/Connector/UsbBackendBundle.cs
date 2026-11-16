using System.Security.Cryptography;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// The distributed ZIP contains readable backend scripts and binaries in
/// ./backend, beside the EXE. Every shipped payload file is verified against
/// SHA256SUMS before use; users may inspect/modify source in GitHub and rebuild.
/// A mismatched local file is reported, not silently overwritten.
/// </summary>
internal static class UsbBackendBundle
{
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

    internal static string Folder => Path.Combine(AppPaths.PortableRoot, "backend");
    internal static string Script => Path.Combine(Folder, "USB-FullInternet.ps1");
    internal static bool Installed => VerifyDistribution(out _);

    internal static bool VerifyDistribution(out string error)
    {
        try
        {
            if (!Directory.Exists(Folder))
                throw new DirectoryNotFoundException("Portable backend folder missing beside EXE.");

            string manifest = Path.Combine(Folder, "SHA256SUMS");
            if (!File.Exists(manifest))
                throw new FileNotFoundException("Backend SHA256SUMS manifest is missing.");

            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in File.ReadLines(manifest))
            {
                if (line.Length < 67 || line[64] != ' ' || line[65] != ' ')
                    throw new InvalidDataException("Malformed backend hash manifest.");
                string expected = line[..64];
                string relative = line[66..];
                if (expected.Length != 64 || !expected.All(Uri.IsHexDigit)
                    || relative.Contains('\\') || relative.StartsWith('/')
                    || relative.Contains("..", StringComparison.Ordinal)
                    || relative.Contains(':') || Path.IsPathRooted(relative))
                    throw new InvalidDataException("Unsafe manifest path: " + relative);
                if (!hashes.TryAdd(relative, expected))
                    throw new InvalidDataException("Duplicate backend manifest path.");
            }

            if (Required.Any(f => !hashes.ContainsKey(f)))
                throw new InvalidDataException("Some required backend files were not shipped.");

            foreach (var pair in hashes)
            {
                string path = Path.Combine(Folder, pair.Key.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                    throw new FileNotFoundException("Backend file missing: " + pair.Key);
                using var file = File.OpenRead(path);
                string actual = Convert.ToHexString(SHA256.HashData(file));
                if (!actual.Equals(pair.Value, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Backend file failed SHA-256 check: " + pair.Key);
            }

            // Prevent adding unverified executable or shell files into our folder.
            foreach (string file in Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(Folder, file)
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (relative == "SHA256SUMS") continue;
                if (!hashes.ContainsKey(relative))
                    throw new InvalidDataException("Unlisted file in backend distribution: " + relative);
            }
            error = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or ArgumentException or OverflowException)
        {
            error = ex.Message;
            return false;
        }
    }

    internal static Task EnsureInstalledAsync(Action<string> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!VerifyDistribution(out string error))
            throw new InvalidDataException("Portable backend integrity failed: " + error);
        log("Portable USB backend checksum verification passed.");
        return Task.CompletedTask;
    }
}
