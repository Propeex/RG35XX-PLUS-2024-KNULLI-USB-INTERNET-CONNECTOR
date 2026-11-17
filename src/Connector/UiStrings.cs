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
                ["Title"] = "KNULLI USB Link · RG35XX+ (2024)",
                ["Language"] = "언어",
                ["StatusPrefix"] = "상태",
                ["DeviceLabel"] = "기기 연결:",
                ["InternetLabel"] = "인터넷 연결:",
                ["DeviceConnected"] = "연결됨",
                ["DeviceDisconnected"] = "연결 안 됨",
                ["UsbReconnectHint"] = "USB 재연결 후 인식되지 않으면 기기에서 USB 모드를 다시 설정하세요.",
                ["ActiveOperationOnExit"] = "USB 작업이 진행 중입니다. 작업이 끝나면 종료할 수 있습니다.",
                ["DisconnectOnExit"] = "USB 인터넷 연결을 안전하게 해제하고 종료하시겠습니까?\n\n예: 연결을 해제한 후 종료\n아니요: 프로그램으로 돌아가기",
                ["DisconnectFailedOnExit"] = "인터넷 연결 해제가 완료되지 않았습니다. 기기의 네트워크가 계속 활성화되어 있을 수 있습니다.",
                ["AlreadyRunning"] = "KNULLI USB Link가 이미 실행 중입니다.",
                ["InternetConnected"] = "연결됨",
                ["InternetDisconnected"] = "연결 안 됨",
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
                ["ProgressStarting"] = "인터넷 연결 준비 중...",
                ["ProgressStopping"] = "인터넷 연결 해제 중...",
                ["ProgressComplete"] = "작업 완료",
                ["ProgressStage"] = "연결 단계 {0}/{1} 진행 중…",
                ["ProgressStopStage"] = "해제 단계 {0}/{1} 진행 중…",
                ["Log"] = "실행 기록",
                ["AppUpdate"] = "프로그램 업데이트",
                ["ToolsUpdate"] = "Platform-Tools 업데이트",
                ["NotIntegrated"] = "네트워크 엔진은 아직 통합되지 않았습니다.",
                ["Close"] = "닫기"
            },
            ["en-US"] = new Dictionary<string, string>
            {
                ["Title"] = "KNULLI USB Link · RG35XX+ (2024)",
                ["Language"] = "Language",
                ["StatusPrefix"] = "Status",
                ["DeviceLabel"] = "Device:",
                ["InternetLabel"] = "Internet:",
                ["DeviceConnected"] = "Connected",
                ["DeviceDisconnected"] = "Disconnected",
                ["UsbReconnectHint"] = "If USB reconnection is not detected, reset USB mode on your KNULLI device.",
                ["ActiveOperationOnExit"] = "A USB operation is still running. Close the app when it finishes.",
                ["DisconnectOnExit"] = "Disconnect USB Internet safely and close the application?\n\nYes: Disconnect and exit\nNo: Return to the application",
                ["DisconnectFailedOnExit"] = "USB Internet STOP did not complete. Networking on the device may still be active.",
                ["AlreadyRunning"] = "KNULLI USB Link is already running.",
                ["InternetConnected"] = "Connected",
                ["InternetDisconnected"] = "Disconnected",
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
                ["ProgressStarting"] = "Preparing USB Internet...",
                ["ProgressStopping"] = "Disconnecting USB Internet...",
                ["ProgressComplete"] = "Operation complete",
                ["ProgressStage"] = "Connection stage {0}/{1} in progress…",
                ["ProgressStopStage"] = "Disconnect stage {0}/{1} in progress…",
                ["Log"] = "Activity Log",
                ["AppUpdate"] = "Application Update",
                ["ToolsUpdate"] = "Platform-Tools Update",
                ["NotIntegrated"] = "The networking engine has not yet been integrated.",
                ["Close"] = "Close"
            },
            ["ja-JP"] = new Dictionary<string, string>
            {
                ["Title"] = "KNULLI USB Link · RG35XX+ (2024)",
                ["Language"] = "言語",
                ["StatusPrefix"] = "状態",
                ["DeviceLabel"] = "デバイス:",
                ["InternetLabel"] = "インターネット:",
                ["DeviceConnected"] = "接続済み",
                ["DeviceDisconnected"] = "未接続",
                ["UsbReconnectHint"] = "USB 再接続後に認識しない場合は、本体の USB モードを再設定してください。",
                ["ActiveOperationOnExit"] = "USB 処理を実行中です。完了後に終了できます。",
                ["DisconnectOnExit"] = "USB インターネット接続を安全に切断して終了しますか？\n\nはい：切断して終了\nいいえ：アプリに戻る",
                ["DisconnectFailedOnExit"] = "接続解除が完了しませんでした。本体のネットワークがまだ有効な可能性があります。",
                ["AlreadyRunning"] = "KNULLI USB Link はすでに実行中です。",
                ["InternetConnected"] = "接続済み",
                ["InternetDisconnected"] = "未接続",
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
                ["ProgressStarting"] = "インターネット接続を準備中...",
                ["ProgressStopping"] = "インターネット接続を切断中...",
                ["ProgressComplete"] = "操作完了",
                ["ProgressStage"] = "接続段階 {0}/{1} 処理中…",
                ["ProgressStopStage"] = "切断段階 {0}/{1} 処理中…",
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
