using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace RG35XXUsbInternetConnector;

/// <summary>
/// Offline ADB wire-protocol regression tests. Loopback listeners emulate a
/// running ADB server: no real adb.exe is launched and no device is required.
/// </summary>
internal static class AdbProtocolSelfTests
{
    internal static async Task RunAsync()
    {
        await TestSnapshotAsync();
        await TestTrackingAsync();
    }

    private static async Task TestSnapshotAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task server = Task.Run(async () =>
            {
                using TcpClient peer = await listener.AcceptTcpClientAsync();
                using NetworkStream stream = peer.GetStream();
                string command = await ReceiveCommandAsync(stream);
                if (command != "host:devices-l")
                    throw new InvalidDataException("Unexpected passive ADB query.");
                await stream.WriteAsync(Encoding.ASCII.GetBytes("OKAY"));
                await WriteListingAsync(stream, "testserial\tdevice product:KNULLI\n");
            });

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            AdbServerProtocol.Snapshot snapshot =
                await AdbServerProtocol.QueryAsync(port.ToString(CultureInfo.InvariantCulture))
                    .WaitAsync(timeout.Token);
            if (!snapshot.ServerPresent || !snapshot.Ready || snapshot.State != "device")
                throw new InvalidOperationException("Passive ADB snapshot did not detect ready device.");
            await server.WaitAsync(timeout.Token);
        }
        finally { listener.Stop(); }
    }

    private static async Task TestTrackingAsync()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task server = Task.Run(async () =>
            {
                using TcpClient peer = await listener.AcceptTcpClientAsync();
                using NetworkStream stream = peer.GetStream();
                string command = await ReceiveCommandAsync(stream);
                if (command != "host:track-devices")
                    throw new InvalidDataException("Unexpected passive ADB tracking request.");
                await stream.WriteAsync(Encoding.ASCII.GetBytes("OKAY"));
                await WriteListingAsync(stream, "testserial\tdevice\n");
                await WriteListingAsync(stream, "testserial\toffline\n");
            });
            int notifications = 0;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            try
            {
                await AdbServerProtocol.TrackAsync(
                    port.ToString(CultureInfo.InvariantCulture),
                    () => Interlocked.Increment(ref notifications),
                    timeout.Token);
            }
            catch (EndOfStreamException)
            {
                // Expected: fake ADB server disconnects after two messages.
            }
            catch (IOException)
            {
                // The test server has intentionally closed its device stream.
            }
            if (notifications != 2)
                throw new InvalidOperationException("ADB device tracker missed device state change.");
            await server.WaitAsync(timeout.Token);
        }
        finally { listener.Stop(); }
    }

    private static async Task<string> ReceiveCommandAsync(NetworkStream stream)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header);
        if (!int.TryParse(Encoding.ASCII.GetString(header), NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out int size) || size < 1 || size > 128)
            throw new InvalidDataException("Invalid ADB host request framing.");
        byte[] bytes = new byte[size];
        await stream.ReadExactlyAsync(bytes);
        return Encoding.ASCII.GetString(bytes);
    }

    private static async Task WriteListingAsync(NetworkStream stream, string listing)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(listing);
        string size = bytes.Length.ToString("X4", CultureInfo.InvariantCulture);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(size));
        await stream.WriteAsync(bytes);
        await stream.FlushAsync();
    }
}
