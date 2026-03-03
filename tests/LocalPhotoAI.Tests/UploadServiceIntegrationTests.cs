using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LocalPhotoAI.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LocalPhotoAI.Tests;

public class UploadServiceIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testStoragePath;

    public UploadServiceIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), "upload_test_" + Guid.NewGuid().ToString("N"));

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("StoragePath", _testStoragePath);
        });

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        if (Directory.Exists(_testStoragePath))
            Directory.Delete(_testStoragePath, recursive: true);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Upload_SinglePhoto_ReturnsPhotoId()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "files", "test-photo.jpg");

        var response = await _client.PostAsync("/api/uploads", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UploadResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.Single(result.Files);
        Assert.False(string.IsNullOrEmpty(result.Files[0].PhotoId));
        Assert.False(string.IsNullOrEmpty(result.Files[0].JobId));
    }

    [Fact]
    public async Task Upload_MultiplePhotos_ReturnsMultiplePhotoIds()
    {
        using var content = new MultipartFormDataContent();

        for (int i = 0; i < 3; i++)
        {
            var fileContent = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content.Add(fileContent, "files", $"photo{i}.png");
        }

        var response = await _client.PostAsync("/api/uploads", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UploadResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.Equal(3, result.Files.Count);

        var photoIds = result.Files.Select(f => f.PhotoId).ToList();
        Assert.Equal(3, photoIds.Distinct().Count()); // All unique
    }

    [Fact]
    public async Task Upload_DisallowedExtension_Returns400()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x00, 0x01]);
        content.Add(fileContent, "files", "malware.exe");

        var response = await _client.PostAsync("/api/uploads", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_NoFiles_Returns400()
    {
        // Send a valid JSON body instead of multipart to trigger "Expected multipart/form-data"
        var response = await _client.PostAsync("/api/uploads",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_FileLandsInCorrectPath()
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "files", "test.jpg");

        var response = await _client.PostAsync("/api/uploads", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UploadResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(result);

        var photoId = result.Files[0].PhotoId;
        var expectedDir = Path.Combine(_testStoragePath, "originals", photoId);
        var expectedFile = Path.Combine(expectedDir, "original.jpg");

        Assert.True(Directory.Exists(expectedDir), $"Directory should exist: {expectedDir}");
        Assert.True(File.Exists(expectedFile), $"File should exist: {expectedFile}");

        var savedBytes = await File.ReadAllBytesAsync(expectedFile);
        Assert.Equal(fileBytes, savedBytes);
    }
}
