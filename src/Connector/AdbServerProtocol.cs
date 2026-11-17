using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// ADB's documented host smart-socket protocol. Unlike invoking
/// "adb -P ... devices", opening this socket NEVER starts a second server.
/// No device serials are ever written to the GUI or private diagnostics.
/// </summary>
internal static class AdbServerProtocol
{
    internal sealed record Snapshot(bool ServerPresent, string State, bool Ready);

    private const int MaxPayload = 65535;

    internal static async Task<Snapshot> QueryAsync(string port)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        bool connected = false;
        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(IPAddress.Loopback, int.Parse(port, CultureInfo.InvariantCulture),
                timeout.Token);
            connected = true;
            using NetworkStream stream = client.GetStream();
            await SendCommandAsync(stream, "host:devices-l", timeout.Token);
            string response = await ReadStatusAsync(stream, timeout.Token);
            if (response != "OKAY")
                return new Snapshot(true, "ADB server rejected device query", false);

            string listing = await ReadPayloadAsync(stream, timeout.Token);
            string state = AdbProbe.ParseState(listing);
            return new Snapshot(true, state, state == "device");
        }
        catch (OperationCanceledException)
        {
            return new Snapshot(connected, connected ? "ADB server timed out" : "not running", false);
        }
        catch (SocketException)
        {
            return new Snapshot(connected, connected ? "ADB transport interrupted" : "not running", false);
        }
        catch (IOException)
        {
            return new Snapshot(connected, "ADB protocol response incomplete", false);
        }
        catch (ObjectDisposedException)
        {
            return new Snapshot(connected, "ADB server connection closed", false);
        }
    }

    internal static async Task TrackAsync(string port, Action onStateChanged,
        CancellationToken cancellation)
    {
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(IPAddress.Loopback, int.Parse(port, CultureInfo.InvariantCulture),
            cancellation);
        using NetworkStream stream = client.GetStream();
        await SendCommandAsync(stream, "host:track-devices", cancellation);
        if (await ReadStatusAsync(stream, cancellation) != "OKAY")
            throw new IOException("ADB server rejected host:track-devices.");

        string? lastState = null;
        while (!cancellation.IsCancellationRequested)
        {
            string listing = await ReadPayloadAsync(stream, cancellation);
            // Notify for meaningful changes. The UI queries the selected
            // server directly rather than trusting stale event payloads.
            string state = AdbProbe.ParseState(listing);
            if (state != lastState)
            {
                lastState = state;
                onStateChanged();
            }
        }
    }

    private static async Task SendCommandAsync(NetworkStream stream, string command,
        CancellationToken cancellation)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(command);
        if (bytes.Length > 0xffff) throw new ArgumentOutOfRangeException(nameof(command));
        string hex = bytes.Length.ToString("X4", CultureInfo.InvariantCulture);
        byte[] framed = Encoding.ASCII.GetBytes(hex + command);
        await stream.WriteAsync(framed, cancellation);
    }

    private static async Task<string> ReadStatusAsync(NetworkStream stream,
        CancellationToken cancellation)
    {
        byte[] marker = new byte[4];
        await stream.ReadExactlyAsync(marker, cancellation);
        return Encoding.ASCII.GetString(marker);
    }

    private static async Task<string> ReadPayloadAsync(NetworkStream stream,
        CancellationToken cancellation)
    {
        byte[] lengthBytes = new byte[4];
        await stream.ReadExactlyAsync(lengthBytes, cancellation);
        if (!int.TryParse(Encoding.ASCII.GetString(lengthBytes), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out int length) || length < 0 || length > MaxPayload)
            throw new InvalidDataException("Invalid ADB smart-socket payload length.");

        byte[] body = new byte[length];
        await stream.ReadExactlyAsync(body, cancellation);
        return Encoding.UTF8.GetString(body);
    }

    // Offline tests verify exactly which ADB host services we send. These
    // requests only query an already-running server and cannot spawn it.
    internal static string DeviceQueryService => "host:devices-l";
    internal static string DeviceTrackService => "host:track-devices";
}
