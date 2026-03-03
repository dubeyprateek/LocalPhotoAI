using System.Net;
using System.Net.Sockets;
using QRCoder;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri("http://localhost:5101/health"), name: "upload-service", tags: ["services"])
    .AddUrlGroup(new Uri("http://localhost:5102/health"), name: "job-service", tags: ["services"])
    .AddUrlGroup(new Uri("http://localhost:5103/health"), name: "gallery-service", tags: ["services"]);

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/gateway/status", () => Results.Ok(new { service = "LocalPhotoAI Gateway", status = "running" }));
app.MapHealthChecks("/health");

app.MapGet("/api/gateway/connection-info", (HttpRequest request) =>
{
    var lanIp = GetLanIpAddress();
    var port = request.Host.Port ?? 80;
    var gatewayUrl = lanIp is not null ? $"http://{lanIp}:{port}" : null;

    return Results.Ok(new
    {
        lanIp,
        port,
        gatewayUrl,
        hostname = Dns.GetHostName(),
        localUrl = $"http://{Dns.GetHostName().ToLowerInvariant()}.local:{port}",
        note = "The .local URL requires mDNS support on your network. Use the IP-based URL as a reliable fallback."
    });
});

app.MapGet("/api/gateway/qr", (HttpRequest request) =>
{
    var lanIp = GetLanIpAddress();
    var port = request.Host.Port ?? 80;
    var url = lanIp is not null ? $"http://{lanIp}:{port}" : $"http://localhost:{port}";

    using var qrGenerator = new QRCodeGenerator();
    using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
    using var svgQrCode = new SvgQRCode(qrCodeData);
    var svgContent = svgQrCode.GetGraphic(10);

    return Results.Content(svgContent, "image/svg+xml");
});

app.MapReverseProxy();

app.Run();

static string? GetLanIpAddress()
{
    try
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        return host.AddressList
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
            ?.ToString();
    }
    catch
    {
        return null;
    }
}
