using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace RG35XXUsbInternetConnector;

internal static class AppPaths
{
    internal static string BaseDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RG35XXUsbInternetConnector");

    internal static string ToolsDirectory => Path.Combine(BaseDirectory, "platform-tools");
    internal static string AdbPath => Path.Combine(ToolsDirectory, "adb.exe");
    internal static string InitializedMarker => Path.Combine(BaseDirectory, "initialization-complete.marker");
    internal static string SdkLicenseAcceptedMarker => Path.Combine(BaseDirectory, "google-sdk-license-accepted.marker");
}

internal record StartupStep(string Message, long BytesReceived = 0, long? BytesTotal = null);

internal static class StartupHttp
{
    internal static readonly HttpClient Client = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true
    })
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    static StartupHttp()
    {
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("RG35XX-KNULLI-USB-Internet-Connector/0.1");
    }
}

internal sealed class StartupService
{
    internal async Task PrepareAsync(
        IProgress<StartupStep> progress,
        Action<string> log,
        CancellationToken cancellation)
    {
        progress.Report(new StartupStep("Checking application releases..."));
        try
        {
            string summary = await GithubReleaseChecker.CheckAsync(cancellation);
            log(summary);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // GitHub outage must not break an already installed, usable app.
            log("GitHub release check unavailable: " + ex.Message);
        }

        progress.Report(new StartupStep("Checking Android Platform-Tools..."));
        await PlatformToolsService.EnsureInstalledAsync(progress, log, cancellation);

        if (!File.Exists(AppPaths.AdbPath))
            throw new FileNotFoundException("Platform-Tools installation finished without adb.exe.");
        progress.Report(new StartupStep("Startup checks completed."));
        log("Startup prerequisites completed.");
    }
}

internal static class GithubReleaseChecker
{
    private const string ReleasesApi =
        "https://api.github.com/repos/Propeex/RG35XX-PLUS-2024-KNULLI-USB-INTERNET-CONNECTOR/releases?per_page=30";

    /// <summary>
    /// Discovery only. Secure replacement/restart implementation is tracked by issue #3.
    /// Never claim a release has been installed while that installer does not exist.
    /// </summary>
    internal static async Task<string> CheckAsync(CancellationToken cancellation)
    {
        using var response = await StartupHttp.Client.GetAsync(ReleasesApi, cancellation);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellation);
        if (json.RootElement.ValueKind != JsonValueKind.Array)
            throw new JsonException("Expected GitHub Releases array.");

        // /releases/latest excludes prereleases; enumerate all for the beta channel.
        foreach (var release in json.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            if (release.TryGetProperty("tag_name", out var tag))
                return "GitHub release discovered: " + (tag.GetString() ?? "unknown") +
                    ". Automatic replacement pending implementation (#3).";
        }
        return "No published GitHub Releases yet. Continuing with development build.";
    }
}
