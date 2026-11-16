using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Device readiness and native Internet health are independent facts.
/// The GUI shows only two values for each; operations and errors belong in the log.
/// No previous bootstrap or process logs are replayed when this window opens.
/// </summary>
internal sealed class MainForm : Form
{
    private const int WmDeviceChange = 0x0219;
    private const int WmPowerBroadcast = 0x0218;
    private const int PbtApmSuspend = 0x0004;

    private readonly Label _title = new();
    private readonly Label _deviceCaption = new();
    private readonly Label _deviceValue = new();
    private readonly Label _internetCaption = new();
    private readonly Label _internetValue = new();
    private readonly ComboBox _language = new();
    private readonly Button _connectToggle = new();
    private readonly Label _logLabel = new();
    private readonly TextBox _log = new();
    private readonly System.Windows.Forms.Timer _statusTimer = new();
    private readonly UsbEngineRunner _engine = new();

    private string _selectedLanguage = UiStrings.DefaultLanguage;
    private bool _engineInstalled;
    private bool _adbReady;
    private bool _internetHealthy;
    private bool _busy;
    private bool _closingAfterStop;
    private bool _mayHaveActiveSession;
    private bool _languageInitialized;
    private bool _closed;
    private int _polling;
    private int _healthPolling;
    private int _deviceGeneration;
    private int _missingDeviceChecksDuringAction;
    private CancellationTokenSource? _actionCancellation;
    private string _lastAdbDiagnostic = "";
    private DateTimeOffset _lastHealthCheck = DateTimeOffset.MinValue;

    private static string SshPidFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RG35XX_USB_FullNet", "ssh.pid");

    internal MainForm()
    {
        Text = "RG35XX+ (2024) KNULLI USB Internet Connector";
        ClientSize = new Size(900, 470);
        MinimumSize = new Size(850, 420);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);

        BuildLayout();
        _engineInstalled = UsbBackendBundle.Installed;
        _mayHaveActiveSession = File.Exists(SshPidFile);
        _selectedLanguage = LanguageSettings.Load();
        _language.SelectedIndex = Array.FindIndex(UiStrings.Languages,
            x => x.Code == _selectedLanguage);
        _languageInitialized = true;
        ApplyLanguage();

        // The Activity Log is intentionally blank for each new GUI session.
        // Current-session USB/ADB checks and operations will be appended below.
        if (!_engineInstalled)
            AppendLog("Portable backend is missing or invalid. Extract the complete ZIP again.");

        _statusTimer.Interval = 2500;
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
            // Launch idle-server shutdown before the WinForms message loop
            // exits. Never terminate an ADB server needed by an owned tunnel.
            if (!_mayHaveActiveSession && AdbProbe.ServerPort == "5056")
                _ = AdbProbe.StopDedicatedAsync();
        };
    }

    private void BuildLayout()
    {
        _title.Location = new Point(24, 18);
        _title.Size = new Size(640, 44);
        _title.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        _title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_title);

        // Show the currently chosen language, not a second "Language" heading.
        _language.DropDownStyle = ComboBoxStyle.DropDownList;
        _language.Location = new Point(748, 18);
        _language.Size = new Size(130, 32);
        _language.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        foreach (var language in UiStrings.Languages)
            _language.Items.Add(language.Display);
        _language.SelectedIndexChanged += (_, _) =>
        {
            int selected = _language.SelectedIndex;
            if (!_languageInitialized || selected < 0) return;
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

        _deviceCaption.Location = new Point(25, 74);
        _deviceCaption.Size = new Size(125, 35);
        _deviceCaption.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        Controls.Add(_deviceCaption);

        _deviceValue.Location = new Point(150, 74);
        _deviceValue.Size = new Size(155, 35);
        _deviceValue.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        Controls.Add(_deviceValue);

        _internetCaption.Location = new Point(365, 74);
        _internetCaption.Size = new Size(145, 35);
        _internetCaption.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        Controls.Add(_internetCaption);

        _internetValue.Location = new Point(510, 74);
        _internetValue.Size = new Size(180, 35);
        _internetValue.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        Controls.Add(_internetValue);

        _connectToggle.Location = new Point(288, 140);
        _connectToggle.Size = new Size(324, 62);
        _connectToggle.BackColor = Color.FromArgb(38, 112, 222);
        _connectToggle.ForeColor = Color.White;
        _connectToggle.FlatStyle = FlatStyle.Flat;
        _connectToggle.FlatAppearance.BorderSize = 0;
        _connectToggle.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _connectToggle.Click += async (_, _) =>
            await RunActionAsync(ShouldDisconnect ? EngineAction.Stop : EngineAction.Start, false);
        Controls.Add(_connectToggle);

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

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        // Windows USB arrival/removal hints cause a prompt re-probe. Periodic
        // ADB checks remain authoritative (some drivers emit no device event).
        if (m.Msg == WmPowerBroadcast && m.WParam == (IntPtr)PbtApmSuspend)
        {
            CancelActiveOperation("Windows is entering sleep; cancelling current USB action.");
            _adbReady = false;
            _internetHealthy = false;
            _deviceGeneration++;
            ApplyLanguage();
        }
        if (m.Msg == WmDeviceChange && IsHandleCreated && !_closed)
            _ = PollDeviceAsync();
    }

    private bool ShouldDisconnect => _internetHealthy || _mayHaveActiveSession;

    private void CancelActiveOperation(string reason)
    {
        if (!_busy || _actionCancellation is null || _actionCancellation.IsCancellationRequested)
            return;
        AppendLog(reason);
        _actionCancellation.Cancel();
    }

    private async Task RunActionAsync(EngineAction action, bool closeAfterStop)
    {
        if (_busy || !_engineInstalled || (!_adbReady && action != EngineAction.Stop))
            return;
        _busy = true;
        _missingDeviceChecksDuringAction = 0;
        _actionCancellation = new CancellationTokenSource();
        var actionStopwatch = Stopwatch.StartNew();
        // A connection in transition is not a verified healthy connection.
        _internetHealthy = false;
        // Device presence must stay current even during a long SSH/TUN action.
        // Polls run against the selected ADB server only while an action is busy.
        AppendLog(action == EngineAction.Start
            ? "Connecting USB Internet..." : "Disconnecting USB Internet...");
        ApplyLanguage();
        try
        {
            var outcome = await _engine.RunAsync(action, AppendLog, _actionCancellation.Token);
            _mayHaveActiveSession = File.Exists(SshPidFile);
            _lastHealthCheck = DateTimeOffset.MinValue;
            if (action == EngineAction.Stop)
            {
                _internetHealthy = false;
                if (outcome.Success)
                    AppendLog("USB Internet STOP verified. Windows tunnel cleanup requested.");
                else
                    AppendLog("STOP was incomplete; remote cleanup is not confirmed.");
            }
            else if (outcome.Success)
            {
                // An owned SSH session and independent HTTPS proof are both required.
                _internetHealthy = _mayHaveActiveSession &&
                    await UsbEngineRunner.NativeInternetHealthyAsync(_actionCancellation.Token);
                if (_internetHealthy)
                    AppendLog("Independent native HTTPS verification passed.");
                else
                    AppendLog("Internet remains disconnected: independent HTTPS verification failed.");
            }
            else
            {
                _internetHealthy = false;
                AppendLog("USB Internet start failed; see detailed stage messages above.");
            }

            if (closeAfterStop && action == EngineAction.Stop && outcome.Success)
            {
                _closingAfterStop = true;
                Close();
            }
        }
        catch (OperationCanceledException)
        {
            _internetHealthy = false;
            _mayHaveActiveSession = File.Exists(SshPidFile);
            AppendLog("USB action cancelled; connect again from the beginning when the device is ready.");
        }
        catch (Exception ex)
        {
            _internetHealthy = false;
            _mayHaveActiveSession = File.Exists(SshPidFile);
            AppendLog("USB backend action failed: " + ex.GetType().Name);
            AppendLog("Details: " + ex.Message);
            if (closeAfterStop)
                MessageBox.Show(this, "STOP failed; the device may still have active networking.",
                    "USB Internet", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            actionStopwatch.Stop();
            AppendLog("USB action elapsed: " +
                actionStopwatch.Elapsed.TotalSeconds.ToString("0.0") + " seconds.");
            _actionCancellation?.Dispose();
            _actionCancellation = null;
            _busy = false;
            ApplyLanguage();
            if (!_closed && !_closingAfterStop)
            {
                _statusTimer.Start();
                _ = PollDeviceAsync();
            }
        }
    }

    private async Task PollDeviceAsync()
    {
        if (_closed || Interlocked.Exchange(ref _polling, 1) != 0) return;
        try
        {
            bool keepServer = _mayHaveActiveSession || _internetHealthy || _busy;
            // A live tunnel was forwarded through a specific ADB server.
            // Never silently switch ports while its SSH ownership marker exists.
            bool ready = await AdbProbe.HasReadyDeviceAsync(
                keepServer, allowFallback: !_busy && !_mayHaveActiveSession);
            if (_closed || IsDisposed) return;

            string detail = AdbProbe.LastDiagnostic;
            if (detail != _lastAdbDiagnostic)
            {
                _lastAdbDiagnostic = detail;
                AppendLog(detail);
            }
            if (_busy)
            {
                _missingDeviceChecksDuringAction = ready
                    ? 0 : _missingDeviceChecksDuringAction + 1;
                // A single missed ADB response during SSH startup can be
                // transient; cancel only after two consecutive failed probes.
                if (_missingDeviceChecksDuringAction >= 2)
                    CancelActiveOperation("USB device unavailable twice during operation.");
            }
            if (_adbReady != ready)
            {
                _adbReady = ready;
                _deviceGeneration++;
                _lastHealthCheck = DateTimeOffset.MinValue;
                if (!ready)
                {
                    _internetHealthy = false;
                    AppendLog("USB device disconnected; Internet status hidden.");
                }
                else
                    AppendLog("USB device connected; checking Internet separately.");
            }

            if (!_adbReady)
                _internetHealthy = false;
            else if (!_mayHaveActiveSession)
                _internetHealthy = false;
            else if (!_busy)
                _ = CheckInternetHealthAsync();

            ApplyLanguage();
        }
        catch (Exception ex)
        {
            if (_closed || IsDisposed) return;
            _adbReady = false;
            _internetHealthy = false;
            _deviceGeneration++;
            AppendLog("ADB device probe failed: " + ex.GetType().Name);
            ApplyLanguage();
        }
        finally
        {
            Interlocked.Exchange(ref _polling, 0);
        }
    }

    private async Task CheckInternetHealthAsync()
    {
        if (_busy || _closed || !_adbReady || !_mayHaveActiveSession ||
            DateTimeOffset.UtcNow - _lastHealthCheck < TimeSpan.FromSeconds(15) ||
            Interlocked.Exchange(ref _healthPolling, 1) != 0)
            return;
        int generation = _deviceGeneration;
        _lastHealthCheck = DateTimeOffset.UtcNow;
        try
        {
            bool healthy = await UsbEngineRunner.NativeInternetHealthyAsync(CancellationToken.None);
            if (_closed || IsDisposed || _busy || !_adbReady ||
                generation != _deviceGeneration || !_mayHaveActiveSession)
                return;
            if (healthy != _internetHealthy)
                AppendLog(healthy
                    ? "Native Internet HTTPS health check passed."
                    : "Native Internet HTTPS health check failed.");
            _internetHealthy = healthy;
            ApplyLanguage();
        }
        catch (Exception ex)
        {
            if (!_closed && !IsDisposed)
            {
                _internetHealthy = false;
                AppendLog("Native Internet probe failed: " + ex.GetType().Name);
                ApplyLanguage();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _healthPolling, 0);
        }
    }

    private void ApplyLanguage()
    {
        if (IsDisposed) return;
        string T(string key) => UiStrings.Get(_selectedLanguage, key);
        _title.Text = T("Title");
        _deviceCaption.Text = T("DeviceLabel");
        _deviceValue.Text = T(_adbReady ? "DeviceConnected" : "DeviceDisconnected");
        _deviceValue.ForeColor = _adbReady
            ? Color.FromArgb(24, 129, 82) : Color.FromArgb(130, 137, 146);

        _internetCaption.Visible = _adbReady;
        _internetValue.Visible = _adbReady;
        _internetCaption.Text = T("InternetLabel");
        _internetValue.Text = T(_internetHealthy ? "InternetConnected" : "InternetDisconnected");
        _internetValue.ForeColor = _internetHealthy
            ? Color.FromArgb(24, 129, 82) : Color.FromArgb(189, 49, 49);

        _connectToggle.Text = T(ShouldDisconnect ? "Disconnect" : "Connect");
        _connectToggle.Enabled = !_busy && _engineInstalled &&
            (_adbReady || _mayHaveActiveSession && ShouldDisconnect);
        _logLabel.Text = T("Log");
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
