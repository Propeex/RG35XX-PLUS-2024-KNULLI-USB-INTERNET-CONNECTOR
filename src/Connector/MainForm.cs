using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// One-button UI for the transitional, SHA-pinned legacy USB engine.
/// Never represents ADB presence or an active marker as proven Internet health.
/// No update progress appears here: BootstrapForm finishes startup first.
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
    private readonly UsbEngineRunner _engine = new();

    private string _selectedLanguage = UiStrings.DefaultLanguage;
    private ConnectionState _state = ConnectionState.Checking;
    private bool _engineInstalled;
    private bool _adbReady;
    private bool _busy;
    private bool _closingAfterStop;
    private bool _mayHaveActiveSession;
    private int _polling;
    private bool _closed;
    private string _lastAdbDiagnostic = "";
    private DateTimeOffset _lastHealthCheck = DateTimeOffset.MinValue;

    internal MainForm(IReadOnlyList<string> startupLogs)
    {
        Text = "RG35XX+ (2024) KNULLI USB Internet Connector";
        ClientSize = new Size(900, 470);
        MinimumSize = new Size(850, 420);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        BuildLayout();
        _engineInstalled = UsbBackendBundle.Installed;
        // A previous GUI instance may have been closed while its SSH tunnel
        // remained alive. Never kill that ADB server merely to poll status.
        _mayHaveActiveSession = File.Exists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RG35XX_USB_FullNet", "ssh.pid"));
        _selectedLanguage = LanguageSettings.Load();
        _language.SelectedIndex = Array.FindIndex(UiStrings.Languages,
            x => x.Code == _selectedLanguage);
        ApplyLanguage();

        foreach (string line in startupLogs) AppendLog(line);
        if (!_engineInstalled)
        {
            AppendLog("Embedded USB backend missing or invalid; connection is disabled.");
            AppendLog("This development build has no verified embedded backend. Consult the GitHub build log.");
        }
        else
        {
            AppendLog("Embedded source-tracked USB backend available (hardware re-test required).");
        }

        _statusTimer.Interval = 9000;
        _statusTimer.Tick += async (_, _) => await PollDeviceAsync();
        Shown += async (_, _) =>
        {
            await PollDeviceAsync();
            if (!_closed) _statusTimer.Start();
        };
        FormClosing += OnFormClosing;
        FormClosed += (_, _) =>
        {
            _closed = true;
            _statusTimer.Stop();
            _statusTimer.Dispose();
        };
    }

    private void BuildLayout()
    {
        _title.Location = new Point(24, 18);
        _title.Size = new Size(615, 44);
        _title.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        _title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_title);

        _languageLabel.Location = new Point(662, 23);
        _languageLabel.Size = new Size(100, 30);
        _languageLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _languageLabel.Text = "Language";
        Controls.Add(_languageLabel);

        _language.DropDownStyle = ComboBoxStyle.DropDownList;
        _language.Location = new Point(767, 18);
        _language.Size = new Size(112, 32);
        _language.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        foreach (var language in UiStrings.Languages)
            _language.Items.Add(language.Display);
        _language.SelectedIndexChanged += (_, _) =>
        {
            int selected = _language.SelectedIndex;
            if (selected < 0) return;
            _selectedLanguage = UiStrings.Languages[selected].Code;
            ApplyLanguage();
            try { LanguageSettings.Save(_selectedLanguage); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                AppendLog("Cannot save language preference: " + e.GetType().Name);
            }
            AppendLog("Language switched to " + _selectedLanguage + ".");
        };
        Controls.Add(_language);

        _status.Location = new Point(25, 74);
        _status.Size = new Size(850, 35);
        _status.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        _status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_status);

        _connectToggle.Location = new Point(288, 140);
        _connectToggle.Size = new Size(324, 62);
        _connectToggle.BackColor = Color.FromArgb(38, 112, 222);
        _connectToggle.ForeColor = Color.White;
        _connectToggle.FlatStyle = FlatStyle.Flat;
        _connectToggle.FlatAppearance.BorderSize = 0;
        _connectToggle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _connectToggle.Click += async (_, _) =>
            await RunActionAsync(ShouldDisconnect
                ? EngineAction.Stop : EngineAction.Start, false);
        Controls.Add(_connectToggle);

        _logLabel.Text = "Activity Log";
        _logLabel.Location = new Point(24, 227);
        _logLabel.Size = new Size(290, 28);
        _logLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        Controls.Add(_logLabel);

        _log.Location = new Point(24, 259);
        _log.Size = new Size(852, 186);
        _log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.WordWrap = false;
        _log.ScrollBars = ScrollBars.Both;
        _log.BackColor = Color.White;
        _log.Font = new Font("Consolas", 9F);
        Controls.Add(_log);

        Resize += (_, _) =>
            _connectToggle.Left = (ClientSize.Width - _connectToggle.Width) / 2;
    }

    private bool ShouldDisconnect => _state == ConnectionState.Connected
        || (_mayHaveActiveSession && !_adbReady);

    private async Task RunActionAsync(EngineAction action, bool closeAfterStop)
    {
        if (_busy || !_engineInstalled || (!_adbReady && action != EngineAction.Stop))
            return;
        _busy = true;
        _statusTimer.Stop();
        SetState(action == EngineAction.Start
            ? ConnectionState.Connecting : ConnectionState.Disconnecting);
        try
        {
            if (action == EngineAction.Start) _mayHaveActiveSession = true;
            var outcome = await _engine.RunAsync(action, AppendLog, CancellationToken.None);
            if (action == EngineAction.Stop)
            {
                if (!outcome.Success)
                {
                    // The legacy STOP cleans Windows SSH and ADB even if the
                    // device was unplugged. Recheck ownership marker locally.
                    _mayHaveActiveSession = File.Exists(Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "RG35XX_USB_FullNet", "ssh.pid"));
                    SetState(ConnectionState.Error);
                    AppendLog("Device-side STOP did not complete. No success has been assumed.");
                }
                else
                {
                    _mayHaveActiveSession = false;
                    _lastHealthCheck = DateTimeOffset.MinValue;
                    SetState(ConnectionState.Ready);
                    AppendLog("USB internet session disconnected; app ADB tunnel stopped.");
                }
            }
            else if (outcome.Success)
            {
                // The backend already tests native HTTPS. Re-test from an
                // independent ADB process before displaying Connected.
                bool healthy = await UsbEngineRunner.NativeInternetHealthyAsync(
                    CancellationToken.None);
                _lastHealthCheck = DateTimeOffset.UtcNow;
                SetState(healthy ? ConnectionState.Connected : ConnectionState.Error);
                if (!healthy)
                    AppendLog("Backend returned success but independent native HTTPS failed.");
            }
            else
            {
                SetState(ConnectionState.Error);
            }

            if (closeAfterStop && action == EngineAction.Stop && outcome.Success)
            {
                _closingAfterStop = true;
                Close();
            }
        }
        catch (Exception ex)
        {
            SetState(ConnectionState.Error);
            AppendLog("USB backend action failed: " + ex.GetType().Name);
            AppendLog("Details: " + ex.Message);
            if (closeAfterStop)
                MessageBox.Show(this, "STOP failed; the connection might still be active.",
                    "USB Internet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _busy = false;
            ApplyLanguage();
            if (!_closed && !_closingAfterStop) _statusTimer.Start();
        }
    }

    private async Task PollDeviceAsync()
    {
        if (_busy || _closed || Interlocked.Exchange(ref _polling, 1) != 0) return;
        try
        {
            // Never kill the ADB server when SSH forwarding depends on it.
            bool keepAdbServer = _mayHaveActiveSession
                || _state == ConnectionState.Connected;
            bool ready = await AdbProbe.HasReadyDeviceAsync(keepAdbServer);
            if (_closed || IsDisposed || _busy) return;
            string detail = AdbProbe.LastDiagnostic;
            if (detail != _lastAdbDiagnostic)
            {
                _lastAdbDiagnostic = detail;
                AppendLog(detail);
            }
            _adbReady = ready;

            if (!ready)
            {
                if (_state == ConnectionState.Connected)
                    AppendLog("USB device was disconnected; native Internet is no longer verified.");
                SetState(ConnectionState.Disconnected);
                return;
            }
            if (!_engineInstalled)
            {
                SetState(ConnectionState.NotReady);
                return;
            }

            if (_mayHaveActiveSession && DateTimeOffset.UtcNow - _lastHealthCheck
                > TimeSpan.FromSeconds(35))
            {
                bool healthy = await UsbEngineRunner.NativeInternetHealthyAsync(
                    CancellationToken.None);
                _lastHealthCheck = DateTimeOffset.UtcNow;
                if (_closed || IsDisposed || _busy) return;
                if (_state == ConnectionState.Connected && !healthy)
                {
                    SetState(ConnectionState.Error);
                    AppendLog("Native Internet health check failed. Select Connect to attempt recovery.");
                }
                else if (healthy && _state != ConnectionState.Connected)
                {
                    SetState(ConnectionState.Connected);
                    AppendLog("Native Internet health check succeeded.");
                }
            }
            else if (!_mayHaveActiveSession)
            {
                SetState(ConnectionState.Ready);
            }
        }
        catch (Exception ex)
        {
            if (_closed || IsDisposed) return;
            SetState(ConnectionState.Error);
            AppendLog("ADB device probe failed: " + ex.GetType().Name);
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
            AppendLog("Connection state changed: " + next);
        }
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        if (IsDisposed) return;
        string T(string key) => UiStrings.Get(_selectedLanguage, key);
        _title.Text = T("Title");
        _status.Text = T("StatusPrefix") + ": " + T(_state.ToString());
        _status.ForeColor = _state switch
        {
            ConnectionState.Connected => Color.FromArgb(24, 129, 82),
            ConnectionState.Error => Color.FromArgb(189, 49, 49),
            _ => Color.FromArgb(75, 86, 100)
        };
        _connectToggle.Text = T(ShouldDisconnect ? "Disconnect" : "Connect");
        bool allowedConnected = _adbReady &&
            (_state == ConnectionState.Ready || _state == ConnectionState.Connected
             || _state == ConnectionState.Error);
        _connectToggle.Enabled = !_busy && _engineInstalled
            && (allowedConnected || (!_adbReady && _mayHaveActiveSession));
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closingAfterStop) return;
        if (_busy)
        {
            e.Cancel = true;
            MessageBox.Show(this, "A USB operation is still running.",
                "USB Internet", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (!_mayHaveActiveSession) return;

        // Do not silently strand SSH forwards when the GUI is closed.
        var answer = MessageBox.Show(this,
            "Disconnect USB internet before exiting?\n\n" +
            "Yes: Disconnect safely and exit.\n" +
            "No: Keep the connection running in the background.\n" +
            "Cancel: Return to the application.",
            "USB Internet", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (answer == DialogResult.Cancel)
        {
            e.Cancel = true;
            return;
        }
        if (answer == DialogResult.No) return;
        e.Cancel = true;
        await RunActionAsync(EngineAction.Stop, closeAfterStop: true);
    }

    private void AppendLog(string message)
    {
        if (_closed || IsDisposed) return;
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => AppendLog(message)));
            return;
        }
        if (_log.TextLength > 0) _log.AppendText(Environment.NewLine);
        _log.AppendText($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
    }
}
