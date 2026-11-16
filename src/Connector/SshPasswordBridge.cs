using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Invoked by Windows OpenSSH through SSH_ASKPASS only when the KNULLI server
/// requests a password. The default firmware password is intentionally "linux".
/// Never put the password on an ssh.exe command line, in a diagnostics file,
/// or in the displayed Activity Log.
/// </summary>
internal static class SshPasswordBridge
{
    internal const string ModeVariable = "RG35XX_USB_SSH_ASKPASS";
    private const string KnulliDefaultPassword = "linux";
    private const int StdOutputHandle = -11;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteFile(
        IntPtr file, byte[] data, int length, out int bytesWritten, IntPtr overlapped);

    internal static bool IsPasswordPrompt(string[] args)
    {
        // SSH may ask for a host-key decision or an identity passphrase.
        // Those prompts must never receive the firmware root password.
        return args.Length == 1 &&
            args[0].Contains("password", StringComparison.OrdinalIgnoreCase) &&
            !args[0].Contains("passphrase", StringComparison.OrdinalIgnoreCase);
    }

    internal static int Respond(string[] args)
    {
        if (!IsPasswordPrompt(args)) return 2;

        IntPtr stdout = GetStdHandle(StdOutputHandle);
        if (stdout == IntPtr.Zero || stdout == new IntPtr(-1)) return 3;

        byte[] passwordLine = Encoding.ASCII.GetBytes(KnulliDefaultPassword + "\n");
        try
        {
            return WriteFile(stdout, passwordLine, passwordLine.Length,
                out int written, IntPtr.Zero) && written == passwordLine.Length
                ? 0 : 4;
        }
        finally
        {
            Array.Clear(passwordLine, 0, passwordLine.Length);
        }
    }
}
