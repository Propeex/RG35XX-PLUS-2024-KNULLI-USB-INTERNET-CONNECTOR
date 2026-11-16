using System;
using System.Windows.Forms;

namespace RG35XXUsbInternetConnector;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--self-test")
        {
            Environment.ExitCode = SelfTests.Run();
            return;
        }
        if (args.Length == 2 && args[0] == "--verify-package")
        {
            try
            {
                string stage = PortableUpdatePackage.StageAndValidate(args[1]);
                Directory.Delete(stage, recursive: true);
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppPaths.PortableRoot,
                        "package-verification-error.txt"), ex.ToString());
                }
                catch (IOException) { }
                Environment.ExitCode = 2;
            }
            return;
        }
        if (args.Length > 0 && args[0] == "--apply-update")
        {
            // This is launched from an isolated temporary COPY of the old EXE
            // only after the new GitHub EXE has passed SHA-256 verification.
            UpdateWorker.Apply(args);
            return;
        }
        // Only one instance may own the ADB port, SSH PID file or the update
        // staging directory. A second launch must never interfere with them.
        using var singleInstance = new System.Threading.Mutex(
            initiallyOwned: true,
            name: @"Local\RG35XX_KNULLI_USB_Internet_Connector",
            createdNew: out bool createdNew);
        ApplicationConfiguration.Initialize();
        if (!createdNew)
        {
            MessageBox.Show("USB Internet Connector is already running.",
                "RG35XX USB Internet Connector",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Application.Run(new BootstrapForm());
    }
}
