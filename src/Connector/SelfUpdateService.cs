using System;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Selects a compatible GitHub release and downloads its complete portable ZIP,
/// verifies SHA-256 and backend contents, then hands off replacement to a copy
/// of the running application. Never updates by overwriting a running EXE.
/// </summary>
internal static class SelfUpdateService
{
    private const string Repository = "Propeex/RG35XX-PLUS-2024-KNULLI-USB-INTERNET-CONNECTOR";
    private const string ExpectedAsset = PortableUpdatePackage.ReleaseAsset;
    private const string Api = "https://api.github.com/repos/" + Repository + "/releases?per_page=30";

    internal static async Task<bool> CheckAndLaunchAsync(
        IProgress<StartupStep> progress, Action<string> log, CancellationToken ct)
    {
        // Check only release METADATA with a bounded deadline. If GitHub
        // is unreachable, StartupService keeps the existing program usable.
        // A real update ZIP download is intentionally NOT limited to 8s.
        using var metadataDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        metadataDeadline.CancelAfter(StartupHttp.MetadataTimeout);
        using var response = await StartupHttp.Client.GetAsync(Api, metadataDeadline.Token);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(metadataDeadline.Token);
        using var json = await JsonDocument.ParseAsync(body,
            cancellationToken: metadataDeadline.Token);
        if (json.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("Unexpected GitHub release listing.");

        string ownVersionString = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "0.0.0-dev";
        if (!SemVersion.TryParse(ownVersionString, out var current))
            throw new InvalidOperationException("Unrecognized current application version.");
        bool onBeta = current.PreRelease is not null;
        log("Current application version: " + ownVersionString);

        JsonElement? best = null;
        SemVersion? bestVersion = null;
        foreach (var release in json.RootElement.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean()) continue;
            bool prerelease = release.GetProperty("prerelease").GetBoolean();
            if (prerelease && !onBeta) continue;
            string tag = release.GetProperty("tag_name").GetString() ?? "";
            if (!SemVersion.TryParse(tag, out var candidate)) continue;
            if (candidate.CompareTo(current) <= 0) continue;
            if (bestVersion is not null && candidate.CompareTo(bestVersion) <= 0) continue;
            best = release;
            bestVersion = candidate;
        }
        if (best is null)
        {
            log("No newer compatible GitHub Release.");
            return false;
        }

        var selected = best.Value;
        string version = selected.GetProperty("tag_name").GetString() ?? "?";
        log("New application release available: " + version);
        JsonElement? binary = null;
        if (selected.TryGetProperty("assets", out var assets) &&
            assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (asset.GetProperty("name").GetString() == ExpectedAsset)
                {
                    binary = asset;
                    break;
                }
            }
        }
        if (binary is null)
        {
            log("Release " + version + " has no portable ZIP asset; skipping.");
            return false;
        }

        var assetInfo = binary.Value;
        string digest = assetInfo.TryGetProperty("digest", out var d)
            ? d.GetString() ?? "" : "";
        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            log("Release asset lacks SHA-256 digest; refusing automatic update.");
            return false;
        }
        string hex = digest[7..];
        if (hex.Length != 64 || !hex.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid release asset SHA-256.");
        long expectedSize = assetInfo.GetProperty("size").GetInt64();
        if (expectedSize < 100_000 || expectedSize > 200_000_000)
            throw new InvalidDataException("Unexpected release asset size.");

        string downloadLink = assetInfo.GetProperty("browser_download_url").GetString() ?? "";
        var uri = new Uri(downloadLink, UriKind.Absolute);
        if (uri.Scheme != Uri.UriSchemeHttps || uri.Host != "github.com"
            || !uri.AbsolutePath.StartsWith("/" + Repository + "/releases/download/",
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Unexpected GitHub release asset URL.");

        var folder = Path.Combine(AppPaths.BaseDirectory, "updates");
        Directory.CreateDirectory(folder);
        var downloaded = Path.Combine(folder, "download-" + Guid.NewGuid().ToString("N") + ".zip");
        string? stage = null;
        bool updaterLaunched = false;
        try
        {
            await DownloadAsync(uri, downloaded, expectedSize, progress, ct);
            progress.Report(new StartupStep("Verifying application ZIP and backend..."));
            await VerifySha256Async(downloaded, hex, ct);
            stage = PortableUpdatePackage.StageAndValidate(downloaded);
            // Check the *new* executable in its complete staged directory
            // before replacing a working installation. The existing EXE's
            // ZIP verifier alone cannot detect a non-starting new build.
            progress.Report(new StartupStep("Testing downloaded application before install..."));
            await PreflightStagedExecutableAsync(stage, ct);
            await PreflightStagedLauncherAsync(stage, ct);
            string newExeHash = PortableUpdatePackage.ExeHash(stage);
            string? currentExe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
                throw new InvalidOperationException("Cannot locate running executable.");
            string updaterCopy = Path.Combine(folder, "updater-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(currentExe, updaterCopy, overwrite: false);
            var start = new ProcessStartInfo(updaterCopy)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(currentExe)!
            };
            start.ArgumentList.Add("--apply-update");
            start.ArgumentList.Add(Process.GetCurrentProcess().Id.ToString());
            start.ArgumentList.Add(currentExe);
            start.ArgumentList.Add(stage);
            start.ArgumentList.Add(newExeHash);
            using var updater = Process.Start(start)
                ?? throw new InvalidOperationException("Could not launch update helper.");
            log("Verified portable update; updater will replace EXE and backend, preserving platform-tools and settings.");
            updaterLaunched = true;
            return true;
        }
        finally
        {
            try { File.Delete(downloaded); } catch (IOException) { }
            if (!updaterLaunched && stage is not null)
            {
                try { Directory.Delete(stage, recursive: true); } catch (IOException) { }
            }
        }
    }

    private static async Task PreflightStagedLauncherAsync(string stage, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var info = new ProcessStartInfo(Path.Combine(stage, PortableUpdatePackage.Launcher))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = stage
        };
        info.ArgumentList.Add("--self-test");
        using var process = Process.Start(info)
            ?? throw new InvalidDataException("Staged launcher could not start.");
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch (InvalidOperationException) { }
            throw new InvalidDataException("Staged launcher test timed out.");
        }
        if (process.ExitCode != 0)
            throw new InvalidDataException("Staged launcher self-test failed.");
    }

    private static async Task PreflightStagedExecutableAsync(string stage, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        var info = new ProcessStartInfo(Path.Combine(stage, PortableUpdatePackage.Executable))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = stage
        };
        info.ArgumentList.Add("--self-test");
        Process process;
        try
        {
            process = Process.Start(info)
                ?? throw new InvalidDataException("Downloaded executable failed to launch.");
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            // Treat an unlaunchable candidate as an invalid update, not as a
            // fatal GUI startup error; leave the working installation intact.
            throw new InvalidDataException("Downloaded executable cannot start on this Windows installation.", e);
        }
        using (process)
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            catch (InvalidOperationException) { }
            if (ct.IsCancellationRequested) throw;
            throw new InvalidDataException("Downloaded executable self-test timed out.");
        }
        if (process.ExitCode != 0)
            throw new InvalidDataException("Downloaded executable did not pass its self-test.");
    }

    private static async Task DownloadAsync(
        Uri url, string output, long expectedLength, IProgress<StartupStep> progress,
        CancellationToken ct)
    {
        using var response = await StartupHttp.Client.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        Uri final = response.RequestMessage?.RequestUri ?? url;
        if (final.Scheme != Uri.UriSchemeHttps ||
            !(final.Host == "github.com" || final.Host == "objects.githubusercontent.com"
              || final.Host == "release-assets.githubusercontent.com"))
            throw new InvalidDataException("Unexpected GitHub asset redirect.");

        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var file = new FileStream(output, FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 65536, useAsync: true);
        var buffer = new byte[65536];
        long received = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, ct)) > 0)
        {
            received += count;
            if (received > expectedLength)
                throw new InvalidDataException("GitHub release exceeds expected size.");
            await file.WriteAsync(buffer.AsMemory(0, count), ct);
            progress.Report(new StartupStep("Downloading application update...", received, expectedLength));
        }
        if (received != expectedLength)
            throw new InvalidDataException("GitHub release download size mismatch.");
    }

    internal static async Task VerifySha256Async(string file, string expectedHex, CancellationToken ct)
    {
        await using var stream = File.OpenRead(file);
        byte[] actual = await SHA256.HashDataAsync(stream, ct);
        if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expectedHex)))
            throw new InvalidDataException("GitHub release SHA-256 mismatch.");
    }
}

/// <summary>
/// Minimal SemVer precedence for stable/beta selection (supports numeric and
/// alphanumeric dotted prerelease identifiers, ignores build metadata).
/// </summary>
internal sealed record SemVersion(int Major, int Minor, int Patch, string? PreRelease)
    : IComparable<SemVersion>
{
    internal static bool TryParse(string input, out SemVersion result)
    {
        result = new SemVersion(0, 0, 0, null);
        string text = input.Trim().TrimStart('v', 'V').Split('+')[0];
        string[] coreAndPre = text.Split('-', 2);
        string[] parts = coreAndPre[0].Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out int major)
            || !int.TryParse(parts[1], out int minor)
            || !int.TryParse(parts[2], out int patch)
            || major < 0 || minor < 0 || patch < 0)
            return false;
        result = new SemVersion(major, minor, patch,
            coreAndPre.Length == 2 ? coreAndPre[1] : null);
        return true;
    }

    public int CompareTo(SemVersion? other)
    {
        if (other is null) return 1;
        int cmp = Major.CompareTo(other.Major);
        if (cmp != 0) return cmp;
        cmp = Minor.CompareTo(other.Minor);
        if (cmp != 0) return cmp;
        cmp = Patch.CompareTo(other.Patch);
        if (cmp != 0) return cmp;
        if (PreRelease is null) return other.PreRelease is null ? 0 : 1;
        if (other.PreRelease is null) return -1;
        string[] left = PreRelease.Split('.');
        string[] right = other.PreRelease.Split('.');
        for (int i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            bool aNumeric = int.TryParse(left[i], out int a);
            bool bNumeric = int.TryParse(right[i], out int b);
            if (aNumeric && bNumeric) cmp = a.CompareTo(b);
            else if (aNumeric != bNumeric) cmp = aNumeric ? -1 : 1;
            else cmp = string.CompareOrdinal(left[i], right[i]);
            if (cmp != 0) return cmp;
        }
        return left.Length.CompareTo(right.Length);
    }
}
