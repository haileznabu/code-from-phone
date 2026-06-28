# Lightweight LAN Remote Desktop (Windows ↔ Android)

A minimal screen-share + remote-control setup over your local network.
PC streams JPEG screenshots over a WebSocket; phone displays them and sends
tap/move events back, which the PC replays as real mouse input.

## Folder structure
```
RemoteDesktop/
├── WindowsServer/        <- C# .NET console app (run on the PC)
│   ├── WindowsServer.csproj
│   └── Program.cs
└── AndroidClient/        <- Kotlin Android app (run on the phone)
    └── app/
        ├── build.gradle
        └── src/main/
            ├── AndroidManifest.xml
            ├── java/com/example/remotedesktop/MainActivity.kt
            └── res/layout/activity_main.xml
```

## 1. Run the Windows server

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download)

```bash
cd WindowsServer
dotnet restore
dotnet run
```

On first run it will print your PC's LAN IP, e.g.:
```
Server running on port 8181
Your PC's local IP (use this in the Android app):
  ws://192.168.1.50:8181
```
Make sure Windows Firewall allows inbound connections on port 8181 (it
will usually prompt you the first time you run it — allow it for
Private networks).

## 2. Set up the Android app

This is just the app module — you'll need to open/create it inside a
full Android Studio project (File → New → Empty Activity project named
`RemoteDesktop`, then drop these files into the matching paths,
overwriting the generated ones).

Before building, edit `MainActivity.kt`:
```kotlin
private val pcWidth = 1920          // <- your PC's actual screen width
private val pcHeight = 1080         // <- your PC's actual screen height
private val serverUrl = "ws://192.168.1.50:8181"   // <- IP printed by the server
```

Then connect your phone to the **same Wi-Fi network** as the PC, build,
and run the app (USB debugging or `adb install`).

## How it works
- PC captures the screen ~15x/second, encodes as JPEG, sends bytes over
  the WebSocket.
- Phone decodes each JPEG and shows it in an `ImageView`.
- Tapping the image sends `{"type":"tap","x":..,"y":..}` (coordinates
  scaled from phone screen space to PC screen space).
- PC moves the cursor there and performs a left click.
- Dragging sends `{"type":"move",...}` to update cursor position without
  clicking.

## Known limitations (this is a starting skeleton, not production)
- No encryption — anyone on your LAN could connect to port 8181. Fine for
  home use, not for shared/public networks.
- No authentication / pairing — first device to connect gets control.
- Keyboard input is stubbed out (long-press brings up the soft keyboard,
  but keystrokes aren't forwarded yet — see "Next steps").
- Single client only; a second connection will replace the first.
- JPEG quality is hardcoded to 50 — tune in `CaptureScreenAsJpeg()` in
  `Program.cs` for your bandwidth/quality preference.
- Resolution is hardcoded on the Android side — would be better to send
  it from the PC automatically.

## Suggested next steps
1. **Forward keystrokes**: add a `TextWatcher` on `hiddenInput` and send
   each character as `{"type":"key","keyCode":...}`; handle it in
   `HandleInput()` on the PC with `keybd_event` (already scaffolded).
2. **Auto resolution handshake**: have the PC send its resolution as the
   very first WebSocket message; have the phone parse and store it
   instead of hardcoding `pcWidth`/`pcHeight`.
3. **Simple auth**: require a PIN/token on connect before accepting input
   commands.
4. **Better video**: swap per-frame JPEG for H.264 (Media Foundation on
   the PC side, `MediaCodec` decode on Android) once the basic version
   works — much lower bandwidth and latency.
5. **Auto-discovery**: UDP broadcast from the PC so the phone can find it
   without typing an IP.
