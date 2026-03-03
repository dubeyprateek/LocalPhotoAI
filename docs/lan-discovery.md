# LAN Discovery & Network Access

## Quick Start
1. Run `LocalPhotoAI.Host.exe` (or `dotnet run --project src/LocalPhotoAI.Host`).
2. The app opens your default browser automatically.
3. From your phone, scan the QR code shown at the bottom of the page.

## Connecting to LocalPhotoAI

### Option 1: Automatic Browser Launch
When you start the application, it automatically opens `http://localhost:5100`
in your default browser. The web UI includes a QR code and LAN URL for mobile
devices.

### Option 2: IP Address (Reliable Fallback)
1. Start the application.
2. Visit `http://<your-machine-ip>:5100` in your browser.
3. Use the `/api/connection-info` endpoint to find your LAN IP automatically.

### Option 3: QR Code
1. Open the web UI on the host machine.
2. The bottom of the page displays a QR code.
3. Scan it with your mobile device's camera to open the upload page.
4. Alternatively, `GET /api/qr` returns an SVG QR code of the app URL.

### Option 4: mDNS / `.local` Hostname
If mDNS is enabled on your network, you may be able to access the app at:
```
http://<hostname>.local:5100
```

**Windows:** Windows 10+ includes mDNS support (via DNS-SD). The hostname is
typically your computer name. For example, if your PC is named `MYPC`, try
`http://mypc.local:5100`.

**Security Note:** mDNS operates on the local network using multicast DNS. While
convenient, be aware that it broadcasts your hostname to all devices on the LAN.
In enterprise environments, review your organization's mDNS policy. See
[Microsoft's guidance on mDNS in the enterprise](https://techcommunity.microsoft.com/blog/networkingblog/mdns-in-the-enterprise/3275777).

**If `.local` doesn't work:**
- Ensure mDNS/Bonjour is enabled on both the host and client devices.
- Try accessing via IP address as a fallback (Option 2).
- On some networks, mDNS may be blocked by firewall rules or network configuration.

## Configuration

Edit `appsettings.json` next to the executable:

| Setting          | Default      | Description                              |
|------------------|-------------|------------------------------------------|
| `StoragePath`    | `./storage`  | Directory for photos and metadata        |
| `MaxUploadSizeMB`| `50`        | Maximum upload file size in MB           |
| `Urls`           | `http://0.0.0.0:5100` | Listening address and port     |

## Deployment

### From Source
```
dotnet run --project src/LocalPhotoAI.Host
```

### Publish as Single Executable
```powershell
./publish.ps1                     # defaults to win-x64
./publish.ps1 -Runtime linux-x64  # for Linux
```

This produces a self-contained single-file executable in the `publish/` folder.
No .NET SDK or runtime needed on the target machine.
