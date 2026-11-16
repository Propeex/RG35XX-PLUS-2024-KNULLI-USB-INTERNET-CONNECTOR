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
    private readonly ProgressBar _actionProgress = new();
    private readonly Label _actionProgressLabel = new();
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
    private int _missingDeviceChecksWhileIdle;
    private int _failedInternetChecks;
    private CancellationTokenSource? _actionCancellation;
    private string _lastAdbDiagnostic = "";
    private DateTimeOffset _lastHealthCheck = DateTimeOffset.MinValue;

    private static string SshPidFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RG35XX_USB_FullNet", "ssh.pid");

    internal MainForm()
    {
        Text = "RG35XX+ (2024) KNULLI USB Internet Connector";
        ClientSize = new Size(900, 530);
        MinimumSize = new Size(850, 480);
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
            // Do not fire-and-forget: when the WinForms message loop exits,
            // an unawaited kill-server may never finish. Port 5037 is shared
            // and is never stopped. An explicitly preserved SSH session keeps
            // the private ADB server alive by design.
            if (!_mayHaveActiveSession)
            {
                // Use the thread pool to avoid WinForms sync-context deadlock
                // while we wait for a bounded owned-server shutdown.
                try { Task.Run(AdbProbe.StopDedicatedAsync).GetAwaiter().GetResult(); }
                catch (Exception e) { System.Diagnostics.Debug.WriteLine(e); }
            }
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

        _actionProgress.Location = new Point(288, 214);
        _actionProgress.Size = new Size(324, 18);
        _actionProgress.Style = ProgressBarStyle.Blocks;
        _actionProgress.Minimum = 0;
        _actionProgress.Maximum = 6;
        _actionProgress.Visible = false;
        Controls.Add(_actionProgress);

        _actionProgressLabel.Location = new Point(288, 237);
        _actionProgressLabel.Size = new Size(500, 24);
        _actionProgressLabel.Visible = false;
        Controls.Add(_actionProgressLabel);

        _logLabel.Location = new Point(24, 269);
        _logLabel.Size = new Size(290, 28);
        _logLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        Controls.Add(_logLabel);

        _log.Location = new Point(24, 303);
        _log.Size = new Size(852, 202);
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
            _connectToggle.Left = (ClientSize.Width - _connectToggle.Width) / 2;
            _actionProgress.Left = _connectToggle.Left;
            _actionProgressLabel.Left = _connectToggle.Left;
        };
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
        _failedInternetChecks = 0;
        _actionProgress.Visible = true;
        _actionProgressLabel.Visible = true;
        _actionProgress.Style = ProgressBarStyle.Blocks;
        _actionProgress.Value = 0;
        _actionProgress.Maximum = action == EngineAction.Start ? 6 : 4;
        _actionProgressLabel.Text = action == EngineAction.Start
            ? UiStrings.Get(_selectedLanguage, "ProgressStarting")
            : UiStrings.Get(_selectedLanguage, "ProgressStopping");
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
            var outcome = await _engine.RunAsync(action, message =>
            {
                AppendLog(message);
                UpdateOperationProgress(action, message);
            }, _actionCancellation.Token);
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
            else if (outcome.NativeVerified)
            {
                // A verified network connection and overall setup completion
                // are distinct. Optional KNULLI compatibility can fail after
                // native HTTPS has already succeeded.
                _internetHealthy = true;
                _lastHealthCheck = DateTimeOffset.UtcNow;
                AppendLog("Backend native HTTPS verification passed; Internet connected.");
                if (!outcome.Success)
                    AppendLog("Internet is online, but a later compatibility step failed.");
            }
            else
            {
                _internetHealthy = false;
                AppendLog(outcome.Success
                    ? "Backend returned success without native HTTPS proof; status remains disconnected."
                    : "USB Internet start failed; see detailed stage messages above.");
            }

            if (outcome.Success)
            {
                _actionProgress.Style = ProgressBarStyle.Blocks;
                _actionProgress.Value = _actionProgress.Maximum;
                _actionProgressLabel.Text = UiStrings.Get(_selectedLanguage, "ProgressComplete");
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
            _actionProgress.Visible = false;
            _actionProgressLabel.Visible = false;
            ApplyLanguage();
            if (!_closed && !_closingAfterStop)
            {
                _statusTimer.Start();
                _ = PollDeviceAsync();
            }
        }
    }

    private void UpdateOperationProgress(EngineAction action, string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => UpdateOperationProgress(action, message)));
            return;
        }
        if (_closed || IsDisposed || !_busy) return;

        if (action == EngineAction.Stop)
        {
            int stopStage = message switch
            {
                var s when s.Contains("Stage STOP preparation:", StringComparison.Ordinal) => 1,
                var s when s.Contains("Stage STOP device:", StringComparison.Ordinal) => 2,
                var s when s.Contains("Stage STOP SSH:", StringComparison.Ordinal) => 3,
                var s when s.Contains("Stage STOP ADB:", StringComparison.Ordinal) => 4,
                _ => 0
            };
            if (stopStage > 0)
            {
                _actionProgress.Value = Math.Max(stopStage, _actionProgress.Value);
                _actionProgressLabel.Text = string.Format(
                    UiStrings.Get(_selectedLanguage, "ProgressStopStage"), stopStage, 4);
            }
            return;
        }

        int stage = message switch
        {
            var s when s.Contains("Stage USB forwarding:", StringComparison.Ordinal) => 1,
            var s when s.Contains("Stage SSH/SOCKS:", StringComparison.Ordinal) => 2,
            var s when s.Contains("Stage device payload:", StringComparison.Ordinal) => 3,
            var s when s.Contains("Stage TUN/DNS:", StringComparison.Ordinal) => 4,
            var s when s.Contains("Stage native HTTPS:", StringComparison.Ordinal) => 5,
            var s when s.Contains("Stage compatibility:", StringComparison.Ordinal) => 6,
            _ => 0
        };
        if (stage <= 0) return;
        _actionProgress.Style = ProgressBarStyle.Blocks;
        _actionProgress.Value = Math.Max(stage, _actionProgress.Value);
        _actionProgressLabel.Text = string.Format(
            UiStrings.Get(_selectedLanguage, "ProgressStage"), stage, 6);
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
            if (ready)
                _missingDeviceChecksWhileIdle = 0;
            else
                _missingDeviceChecksWhileIdle++;
            // A single slow adb.exe response must not flicker a physically
            // connected device or hide the Internet status.
            bool visibleReady = ready || (_adbReady &&
                _missingDeviceChecksWhileIdle < 2);
            if (_adbReady != visibleReady)
            {
                _adbReady = visibleReady;
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
            else if (!_busy && (_mayHaveActiveSession || _internetHealthy))
                _ = CheckInternetHealthAsync();

            ApplyLanguage();
        }
        catch (Exception ex)
        {
            if (_closed || IsDisposed) return;
            _missingDeviceChecksWhileIdle++;
            if (_missingDeviceChecksWhileIdle >= 2 && _adbReady)
            {
                _adbReady = false;
                _internetHealthy = false;
                _deviceGeneration++;
            }
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
        if (_busy || _closed || !_adbReady ||
            (!_mayHaveActiveSession && !_internetHealthy) ||
            DateTimeOffset.UtcNow - _lastHealthCheck < TimeSpan.FromSeconds(20) ||
            Interlocked.Exchange(ref _healthPolling, 1) != 0)
            return;
        int generation = _deviceGeneration;
        _lastHealthCheck = DateTimeOffset.UtcNow;
        try
        {
            bool healthy = await UsbEngineRunner.NativeInternetHealthyAsync(CancellationToken.None);
            if (_closed || IsDisposed || _busy || !_adbReady ||
                generation != _deviceGeneration)
                return;
            if (healthy)
            {
                _failedInternetChecks = 0;
                if (!_internetHealthy)
                    AppendLog("Native Internet HTTPS health check passed.");
                _internetHealthy = true;
            }
            else
            {
                _failedInternetChecks++;
                // Do not tell the user Internet is gone because a single
                // public HTTPS endpoint or DNS lookup temporarily timed out.
                if (_failedInternetChecks >= 2 && _internetHealthy)
                {
                    _internetHealthy = false;
                    AppendLog("Two consecutive native HTTPS health checks failed.");
                }
            }
            ApplyLanguage();
        }
        catch (Exception ex)
        {
            if (!_closed && !IsDisposed)
            {
                _failedInternetChecks++;
                if (_failedInternetChecks >= 2) _internetHealthy = false;
                AppendLog("Native Internet probe error: " + ex.GetType().Name);
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
        if (_busy && _actionProgress.Style != ProgressBarStyle.Marquee &&
            _actionProgress.Value > 0)
            _actionProgressLabel.Text = string.Format(
                T(_actionProgress.Maximum == 4 ? "ProgressStopStage" : "ProgressStage"),
                _actionProgress.Value, _actionProgress.Maximum);
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
        if (!ShouldDisconnect) return;

        // Closing an Internet session must not leave an ADB/SSH daemon behind.
        // The user can cancel quitting, but not silently leave the tunnel active.
        var answer = MessageBox.Show(this,
            "Disconnect USB Internet and close the application?\n\n" +
            "Yes: Disconnect safely and exit.\n" +
            "No: Return to the application.",
            "USB Internet", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        e.Cancel = true;
        if (answer == DialogResult.Yes)
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
