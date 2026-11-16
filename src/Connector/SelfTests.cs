using System;
using System.Reflection;
using System.Windows.Forms;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Quick, offline executable smoke tests, invoked by Windows CI.
/// Never starts ADB, downloads SDKs, or opens any GUI.
/// </summary>
internal static class SelfTests
{
    internal static int Run()
    {
        try
        {
            Assert(UsbBackendBundle.VerifyDistribution(out string backendError),
                "portable backend integrity: " + backendError);
            Assert(AdbProbe.HasReadyDevice("0123456789     device product:occam model:Nexus_4"),
                "accept whitespace-separated ADB device line");
            Assert(AdbProbe.HasReadyDevice("0123456789\tdevice product:occam"),
                "accept tab-separated ADB device line");
            Assert(!AdbProbe.HasReadyDevice("0123456789 unauthorized"),
                "do not treat unauthorized device as ready");
            Assert(!AdbProbe.HasReadyDevice("serial-one device\nserial-two device\n"),
                "multiple authorized devices are ambiguous");
            Assert(!AdbProbe.HasReadyDevice("serial-one offline"),
                "offline USB transport is not ready");
            Assert(SshPasswordBridge.IsPasswordPrompt(new[] { "root@127.0.0.1's password:" }),
                "default root password challenge can use headless askpass");
            Assert(!SshPasswordBridge.IsPasswordPrompt(new[] { "Enter passphrase for key" }),
                "askpass must not answer SSH identity passphrases");
            Assert(!SshPasswordBridge.IsPasswordPrompt(new[] { "Confirm SSH host fingerprint" }),
                "askpass must not answer host key confirmation prompts");
            Assert(!SshPasswordBridge.IsPasswordPrompt(Array.Empty<string>()),
                "askpass must reject missing prompt");
            Assert(UsbEngineRunner.NativeBackendVerified(
                    "16:00:01 RGUSB_NATIVE_HTTPS_OK: direct HTTPS verified on KNULLI."),
                "stage marker proves backend native HTTPS");
            Assert(!UsbEngineRunner.NativeBackendVerified(
                    "16:00:02 USB Internet action completed successfully."),
                "generic exit success alone is not native HTTPS proof");
            Assert(UsbEngineRunner.NativeProbeSucceeded(0, "RGUSB_NATIVE_OK\r\n"),
                "explicit remote HTTPS success required");
            Assert(!UsbEngineRunner.NativeProbeSucceeded(0, "RGUSB_NATIVE_FAIL\r\n"),
                "adb success exit code cannot prove remote HTTPS");
            Assert(!UsbEngineRunner.NativeProbeSucceeded(1, "RGUSB_NATIVE_OK"),
                "adb errors cannot prove remote HTTPS");
            Assert(UiStrings.Get("ko-KR", "DeviceDisconnected") == "연결 안 됨",
                "Korean device disconnected state");
            Assert(UiStrings.Get("ko-KR", "InternetConnected") == "연결됨",
                "Korean Internet connected state");
            Assert(UiStrings.Get("en-US", "ProgressStage").Contains("{0}"),
                "manual progress displays a stage number");
            Assert(UiStrings.Get("ja-JP", "ProgressStopping") != "ProgressStopping",
                "manual STOP progress supports Japanese");
            Assert(PortableUpdatePackage.Launcher == "RG35XX-Start.exe",
                "portable update includes the small .NET prerequisite launcher");
            Assert(SemVersion.TryParse("v0.1.0-beta.1", out var beta1), "parse beta1");
            Assert(SemVersion.TryParse("0.1.0-beta.2", out var beta2), "parse beta2");
            Assert(SemVersion.TryParse("v0.1.0", out var stable), "parse stable");
            Assert(beta2.CompareTo(beta1) > 0, "beta number order");
            Assert(stable.CompareTo(beta2) > 0, "stable > beta");
            Assert(!SemVersion.TryParse("not-a-version", out _), "reject invalid tag");
            Assert(UiStrings.Get("ko-KR", "Connect") == "인터넷 연결", "Korean translation");
            Assert(UiStrings.Get("en-US", "Disconnect") == "Disconnect Internet",
                "English translation");
            Assert(UiStrings.Get("ja-JP", "Connected") == "インターネット接続済み",
                "Japanese translation");
            Assert(typeof(MainForm).GetField("_actionProgress",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType == typeof(ProgressBar),
                "MainForm has a manual connect/disconnect progress indicator");
            Assert(typeof(BootstrapForm).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(ProgressBar)),
                "Only startup is allowed a progress bar");
            Assert(typeof(MainForm).GetField("_deviceValue",
                    BindingFlags.Instance | BindingFlags.NonPublic) is not null,
                "MainForm has independent device indicator");
            Assert(typeof(MainForm).GetField("_internetValue",
                    BindingFlags.Instance | BindingFlags.NonPublic) is not null,
                "MainForm has independent Internet indicator");
            Assert(typeof(MainForm).GetField("_languageLabel",
                    BindingFlags.Instance | BindingFlags.NonPublic) is null,
                "Main language selector has no redundant Language label");
            Assert(typeof(MainForm).GetField("_actionCancellation",
                    BindingFlags.Instance | BindingFlags.NonPublic) is not null,
                "User actions support cancellation for USB loss and sleep");
            Assert(typeof(MainForm).GetField("_internetHealthy",
                    BindingFlags.Instance | BindingFlags.NonPublic) is not null,
                "Independent Internet health is tracked");
            Assert(typeof(MainForm).GetField("_missingDeviceChecksDuringAction",
                    BindingFlags.Instance | BindingFlags.NonPublic) is not null,
                "In-flight USB loss uses debounced ADB readiness");

            return 0;
        }
        catch
        {
            return 1;
        }
    }

    private static void Assert(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
    }
}
