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
        if (args.Length > 0 && args[0] == "--apply-update")
        {
            // This is launched from an isolated temporary COPY of the old EXE
            // only after the new GitHub EXE has passed SHA-256 verification.
            UpdateWorker.Apply(args);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.Run(new BootstrapForm());
    }
}
