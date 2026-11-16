using System;
using System.Windows.Forms;

namespace RG35XXUsbInternetConnector;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Every launch finishes dependency checks BEFORE the main window exists.
        // Only a first-time install shows a determinate download progress bar.
        Application.Run(new BootstrapForm());
    }
}
