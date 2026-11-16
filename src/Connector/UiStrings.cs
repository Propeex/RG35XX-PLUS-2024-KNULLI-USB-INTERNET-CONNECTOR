namespace RG35XXUsbInternetConnector;

/// <summary>
/// Only interactive controls and status labels are localized.
/// Diagnostic activity log entries must remain in English.
/// </summary>
internal static class UiStrings
{
    internal const string DefaultLanguage = "ko-KR";

    internal static readonly (string Code, string Display)[] Languages =
    {
        ("ko-KR", "한국어"),
        ("en-US", "English"),
        ("ja-JP", "日本語")
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Values =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["ko-KR"] = new Dictionary<string, string>
            {
                ["Title"] = "RG35XX+ (2024) KNULLI USB Internet Connector",
                ["Language"] = "언어",
                ["StatusPrefix"] = "상태",
                ["NotReady"] = "인터넷 엔진 준비 중",
                ["Checking"] = "기기 연결 상태 확인 중",
                ["Disconnected"] = "기기가 연결되지 않음",
                ["Ready"] = "기기 연결됨 · 인터넷 연결 대기",
                ["Connecting"] = "인터넷 연결 중",
                ["Connected"] = "인터넷 연결됨",
                ["Disconnecting"] = "인터넷 연결 해제 중",
                ["Error"] = "오류 발생 · 실행 기록 확인",
                ["Connect"] = "인터넷 연결",
                ["Disconnect"] = "인터넷 연결 해제",
                ["Log"] = "실행 기록",
                ["AppUpdate"] = "프로그램 업데이트",
                ["ToolsUpdate"] = "Platform-Tools 업데이트",
                ["NotIntegrated"] = "네트워크 엔진은 아직 통합되지 않았습니다.",
                ["Close"] = "닫기"
            },
            ["en-US"] = new Dictionary<string, string>
            {
                ["Title"] = "RG35XX+ (2024) KNULLI USB Internet Connector",
                ["Language"] = "Language",
                ["StatusPrefix"] = "Status",
                ["NotReady"] = "Internet engine not yet integrated",
                ["Checking"] = "Checking device connection",
                ["Disconnected"] = "Device disconnected",
                ["Ready"] = "Device ready · Internet off",
                ["Connecting"] = "Connecting to the internet",
                ["Connected"] = "Internet connected",
                ["Disconnecting"] = "Disconnecting internet",
                ["Error"] = "Error · See activity log",
                ["Connect"] = "Connect Internet",
                ["Disconnect"] = "Disconnect Internet",
                ["Log"] = "Activity Log",
                ["AppUpdate"] = "Application Update",
                ["ToolsUpdate"] = "Platform-Tools Update",
                ["NotIntegrated"] = "The networking engine has not yet been integrated.",
                ["Close"] = "Close"
            },
            ["ja-JP"] = new Dictionary<string, string>
            {
                ["Title"] = "RG35XX+ (2024) KNULLI USB Internet Connector",
                ["Language"] = "言語",
                ["StatusPrefix"] = "状態",
                ["NotReady"] = "ネットワークエンジン準備中",
                ["Checking"] = "デバイス接続を確認中",
                ["Disconnected"] = "デバイス未接続",
                ["Ready"] = "デバイス接続済み · インターネット未接続",
                ["Connecting"] = "インターネット接続中",
                ["Connected"] = "インターネット接続済み",
                ["Disconnecting"] = "切断中",
                ["Error"] = "エラー · ログを確認",
                ["Connect"] = "インターネットに接続",
                ["Disconnect"] = "インターネットを切断",
                ["Log"] = "実行ログ",
                ["AppUpdate"] = "アプリの更新",
                ["ToolsUpdate"] = "Platform-Tools の更新",
                ["NotIntegrated"] = "ネットワークエンジンはまだ統合されていません。",
                ["Close"] = "閉じる"
            }
        };

    internal static string Get(string language, string key)
    {
        if (Values.TryGetValue(language, out var selected) && selected.TryGetValue(key, out var value))
            return value;
        if (Values[DefaultLanguage].TryGetValue(key, out var fallback))
            return fallback;
        return key;
    }
}

internal enum ConnectionState
{
    NotReady,
    Checking,
    Disconnected,
    Ready,
    Connecting,
    Connected,
    Disconnecting,
    Error
}
