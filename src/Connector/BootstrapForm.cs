using System.Drawing;
using System.Windows.Forms;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// All prerequisites finish before MainForm is constructed. Only the first
/// startup may display a progress bar. On a new portable installation the
/// user chooses language BEFORE any Google SDK license question.
/// </summary>
internal sealed class BootstrapForm : Form
{
    private readonly Label _heading = new();
    private readonly Label _stage = new();
    private readonly Label _languageLabel = new();
    private readonly ComboBox _languageCombo = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _retry = new();
    private readonly TextBox _details = new();
    private readonly List<string> _events = new();

    private readonly bool _firstLaunch = !File.Exists(AppPaths.InitializedMarker);
    private readonly bool _chooseLanguage = !LanguageSettings.IsConfigured;
    private string _selectedLanguage = LanguageSettings.Load();
    private bool _busy;
    private bool _languagePromptPending;

    internal BootstrapForm()
    {
        Text = _chooseLanguage ? "Language" : "RG35XX+ (2024) Connector — Startup";
        ClientSize = new Size(580, _chooseLanguage ? 145 : _firstLaunch ? 322 : 145);
        _languagePromptPending = _chooseLanguage;
        MinimumSize = MaximumSize = Size;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);

        _heading.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        _heading.Visible = !_chooseLanguage;
        _heading.Location = new Point(22, 15);
        _heading.Size = new Size(530, 37);
        Controls.Add(_heading);

        _languageLabel.Location = new Point(22, 65);
        _languageLabel.Size = new Size(145, 29);
        _languageLabel.Visible = _chooseLanguage;
        Controls.Add(_languageLabel);

        _languageCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _languageCombo.Location = new Point(174, 60);
        _languageCombo.Size = new Size(160, 32);
        _languageCombo.Visible = _chooseLanguage;
        foreach (var item in UiStrings.Languages) _languageCombo.Items.Add(item.Display);
        _languageCombo.SelectedIndexChanged += async (_, _) =>
        {
            int i = _languageCombo.SelectedIndex;
            if (i < 0 || !_languagePromptPending) return;
            _selectedLanguage = UiStrings.Languages[i].Code;
            try
            {
                LanguageSettings.Save(_selectedLanguage);
                _languagePromptPending = false;
                ActivateLocalizedStartup();
                await StartAsync();
            }
            catch (Exception ex)
            {
                _stage.Text = T("Failed");
                Log("Language selection failed: " + ex.GetType().Name);
                MessageBox.Show(this, ex.Message, T("Failed"),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        Controls.Add(_languageCombo);

        _stage.Visible = !_chooseLanguage;
        _stage.Location = new Point(22, 61);
        _stage.Size = new Size(531, 30);
        Controls.Add(_stage);

        // Do not animate a progress bar while waiting for language choice.
        _progress.Visible = _firstLaunch && !_chooseLanguage;
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 35;
        _progress.Location = new Point(22, 101);
        _progress.Size = new Size(532, 18);
        Controls.Add(_progress);

        _details.Visible = _firstLaunch && !_chooseLanguage;
        _details.Multiline = true;
        _details.ReadOnly = true;
        _details.ScrollBars = ScrollBars.Vertical;
        _details.Font = new Font("Consolas", 9F);
        _details.Location = new Point(22, 134);
        _details.Size = new Size(532, 76);
        Controls.Add(_details);

        _retry.Visible = false;
        _retry.Location = new Point(436, _firstLaunch ? 268 : 104);
        _retry.Size = new Size(118, 32);
        _retry.Click += async (_, _) => await StartAsync();
        Controls.Add(_retry);

        if (!_chooseLanguage)
            _languageCombo.SelectedIndex = Array.FindIndex(UiStrings.Languages,
                x => x.Code == _selectedLanguage);
        // On a new installation no locale is preselected: the first visible
        // screen contains only the literal English "Language" and its choices.
        RefreshLabels();

        Shown += async (_, _) =>
        {
            if (!_chooseLanguage) await StartAsync();
        };
    }

    private void ActivateLocalizedStartup()
    {
        _languageLabel.Visible = false;
        _languageCombo.Visible = false;
        _heading.Visible = true;
        _stage.Visible = true;
        _progress.Visible = _firstLaunch;
        _details.Visible = _firstLaunch;
        // Lift the compact language-dialog bounds before expanding to the
        // localized setup screen; otherwise MaximumSize clips the controls.
        MinimumSize = Size.Empty;
        MaximumSize = Size.Empty;
        ClientSize = new Size(580, _firstLaunch ? 322 : 145);
        MinimumSize = MaximumSize = Size;
        Text = T(_firstLaunch ? "Welcome" : "Checking");
        RefreshLabels();
    }

    private string T(string key)
    {
        var ko = new Dictionary<string, string>
        {
            ["Welcome"] = "첫 실행 설정",
            ["Checking"] = "업데이트 확인 중",
            ["LanguageChoice"] = "사용할 언어",
            ["Continue"] = "설치 시작",
            ["Retry"] = "다시 시도",
            ["Initializing"] = "설치 준비 중...",
            ["SDKTitle"] = "Google Android SDK 사용 동의",
            ["SDKQuestion"] = "이 프로그램은 Google의 공식 Android SDK Platform-Tools를 다운로드합니다. 아래 약관을 확인한 후 동의하시면 설치를 진행합니다.\n\nhttps://developer.android.com/studio/terms\n\n약관에 동의하시겠습니까?",
            ["Failed"] = "시작 실패 — 다시 시도하거나 창을 닫으세요.",
            ["CheckApp"] = "프로그램 업데이트 확인 중...",
            ["CheckTools"] = "Platform-Tools 버전 확인 중...",
            ["DownloadTools"] = "Platform-Tools 다운로드 중...",
            ["VerifyTools"] = "Platform-Tools 검증 중...",
            ["InstallTools"] = "Platform-Tools 설치 중...",
            ["Backend"] = "인터넷 연결 엔진 검사 중...",
            ["Done"] = "준비 완료."
        };
        var en = new Dictionary<string, string>
        {
            ["Welcome"] = "First-time setup",
            ["Checking"] = "Checking for updates",
            ["LanguageChoice"] = "Language",
            ["Continue"] = "Start setup",
            ["Retry"] = "Retry",
            ["Initializing"] = "Preparing...",
            ["SDKTitle"] = "Google Android SDK License",
            ["SDKQuestion"] = "This app downloads Google's official Android SDK Platform-Tools. Please review the terms and accept to continue.\n\nhttps://developer.android.com/studio/terms\n\nDo you agree to the terms?",
            ["Failed"] = "Startup failed — retry or close this window.",
            ["CheckApp"] = "Checking application updates...",
            ["CheckTools"] = "Checking Platform-Tools revision...",
            ["DownloadTools"] = "Downloading Platform-Tools...",
            ["VerifyTools"] = "Verifying Platform-Tools...",
            ["InstallTools"] = "Installing Platform-Tools...",
            ["Backend"] = "Checking USB internet backend...",
            ["Done"] = "Ready."
        };
        var ja = new Dictionary<string, string>
        {
            ["Welcome"] = "初回セットアップ",
            ["Checking"] = "更新を確認中",
            ["LanguageChoice"] = "言語",
            ["Continue"] = "セットアップ開始",
            ["Retry"] = "再試行",
            ["Initializing"] = "準備中...",
            ["SDKTitle"] = "Google Android SDK ライセンス",
            ["SDKQuestion"] = "このアプリは Google 公式の Android SDK Platform-Tools をダウンロードします。利用規約を確認し、同意する場合は続行してください。\n\nhttps://developer.android.com/studio/terms\n\n同意しますか？",
            ["Failed"] = "起動できませんでした — 再試行してください。",
            ["CheckApp"] = "アプリの更新を確認中...",
            ["CheckTools"] = "Platform-Tools のバージョンを確認中...",
            ["DownloadTools"] = "Platform-Tools をダウンロード中...",
            ["VerifyTools"] = "Platform-Tools を検証中...",
            ["InstallTools"] = "Platform-Tools をインストール中...",
            ["Backend"] = "USB インターネットエンジンを確認中...",
            ["Done"] = "準備完了。"
        };
        var dictionary = _selectedLanguage switch
        {
            "en-US" => en,
            "ja-JP" => ja,
            _ => ko
        };
        return dictionary.TryGetValue(key, out var value) ? value : key;
    }

    private void RefreshLabels()
    {
        if (_languagePromptPending)
        {
            _languageLabel.Text = "Language";
            return;
        }
        _heading.Text = T(_firstLaunch ? "Welcome" : "Checking");
        _retry.Text = T("Retry");
        if (!_busy) _stage.Text = T("Initializing");
    }

    private async Task StartAsync()
    {
        if (_busy || (_chooseLanguage && !LanguageSettings.IsConfigured)) return;
        _busy = true;
        _retry.Visible = false;
        try
        {
            if (!File.Exists(AppPaths.SdkLicenseAcceptedMarker))
            {
                var consent = MessageBox.Show(this, T("SDKQuestion"), T("SDKTitle"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (consent != DialogResult.Yes)
                    throw new InvalidOperationException("Android SDK license was not accepted.");
                Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.SdkLicenseAcceptedMarker)!);
                File.WriteAllText(AppPaths.SdkLicenseAcceptedMarker, DateTimeOffset.UtcNow.ToString("O"));
            }

            var reporter = new Progress<StartupStep>(UpdateStage);
            var svc = new StartupService();
            bool updateLaunching = await svc.PrepareAsync(reporter, Log, CancellationToken.None);
            if (updateLaunching)
            {
                Close();
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AppPaths.InitializedMarker)!);
            File.WriteAllText(AppPaths.InitializedMarker, DateTimeOffset.UtcNow.ToString("O"));
            Hide();
            using (var main = new MainForm())
                main.ShowDialog();
            Close();
        }
        catch (Exception ex)
        {
            _stage.Text = T("Failed");
            Log("Startup failed: " + ex.Message);
            _retry.Visible = true;
            _retry.Enabled = true;
            if (_firstLaunch) _progress.Style = ProgressBarStyle.Continuous;
            if (!_firstLaunch)
                MessageBox.Show(this, ex.Message, T("Failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _busy = false;
        }
    }

    private void UpdateStage(StartupStep step)
    {
        if (IsDisposed) return;
        string raw = step.Message;
        string key = raw.StartsWith("Checking application", StringComparison.Ordinal) ? "CheckApp"
            : raw.StartsWith("Checking Android", StringComparison.Ordinal) ? "CheckTools"
            : raw.StartsWith("Downloading Platform", StringComparison.Ordinal) ? "DownloadTools"
            : raw.StartsWith("Verifying Platform", StringComparison.Ordinal) ? "VerifyTools"
            : raw.StartsWith("Installing Platform", StringComparison.Ordinal) ? "InstallTools"
            : raw.StartsWith("Verifying portable", StringComparison.Ordinal) ? "Backend"
            : raw.StartsWith("Startup checks completed", StringComparison.Ordinal) ? "Done"
            : "Initializing";
        _stage.Text = T(key);
        if (!_firstLaunch) return;
        if (step.BytesTotal.HasValue && step.BytesTotal.Value > 0)
        {
            _progress.Style = ProgressBarStyle.Continuous;
            _progress.Value = (int)Math.Clamp(step.BytesReceived * 100.0 / step.BytesTotal.Value, 0, 100);
        }
        else
        {
            _progress.Style = ProgressBarStyle.Marquee;
        }
    }

    private void Log(string message)
    {
        _events.Add(message); // English diagnostics, regardless of selected language
        if (_firstLaunch && !IsDisposed)
        {
            if (_details.TextLength > 0) _details.AppendText(Environment.NewLine);
            _details.AppendText(message);
        }
    }
}
