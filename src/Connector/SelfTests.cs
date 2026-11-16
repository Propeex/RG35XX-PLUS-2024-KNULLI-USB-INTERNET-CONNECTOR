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
            Assert(!typeof(MainForm).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(ProgressBar)),
                "MainForm must never own an update progress bar");
            Assert(typeof(BootstrapForm).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(ProgressBar)),
                "Only startup is allowed a progress bar");
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
