using System.Globalization;
using System.Text.Json;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Public, non-secret portable UI preferences. Sensitive SSH keys and logs
/// intentionally remain in the current Windows user's private app data.
/// </summary>
internal static class LanguageSettings
{
    internal static string FilePath => Path.Combine(AppPaths.PortableRoot, "data", "settings.json");

    internal static bool IsConfigured
    {
        get
        {
            if (!File.Exists(FilePath)) return false;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
                string? language = doc.RootElement.GetProperty("language").GetString();
                return UiStrings.Languages.Any(x => x.Code == language);
            }
            catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException
                    or UnauthorizedAccessException or InvalidOperationException)
            {
                return false;
            }
        }
    }

    internal static string Load()
    {
        if (IsConfigured)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
                return doc.RootElement.GetProperty("language").GetString()!;
            }
            catch (Exception ex) when (ex is IOException or JsonException
                    or UnauthorizedAccessException) { }
        }
        string code = CultureInfo.CurrentUICulture.Name;
        if (code.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) return "ko-KR";
        if (code.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) return "ja-JP";
        return "en-US";
    }

    internal static void Save(string language)
    {
        if (!UiStrings.Languages.Any(x => x.Code == language))
            throw new ArgumentOutOfRangeException(nameof(language));

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string stage = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(stage, JsonSerializer.Serialize(
                new Dictionary<string,string> { ["language"] = language }));
            File.Move(stage, FilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(stage)) File.Delete(stage);
        }
    }
}
