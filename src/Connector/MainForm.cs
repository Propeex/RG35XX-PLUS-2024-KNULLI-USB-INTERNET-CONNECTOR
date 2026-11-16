using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// First tracked GUI foundation. It intentionally does not claim that a USB
/// internet session works until the proven backend is migrated and verified.
/// See GitHub issue #4. Never substitute simulated progress for real work.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly Label _title = new();
    private readonly Label _status = new();
    private readonly Label _languageLabel = new();
    private readonly ComboBox _language = new();
    private readonly Button _connectToggle = new();
    private readonly Label _appUpdateLabel = new();
    private readonly Label _toolsUpdateLabel = new();
    private readonly ProgressBar _appProgress = new();
    private readonly ProgressBar _toolsProgress = new();
    private readonly Label _logLabel = new();
    private readonly TextBox _log = new();
    private string _selectedLanguage = UiStrings.DefaultLanguage;
    private ConnectionState _state = ConnectionState.NotReady;

    public MainForm()
    {
        Text = "RG35XX+ (2024) KNULLI USB Internet Connector";
        ClientSize = new Size(760, 605);
        MinimumSize = new Size(680, 525);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        BuildLayout();
        _language.SelectedIndex = 0;
        ApplyLanguage();
        _connectToggle.Enabled = false; // Never offer a fake Connect action.
        AppendLog("GUI source foundation started.");
        AppendLog("USB network engine is not packaged in this development build.");
        AppendLog("No simulated device connection, progress or update check is reported.");
    }

    private void BuildLayout()
    {
        _title.Location = new Point(24, 18);
        _title.Size = new Size(540, 40);
        _title.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        _title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_title);

        _languageLabel.Location = new Point(574, 23);
        _languageLabel.Size = new Size(68, 30);
        _languageLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(_languageLabel);

        _language.DropDownStyle = ComboBoxStyle.DropDownList;
        _language.Location = new Point(638, 18);
        _language.Size = new Size(100, 32);
        _language.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        foreach (var item in UiStrings.Languages)
            _language.Items.Add(item.Display);
        _language.SelectedIndexChanged += (_, _) =>
        {
            var i = _language.SelectedIndex;
            if (i < 0) return;
            _selectedLanguage = UiStrings.Languages[i].Code;
            ApplyLanguage();
            AppendLog($"Language switched to {_selectedLanguage}.");
        };
        Controls.Add(_language);

        _status.Location = new Point(26, 70);
        _status.Size = new Size(685, 30);
        _status.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_status);

        _appUpdateLabel.Location = new Point(25, 111);
        _appUpdateLabel.Size = new Size(250, 24);
        Controls.Add(_appUpdateLabel);
        _appProgress.Location = new Point(270, 113);
        _appProgress.Size = new Size(468, 18);
        _appProgress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _appProgress.Visible = false; // only shown during a real update operation
        Controls.Add(_appProgress);

        _toolsUpdateLabel.Location = new Point(25, 144);
        _toolsUpdateLabel.Size = new Size(250, 24);
        Controls.Add(_toolsUpdateLabel);
        _toolsProgress.Location = new Point(270, 146);
        _toolsProgress.Size = new Size(468, 18);
        _toolsProgress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _toolsProgress.Visible = false;
        Controls.Add(_toolsProgress);

        _connectToggle.Location = new Point(218, 203);
        _connectToggle.Size = new Size(324, 62);
        _connectToggle.Anchor = AnchorStyles.Top;
        _connectToggle.BackColor = Color.FromArgb(38, 112, 222);
        _connectToggle.ForeColor = Color.White;
        _connectToggle.FlatStyle = FlatStyle.Flat;
        _connectToggle.FlatAppearance.BorderSize = 0;
        _connectToggle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _connectToggle.Click += (_, _) =>
        {
            // The button is disabled until the real USB engine is integrated.
            AppendLog("Connection requested, but the verified backend is not available.");
            SetState(ConnectionState.Error);
        };
        Controls.Add(_connectToggle);

        _logLabel.Location = new Point(24, 296);
        _logLabel.Size = new Size(290, 30);
        _logLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        Controls.Add(_logLabel);

        _log.Location = new Point(24, 329);
        _log.Size = new Size(712, 250);
        _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.WordWrap = false;
        _log.ScrollBars = ScrollBars.Both;
        _log.BackColor = Color.White;
        _log.Font = new Font("Consolas", 9F);
        Controls.Add(_log);

        Resize += (_, _) =>
        {
            // Keep the one main action in the center when resizing.
            _connectToggle.Left = (ClientSize.Width - _connectToggle.Width) / 2;
        };
    }

    private void SetState(ConnectionState state)
    {
        _state = state;
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        string Translate(string key) => UiStrings.Get(_selectedLanguage, key);
        _title.Text = Translate("Title");
        _languageLabel.Text = Translate("Language");
        _status.Text = Translate("StatusPrefix") + ": " + Translate(_state.ToString());
        _status.ForeColor = _state switch
        {
            ConnectionState.Connected => Color.FromArgb(24, 129, 82),
            ConnectionState.Error => Color.FromArgb(189, 49, 49),
            _ => Color.FromArgb(75, 86, 100)
        };
        _connectToggle.Text = Translate(_state == ConnectionState.Connected ? "Disconnect" : "Connect");
        _appUpdateLabel.Text = Translate("AppUpdate");
        _toolsUpdateLabel.Text = Translate("ToolsUpdate");
        _logLabel.Text = Translate("Log");
    }

    private void AppendLog(string message)
    {
        if (_log.TextLength > 0) _log.AppendText(Environment.NewLine);
        _log.AppendText($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
    }
}
