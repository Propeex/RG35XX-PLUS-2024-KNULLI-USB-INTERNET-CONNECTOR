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
    /// <returns>True if a verified app update helper was started. Caller must exit immediately.</returns>
    internal async Task<bool> PrepareAsync(
        IProgress<StartupStep> progress,
        Action<string> log,
        CancellationToken cancellation)
    {
        progress.Report(new StartupStep("Checking application releases..."));
        try
        {
            if (await SelfUpdateService.CheckAndLaunchAsync(progress, log, cancellation))
            {
                // Do not initialize Platform-Tools in an EXE being replaced.
                return true;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                or JsonException or InvalidOperationException or IOException
                or UnauthorizedAccessException or CryptographicException)
        {
            // A failed GitHub update must not take away the existing application.
            log("GitHub release check/update skipped: " + ex.Message);
        }

        progress.Report(new StartupStep("Checking Android Platform-Tools..."));
        await PlatformToolsService.EnsureInstalledAsync(progress, log, cancellation);

        if (!File.Exists(AppPaths.AdbPath))
            throw new FileNotFoundException("Platform-Tools installation finished without adb.exe.");
        progress.Report(new StartupStep("Startup checks completed."));
        log("Startup prerequisites completed.");
        return false;
    }
}
