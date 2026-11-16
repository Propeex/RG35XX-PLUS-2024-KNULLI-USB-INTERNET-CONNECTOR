using System;
using System.Drawing;
using System.Windows.Forms;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Startup gate: the main window is NEVER displayed until prerequisite
/// checks/installs finish. The progress bar is shown ONLY during the first
/// successful initialization, never inside MainForm or on later launches.
/// </summary>
internal sealed class BootstrapForm : Form
{
    private readonly Label _heading = new();
    private readonly Label _stage = new();
    private readonly ProgressBar _progress = new();
    private readonly Button _retry = new();
    private readonly TextBox _details = new();
    private readonly List<string> _events = new();
    private readonly bool _firstLaunch = !File.Exists(AppPaths.InitializedMarker);
    private bool _busy;

    internal BootstrapForm()
    {
        Text = "RG35XX+ (2024) Connector — Startup";
        ClientSize = new Size(560, _firstLaunch ? 265 : 145);
        MinimumSize = MaximumSize = Size;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);

        _heading.Text = _firstLaunch ? "First-time setup" : "Checking for updates...";
        _heading.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        _heading.Location = new Point(22, 15);
        _heading.Size = new Size(515, 35);
        Controls.Add(_heading);

        _stage.Text = "Initializing...";
        _stage.Location = new Point(22, 60);
        _stage.Size = new Size(515, 30);
        Controls.Add(_stage);

        // Never show download/update progress on subsequent launches.
        _progress.Visible = _firstLaunch;
        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 35;
        _progress.Location = new Point(22, 101);
        _progress.Size = new Size(515, 18);
        Controls.Add(_progress);

        _details.Visible = _firstLaunch;
        _details.Multiline = true;
        _details.ReadOnly = true;
        _details.ScrollBars = ScrollBars.Vertical;
        _details.Font = new Font("Consolas", 9F);
        _details.Location = new Point(22, 134);
        _details.Size = new Size(515, 73);
        Controls.Add(_details);

        _retry.Text = "Retry";
        _retry.Visible = false;
        _retry.Location = new Point(429, _firstLaunch ? 221 : 96);
        _retry.Size = new Size(108, 32);
        _retry.Click += async (_, _) => await StartAsync();
        Controls.Add(_retry);

        Shown += async (_, _) => await StartAsync();
    }

    private async Task StartAsync()
    {
        if (_busy) return;
        _busy = true;
        _retry.Visible = false;
        try
        {
            if (!File.Exists(AppPaths.SdkLicenseAcceptedMarker))
            {
                var consent = MessageBox.Show(this,
                    "This utility installs Google's Android SDK Platform-Tools. " +
                    "Please review the Android SDK License Agreement at " +
                    "https://developer.android.com/studio/terms before continuing. " +
                    "Do you agree to its terms?",
                    "Google Android SDK License",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (consent != DialogResult.Yes)
                    throw new InvalidOperationException("Android SDK license was not accepted.");
                Directory.CreateDirectory(AppPaths.BaseDirectory);
                File.WriteAllText(AppPaths.SdkLicenseAcceptedMarker, DateTimeOffset.UtcNow.ToString("O"));
            }

            var reporter = new Progress<StartupStep>(UpdateStage);
            var svc = new StartupService();
            await svc.PrepareAsync(reporter, Log, CancellationToken.None);

            // Mark setup complete only when platform-tools are present/usable.
            Directory.CreateDirectory(AppPaths.BaseDirectory);
            File.WriteAllText(AppPaths.InitializedMarker, DateTimeOffset.UtcNow.ToString("O"));
            Hide();
            using (var main = new MainForm(_events.ToArray()))
                main.ShowDialog();
            Close();
        }
        catch (Exception ex)
        {
            _stage.Text = "Startup failed — retry or close this window.";
            Log("Startup failed: " + ex.Message);
            _retry.Visible = true;
            _retry.Enabled = true;
            if (_firstLaunch) _progress.Style = ProgressBarStyle.Continuous;
            if (!_firstLaunch)
            {
                MessageBox.Show(this, ex.Message, "Startup failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _busy = false;
        }
    }

    private void UpdateStage(StartupStep step)
    {
        if (IsDisposed) return;
        _stage.Text = step.Message;
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
        _events.Add(message);
        if (_firstLaunch && !IsDisposed)
        {
            if (_details.TextLength > 0) _details.AppendText(Environment.NewLine);
            _details.AppendText(message);
        }
    }
}
