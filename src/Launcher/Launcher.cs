// Small .NET Framework 4.x launcher (Windows 10/11 in-box runtime).
// The actual connector is a framework-dependent .NET 8 Windows Forms EXE.
// First-run locale selection and optional Desktop Runtime installation must
// work even when .NET 8 is missing. No additional NuGet packages are used.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Windows.Forms;

internal static class Launcher
{
    private static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
    private static readonly string Settings = Path.Combine(Root, "data", "settings.json");
    private static readonly string PrivateRuntime = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RG35XXUsbInternetConnector", "dotnet");
    private static readonly string ApplicationExe =
        Path.Combine(Root, "RG35XXUsbInternetConnector.exe");
    private const string InstallScript = "https://dot.net/v1/dotnet-install.ps1";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--self-test")
            return 0;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        string language = LoadLanguage();
        if (language == null)
        {
            using (var choice = new LanguageDialog())
            {
                if (choice.ShowDialog() != DialogResult.OK) return 0;
                language = choice.SelectedLanguage;
                try { SaveLanguage(language); }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Language", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return 1;
                }
            }
        }

        if (!File.Exists(ApplicationExe))
        {
            MessageBox.Show(
                Translate(language, "프로그램 파일이 없습니다. ZIP 전체를 다시 압축 해제하세요.",
                    "Application EXE is missing. Extract the complete ZIP again.",
                    "プログラムがありません。ZIP 全体を展開してください。"),
                "RG35XX USB Internet", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }

        string dotnet = FindDesktopRuntime();
        if (dotnet == null)
        {
            var answer = MessageBox.Show(
                Translate(language,
                    ".NET 8 Desktop Runtime이 없습니다.\nMicrosoft 공식 설치 프로그램을 내려받아 현재 Windows 사용자 계정에 설치하시겠습니까?\n설치는 한 번만 필요합니다.",
                    ".NET 8 Desktop Runtime is missing.\nDownload and install the official Microsoft runtime for this Windows user?\nThis is a one-time setup.",
                    ".NET 8 Desktop Runtime がありません。\nMicrosoft 公式ランタイムをダウンロードし、このユーザーにインストールしますか？\nインストールは初回のみです。"),
                Translate(language, ".NET 8 설치", "Install .NET 8", ".NET 8 のインストール"),
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return 0;

            using (var installer = new RuntimeInstallDialog(language))
            {
                if (installer.ShowDialog() != DialogResult.OK) return 3;
            }
            dotnet = FindDesktopRuntime();
            if (dotnet == null)
            {
                MessageBox.Show(
                    Translate(language, ".NET 8 설치를 확인하지 못했습니다.",
                        ".NET 8 installation could not be verified.",
                        ".NET 8 のインストールを確認できませんでした。"),
                    "RG35XX USB Internet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 4;
            }
        }

        try
        {
            var start = new ProcessStartInfo(ApplicationExe);
            start.WorkingDirectory = Root;
            start.UseShellExecute = false;
            // If the Microsoft install script was used, it installs only for
            // this user. Propagate the runtime root to GUI and its updater.
            if (dotnet == PrivateRuntime)
            {
                start.EnvironmentVariables["DOTNET_ROOT_X64"] = dotnet;
                start.EnvironmentVariables["DOTNET_ROOT"] = dotnet;
            }
            Process.Start(start);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "RG35XX USB Internet",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 5;
        }
    }

    private static string Translate(string language, string ko, string en, string ja)
    {
        return language == "ko-KR" ? ko : language == "ja-JP" ? ja : en;
    }

    private static string LoadLanguage()
    {
        try
        {
            if (!File.Exists(Settings)) return null;
            string data = File.ReadAllText(Settings, Encoding.UTF8);
            foreach (string code in new[] { "ko-KR", "en-US", "ja-JP" })
                if (data.Contains("\"language\":\"" + code + "\"")) return code;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    private static void SaveLanguage(string language)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Settings));
        // The .NET 8 application uses the same portable settings.json.
        string temporary = Settings + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllText(temporary, "{\"language\":\"" + language + "\"}",
            new UTF8Encoding(false));
        if (File.Exists(Settings)) File.Delete(Settings);
        File.Move(temporary, Settings);
    }

    private static string FindDesktopRuntime()
    {
        string global = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet");
        foreach (string root in new[] { global, PrivateRuntime })
        {
            if (!File.Exists(Path.Combine(root, "dotnet.exe"))) continue;
            string desktop = Path.Combine(root, "shared", "Microsoft.WindowsDesktop.App");
            string core = Path.Combine(root, "shared", "Microsoft.NETCore.App");
            if (!Directory.Exists(desktop) || !Directory.Exists(core)) continue;
            if (HasMajorEight(desktop) && HasMajorEight(core)) return root;
        }
        return null;
    }

    private static bool HasMajorEight(string folder)
    {
        try
        {
            foreach (string path in Directory.GetDirectories(folder))
                if (Path.GetFileName(path).StartsWith("8.", StringComparison.Ordinal))
                    return true;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return false;
    }

    private static void InstallDesktopRuntime()
    {
        string script = Path.Combine(Path.GetTempPath(),
            "rg35xx-dotnet-install-" + Guid.NewGuid().ToString("N") + ".ps1");
        try
        {
            // Official Microsoft installer script; TLS 1.2 is required even on
            // older Windows 10 .NET Framework configurations.
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            using (var client = new WebClient())
                client.DownloadFile(InstallScript, script);

            Directory.CreateDirectory(PrivateRuntime);
            var command = new ProcessStartInfo("powershell.exe");
            command.UseShellExecute = false;
            command.CreateNoWindow = true;
            command.Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass " +
                "-File \"" + script + "\" -Runtime windowsdesktop -Channel 8.0 " +
                "-InstallDir \"" + PrivateRuntime + "\" -NoPath";
            using (var child = Process.Start(command))
            {
                if (child == null) throw new InvalidOperationException("PowerShell could not start.");
                if (!child.WaitForExit(10 * 60 * 1000))
                {
                    try { child.Kill(); } catch (InvalidOperationException) { }
                    throw new TimeoutException("The .NET Desktop Runtime installer timed out.");
                }
                if (child.ExitCode != 0)
                    throw new InvalidOperationException(
                        "The Microsoft .NET 8 installer returned exit code " + child.ExitCode);
            }
        }
        finally
        {
            try { File.Delete(script); } catch (IOException) { }
        }
    }

    private sealed class LanguageDialog : Form
    {
        private readonly ComboBox languages = new ComboBox();
        internal string SelectedLanguage { get; private set; }

        internal LanguageDialog()
        {
            Text = "";
            ClientSize = new Size(430, 95);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            Controls.Add(new Label {
                Text = "Language", Location = new Point(22, 28),
                Size = new Size(130, 28), Font = new Font("Segoe UI", 11.0f)
            });
            languages.DropDownStyle = ComboBoxStyle.DropDownList;
            languages.Location = new Point(155, 26);
            languages.Size = new Size(222, 27);
            languages.Items.AddRange(new object[] { "한국어", "English", "日本語" });
            // The first screen contains only "Language" and the choices.
            // Selecting any locale immediately continues in that language.
            languages.SelectedIndexChanged += delegate {
                string[] choices = { "ko-KR", "en-US", "ja-JP" };
                SelectedLanguage = choices[languages.SelectedIndex];
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(languages);
        }
    }

    private sealed class RuntimeInstallDialog : Form
    {
        private readonly Label status = new Label();
        private readonly ProgressBar bar = new ProgressBar();
        private readonly string language;

        internal RuntimeInstallDialog(string selected)
        {
            language = selected;
            Text = Translate(language, ".NET 8 설치", "Install .NET 8",
                ".NET 8 のインストール");
            ClientSize = new Size(540, 140);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ControlBox = false;
            status.Text = Translate(language,
                "Microsoft .NET 8 Desktop Runtime을 설치 중입니다...",
                "Installing Microsoft .NET 8 Desktop Runtime...",
                "Microsoft .NET 8 Desktop Runtime をインストールしています...");
            status.Location = new Point(20, 22);
            status.Size = new Size(500, 45);
            Controls.Add(status);
            bar.Style = ProgressBarStyle.Marquee;
            bar.Location = new Point(20, 80);
            bar.Size = new Size(500, 18);
            Controls.Add(bar);

            Shown += delegate {
                var worker = new BackgroundWorker();
                worker.DoWork += delegate { InstallDesktopRuntime(); };
                worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e) {
                    if (e.Error != null)
                    {
                        MessageBox.Show(this, e.Error.Message,
                            Translate(language, "설치 실패", "Installation failed", "インストール失敗"),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        DialogResult = DialogResult.Cancel;
                    }
                    else DialogResult = DialogResult.OK;
                    Close();
                };
                worker.RunWorkerAsync();
            };
        }
    }
}
