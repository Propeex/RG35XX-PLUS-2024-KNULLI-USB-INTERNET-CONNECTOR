using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Minimal main UI. Never display update progress here: updates finish in
/// BootstrapForm before this window is constructed.
/// The network engine is not migrated yet, so the toggle is intentionally disabled.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly Label _title = new();
    private readonly Label _status = new();
    private readonly Label _languageLabel = new();
    private readonly ComboBox _language = new();
    private readonly Button _connectToggle = new();
    private readonly Label _logLabel = new();
    private readonly TextBox _log = new();
    private readonly System.Windows.Forms.Timer _statusTimer = new();

    private string _selectedLanguage = UiStrings.DefaultLanguage;
    private ConnectionState _state = ConnectionState.Checking;
    private int _polling;
    private bool _closed;

    internal MainForm(IReadOnlyList<string> startupLogs)
    {
        Text = "RG35XX+ (2024) KNULLI USB Internet Connector";
        ClientSize = new Size(760, 470);
        MinimumSize = new Size(670, 420);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        BuildLayout();
        _language.SelectedIndex = 0;
        ApplyLanguage();

        _connectToggle.Enabled = false; // Enable only after issue #4 is complete.
        foreach (string line in startupLogs) AppendLog(line);
        AppendLog("GUI ready. Real USB network engine integration pending (#4).");

        _statusTimer.Interval = 7000;
        _statusTimer.Tick += async (_, _) => await PollDeviceAsync();
        Shown += async (_, _) =>
        {
            await PollDeviceAsync();
            if (!_closed) _statusTimer.Start();
        };
        FormClosing += (_, _) =>
        {
            _closed = true;
            _statusTimer.Stop();
        };
    }

    private void BuildLayout()
    {
        _title.Location = new Point(24, 18);
        _title.Size = new Size(537, 44);
        _title.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        _title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_title);

        _languageLabel.Location = new Point(564, 23);
        _languageLabel.Size = new Size(74, 30);
        _languageLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(_languageLabel);

        _language.DropDownStyle = ComboBoxStyle.DropDownList;
        _language.Location = new Point(637, 18);
        _language.Size = new Size(101, 32);
        _language.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        foreach (var item in UiStrings.Languages) _language.Items.Add(item.Display);
        _language.SelectedIndexChanged += (_, _) =>
        {
            int selected = _language.SelectedIndex;
            if (selected < 0) return;
            _selectedLanguage = UiStrings.Languages[selected].Code;
            ApplyLanguage();
            AppendLog($"Language switched to {_selectedLanguage}.");
        };
        Controls.Add(_language);

        _status.Location = new Point(25, 74);
        _status.Size = new Size(700, 35);
        _status.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_status);

        _connectToggle.Location = new Point(218, 140);
        _connectToggle.Size = new Size(324, 62);
        _connectToggle.BackColor = Color.FromArgb(38, 112, 222);
        _connectToggle.ForeColor = Color.White;
        _connectToggle.FlatStyle = FlatStyle.Flat;
        _connectToggle.FlatAppearance.BorderSize = 0;
        _connectToggle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _connectToggle.Click += (_, _) =>
        {
            AppendLog("Connect/Disconnect requires tested KNULLI network engine (#4).");
        };
        Controls.Add(_connectToggle);

        _logLabel.Location = new Point(24, 227);
        _logLabel.Size = new Size(290, 28);
        _logLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        Controls.Add(_logLabel);

        _log.Location = new Point(24, 259);
        _log.Size = new Size(712, 186);
        _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.WordWrap = false;
        _log.ScrollBars = ScrollBars.Both;
        _log.BackColor = Color.White;
        _log.Font = new Font("Consolas", 9F);
        Controls.Add(_log);

        Resize += (_, _) => _connectToggle.Left = (ClientSize.Width - _connectToggle.Width) / 2;
    }

    private async Task PollDeviceAsync()
    {
        if (Interlocked.Exchange(ref _polling, 1) != 0) return;
        try
        {
            var connected = await AdbProbe.HasReadyDeviceAsync();
            if (_closed || IsDisposed) return;
            SetState(connected ? ConnectionState.Ready : ConnectionState.Disconnected);
        }
        catch (Exception ex)
        {
            if (_closed || IsDisposed) return;
            SetState(ConnectionState.Error);
            AppendLog("ADB polling failed: " + ex.GetType().Name);
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    private void SetState(ConnectionState next)
    {
        if (_state != next)
        {
            _state = next;
            AppendLog("Device state changed: " + next);
        }
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        string T(string key) => UiStrings.Get(_selectedLanguage, key);
        _title.Text = T("Title");
        _languageLabel.Text = "Language";
        _status.Text = T("StatusPrefix") + ": " + T(_state.ToString());
        _status.ForeColor = _state switch
        {
            ConnectionState.Connected => Color.FromArgb(24, 129, 82),
            ConnectionState.Error => Color.FromArgb(189, 49, 49),
            _ => Color.FromArgb(75, 86, 100)
        };
        _connectToggle.Text = T(_state == ConnectionState.Connected ? "Disconnect" : "Connect");
        _logLabel.Text = "Activity Log";
    }

    private void AppendLog(string message)
    {
        if (_log.TextLength > 0) _log.AppendText(Environment.NewLine);
        _log.AppendText($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
    }
}
