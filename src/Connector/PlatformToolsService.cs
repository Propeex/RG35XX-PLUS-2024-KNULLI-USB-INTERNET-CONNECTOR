using System;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace RG35XXUsbInternetConnector;

internal record SdkPackage(Version Revision, Uri Url, string? Sha256);

/// <summary>
/// Official Google Platform-Tools bootstrap/update.
/// Separate LOCALAPPDATA folder prevents locking the application executable.
/// All extraction is staged and validated before changing the active directory.
/// </summary>
internal static class PlatformToolsService
{
    private static readonly Uri RepositoryUri =
        new("https://dl.google.com/android/repository/repository2-1.xml");
    private static readonly Uri LatestWindowsUri =
        new("https://dl.google.com/android/repository/platform-tools-latest-windows.zip");
    private static readonly Uri RepositoryBase =
        new("https://dl.google.com/android/repository/");

    internal static async Task EnsureInstalledAsync(
        IProgress<StartupStep> progress,
        Action<string> log,
        CancellationToken ct)
    {
        bool installed = File.Exists(AppPaths.AdbPath)
            && File.Exists(Path.Combine(AppPaths.ToolsDirectory, "source.properties"));
        Version? current = installed ? ReadRevision(
            Path.Combine(AppPaths.ToolsDirectory, "source.properties")) : null;

        SdkPackage? latest = null;
        try
        {
            latest = await GetLatestStableAsync(ct);
            log("Latest official Google Platform-Tools revision: " + latest.Revision);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Xml.XmlException
                or InvalidOperationException or TaskCanceledException)
        {
            log("SDK repository metadata unavailable: " + ex.Message);
            if (installed)
            {
                log("Reusing installed Platform-Tools" +
                    (current is null ? "." : " revision " + current + "."));
                return;
            }
        }

        if (installed && latest is not null && current is not null
                && current.CompareTo(latest.Revision) >= 0)
        {
            log("Platform-Tools are up to date: " + current);
            return;
        }

        Uri download = latest?.Url ?? LatestWindowsUri;
        if (download.Scheme != Uri.UriSchemeHttps || download.Host != "dl.google.com")
            throw new InvalidOperationException("SDK download URL must be HTTPS on dl.google.com.");

        Directory.CreateDirectory(AppPaths.BaseDirectory);
        string session = Path.Combine(AppPaths.PortableRoot, ".tools-stage-" + Guid.NewGuid().ToString("N"));
        string zip = Path.Combine(session, "platform-tools.zip");
        string extract = Path.Combine(session, "extract");
        string staged = Path.Combine(extract, "platform-tools");
        string backup = Path.Combine(AppPaths.PortableRoot,
            "platform-tools-backup-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(session);
        bool movedOld = false;
        bool movedNew = false;
        try
        {
            log("Downloading official Google Platform-Tools from " + download.Host);
            await DownloadAsync(download, zip, progress, ct);
            progress.Report(new StartupStep("Verifying Platform-Tools archive..."));
            await VerifyHashAsync(zip, latest?.Sha256, ct);

            Directory.CreateDirectory(extract);
            using (var archive = ZipFile.OpenRead(zip))
            {
                // Reject a zip bomb, absolute paths, parent traversal and foreign roots.
                long extractedSize = 0;
                string fullRoot = Path.GetFullPath(extract);
                string requiredPrefix = fullRoot + Path.DirectorySeparatorChar;
                foreach (var entry in archive.Entries)
                {
                    extractedSize = checked(extractedSize + entry.Length);
                    if (extractedSize > 600_000_000)
                        throw new InvalidDataException("SDK archive exceeds size limit.");
                    string output = Path.GetFullPath(Path.Combine(extract, entry.FullName));
                    if (!output.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase)
                            || !entry.FullName.Replace('\\', '/').StartsWith("platform-tools/",
                                StringComparison.Ordinal))
                        throw new InvalidDataException("Unexpected SDK archive entry: " + entry.FullName);
                }
                archive.ExtractToDirectory(extract);
            }

            if (!File.Exists(Path.Combine(staged, "adb.exe"))
                    || !File.Exists(Path.Combine(staged, "source.properties")))
                throw new InvalidDataException("SDK ZIP is missing adb.exe or source.properties.");
            Version? unpackedRevision = ReadRevision(Path.Combine(staged, "source.properties"));
            if (unpackedRevision is null)
                throw new InvalidDataException("SDK ZIP has no valid Pkg.Revision.");
            if (latest is not null && unpackedRevision < latest.Revision)
                throw new InvalidDataException("Downloaded Platform-Tools are older than repository metadata.");

            progress.Report(new StartupStep("Installing Platform-Tools..."));
            if (Directory.Exists(AppPaths.ToolsDirectory))
            {
                Directory.Move(AppPaths.ToolsDirectory, backup);
                movedOld = true;
            }
            Directory.Move(staged, AppPaths.ToolsDirectory);
            movedNew = true;
            log("Installed Platform-Tools revision " + unpackedRevision + ".");
        }
        catch
        {
            // Restore last working tools if a staged swap failed.
            if (movedOld && !movedNew && Directory.Exists(backup))
            {
                if (Directory.Exists(AppPaths.ToolsDirectory))
                    Directory.Delete(AppPaths.ToolsDirectory, recursive: true);
                Directory.Move(backup, AppPaths.ToolsDirectory);
            }
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(session)) Directory.Delete(session, recursive: true);
            }
            catch (IOException) { log("Temporary SDK staging folder could not be removed yet."); }
            if (movedNew && Directory.Exists(backup))
            {
                try { Directory.Delete(backup, recursive: true); }
                catch (IOException) { log("Old SDK backup folder retained (locked)."); }
            }
        }
    }

    private static Version? ReadRevision(string file)
    {
        if (!File.Exists(file)) return null;
        foreach (string line in File.ReadLines(file))
        {
            int equals = line.IndexOf('=');
            if (equals < 0 || !line[..equals].Trim().Equals("Pkg.Revision",
                    StringComparison.OrdinalIgnoreCase)) continue;
            return Version.TryParse(line[(equals + 1)..].Trim(), out var version) ? version : null;
        }
        return null;
    }

    private static async Task<SdkPackage> GetLatestStableAsync(CancellationToken ct)
    {
        using var response = await StartupHttp.Client.GetAsync(RepositoryUri, ct);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var document = XDocument.Load(stream);
        var candidates = document.Descendants()
            .Where(e => e.Name.LocalName == "remotePackage"
                && (string?)e.Attribute("path") == "platform-tools");

        foreach (var pkg in candidates)
        {
            var channel = pkg.Elements().FirstOrDefault(e => e.Name.LocalName == "channelRef");
            // Android SDK channel-0 = stable; do not install canary packages.
            if (channel is not null && (string?)channel.Attribute("ref") != "channel-0")
                continue;
            var revision = pkg.Elements().FirstOrDefault(e => e.Name.LocalName == "revision")
                ?? throw new InvalidDataException("Platform-Tools revision is missing.");
            int major = ParseRevisionPart(revision, "major");
            int minor = ParseRevisionPart(revision, "minor");
            int micro = ParseRevisionPart(revision, "micro");

            var archives = pkg.Elements().FirstOrDefault(e => e.Name.LocalName == "archives");
            if (archives is null) continue;
            foreach (var archive in archives.Elements().Where(e => e.Name.LocalName == "archive"))
            {
                string? os = archive.Elements().FirstOrDefault(
                    e => e.Name.LocalName is "host-os" or "hostOs")?.Value;
                if (os != "windows") continue;
                var complete = archive.Elements().FirstOrDefault(e => e.Name.LocalName == "complete");
                string? relative = complete?.Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "url")?.Value.Trim();
                if (string.IsNullOrWhiteSpace(relative)) continue;
                Uri target = new(RepositoryBase, relative);
                if (target.Scheme != Uri.UriSchemeHttps || target.Host != "dl.google.com")
                    throw new InvalidOperationException("Untrusted SDK repository URL.");
                var checksum = complete!.Elements().FirstOrDefault(e =>
                    e.Name.LocalName == "checksum" && (string?)e.Attribute("type") == "sha256");
                string? sha = checksum?.Value.Trim();
                if (sha is not null && (sha.Length != 64 || !sha.All(Uri.IsHexDigit)))
                    throw new InvalidDataException("SDK metadata contains malformed SHA256.");
                return new SdkPackage(new Version(major, minor, micro), target, sha);
            }
        }
        throw new InvalidOperationException("No stable Windows Platform-Tools package in SDK metadata.");
    }

    private static int ParseRevisionPart(XElement revision, string part)
    {
        string? text = revision.Elements().FirstOrDefault(e => e.Name.LocalName == part)?.Value;
        return int.TryParse(text, out var n) && n >= 0 ? n : 0;
    }

    private static async Task DownloadAsync(
        Uri source, string destination, IProgress<StartupStep> progress, CancellationToken ct)
    {
        using var response = await StartupHttp.Client.GetAsync(
            source, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        Uri resolved = response.RequestMessage?.RequestUri ?? source;
        if (resolved.Scheme != Uri.UriSchemeHttps ||
            (resolved.Host != "dl.google.com" && resolved.Host != "storage.googleapis.com"))
            throw new InvalidOperationException("SDK download redirected to unexpected host.");

        long? expected = response.Content.Headers.ContentLength;
        if (expected is > 200_000_000)
            throw new InvalidDataException("Unexpected SDK ZIP size.");

        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(destination, FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 65536, useAsync: true);
        var buffer = new byte[65536];
        long received = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, ct)) > 0)
        {
            received += count;
            if (received > 200_000_000)
                throw new InvalidDataException("SDK download exceeds size limit.");
            await output.WriteAsync(buffer.AsMemory(0, count), ct);
            progress.Report(new StartupStep("Downloading Platform-Tools...", received, expected));
        }
        if (expected.HasValue && received != expected.Value)
            throw new InvalidDataException("SDK download length mismatch.");
        if (received < 4096)
            throw new InvalidDataException("SDK download is unexpectedly small.");
    }

    private static async Task VerifyHashAsync(string path, string? sha256, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sha256)) return; // official HTTPS ZIP + structure checks remain
        await using var stream = File.OpenRead(path);
        byte[] computed = await SHA256.HashDataAsync(stream, ct);
        byte[] expected = Convert.FromHexString(sha256);
        if (!CryptographicOperations.FixedTimeEquals(computed, expected))
            throw new InvalidDataException("Platform-Tools SHA256 mismatch.");
    }
}
