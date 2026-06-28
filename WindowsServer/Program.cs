using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Fleck;

class Program
{
    static IWebSocketConnection? activeSocket;

    static void Main()
    {
        var server = new WebSocketServer("ws://0.0.0.0:8181");
        server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                Console.WriteLine("Phone connected: " + socket.ConnectionInfo.ClientIpAddress);
                activeSocket = socket;
            };
            socket.OnClose = () => activeSocket = null;
            socket.OnMessage = message => HandleInput(message);
        });

        Console.WriteLine("Server running on port 8181");
        Console.WriteLine("Your PC's local IP (use this in the Android app):");
        foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                Console.WriteLine("  ws://" + ip + ":8181");
        }

        // Screen capture loop
        while (true)
        {
            if (activeSocket != null && activeSocket.IsAvailable)
            {
                try
                {
                    byte[] jpegBytes = CaptureScreenAsJpeg();
                    activeSocket.Send(jpegBytes);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Capture error: " + ex.Message);
                }
            }
            Thread.Sleep(66); // ~15 fps
        }
    }

    static byte[] CaptureScreenAsJpeg()
    {
        int width = Screen.PrimaryScreen!.Bounds.Width;
        int height = Screen.PrimaryScreen!.Bounds.Height;

        using var bitmap = new Bitmap(width, height);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(0, 0, 0, 0, new Size(width, height));

        using var ms = new MemoryStream();
        var encoders = ImageCodecInfo.GetImageEncoders();
        ImageCodecInfo jpegEncoder = Array.Find(encoders, c => c.FormatID == ImageFormat.Jpeg.Guid)!;
        var encParams = new EncoderParameters(1);
        encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 50L); // lower = smaller/faster
        bitmap.Save(ms, jpegEncoder, encParams);
        return ms.ToArray();
    }

    // Input handling: expects JSON like {"type":"tap","x":123,"y":456}
    static void HandleInput(string message)
    {
        try
        {
            var data = System.Text.Json.JsonDocument.Parse(message).RootElement;
            string type = data.GetProperty("type").GetString()!;
            int x = data.GetProperty("x").GetInt32();
            int y = data.GetProperty("y").GetInt32();

            SetCursorPos(x, y);

            if (type == "tap")
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
            }
            else if (type == "move")
            {
                // cursor already moved above, nothing else needed for a drag-move
            }
            else if (type == "key")
            {
                // expects {"type":"key","keyCode":13} for Enter, etc.
                if (data.TryGetProperty("keyCode", out var kc))
                {
                    byte vk = (byte)kc.GetInt32();
                    keybd_event(vk, 0, 0, 0);
                    keybd_event(vk, 0, KEYEVENTF_KEYUP, 0);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Bad input message: " + ex.Message);
        }
    }

    [DllImport("user32.dll")]
    static extern void SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    static extern void mouse_event(uint flags, int dx, int dy, uint data, int extraInfo);

    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

    const uint MOUSEEVENTF_LEFTDOWN = 0x02;
    const uint MOUSEEVENTF_LEFTUP = 0x04;
    const uint KEYEVENTF_KEYUP = 0x02;
}
