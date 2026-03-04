using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace LocalPhotoAI.Host;

/// <summary>
/// Opens the default browser to the app's URL after the server starts listening.
/// Automatically skipped during integration testing (no server address features available).
/// </summary>
public class BrowserLauncherService : BackgroundService
{
    private readonly IServer _server;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<BrowserLauncherService> _logger;

    public BrowserLauncherService(IServer server, IHostEnvironment environment, ILogger<BrowserLauncherService> logger)
    {
        _server = server;
        _environment = environment;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Give the server a moment to bind its addresses
        await Task.Delay(500, stoppingToken);

        var addressFeature = _server.Features.Get<IServerAddressesFeature>();
        var address = addressFeature?.Addresses.FirstOrDefault();

        if (address is null)
        {
            _logger.LogDebug("No server address available; skipping browser launch.");
            return;
        }

        var lanIp = NetworkHelper.GetLanIpAddress();
        var port = new Uri(address).Port;
        var lanUrl = lanIp is not null ? $"http://{lanIp}:{port}" : $"http://localhost:{port}";

        Console.WriteLine();
        Console.WriteLine($"  LocalPhotoAI running at {lanUrl}");
        Console.WriteLine($"  Press Ctrl+C to stop.");
        Console.WriteLine();

        try
        {
            Process.Start(new ProcessStartInfo(lanUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not open default browser.");
        }
    }
}
