using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LocalPhotoAI.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LocalPhotoAI.Tests;

public class HostIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly string _testStoragePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public HostIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _testStoragePath = Path.Combine(Path.GetTempPath(), "host_test_" + Guid.NewGuid().ToString("N"));

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("StoragePath", _testStoragePath);
        });

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        try
        {
            if (Directory.Exists(_testStoragePath))
                Directory.Delete(_testStoragePath, recursive: true);
        }
        catch (IOException)
        {
            // Background worker may still hold file locks; best-effort cleanup.
        }
    }

    // -- Job endpoints --------------------------------------------------------

    [Fact]
    public async Task GetJobs_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/jobs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetJob_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/jobs/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetJob_AfterUpload_ReturnsJob()
    {
        var uploadResult = await UploadSinglePhotoAsync();
        var jobId = uploadResult.Files[0].JobId;

        var response = await _client.GetAsync($"/api/jobs/{jobId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var job = await response.Content.ReadFromJsonAsync<JobRecord>(JsonOptions);
        Assert.NotNull(job);
        Assert.Equal(jobId, job.JobId);
        Assert.Equal(uploadResult.Files[0].PhotoId, job.PhotoId);
    }

    // -- Photo gallery endpoints ----------------------------------------------

    [Fact]
    public async Task GetPhotos_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/photos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetPhoto_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/photos/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetPhoto_AfterUpload_ReturnsMetadata()
    {
        var uploadResult = await UploadSinglePhotoAsync();
        var photoId = uploadResult.Files[0].PhotoId;

        var response = await _client.GetAsync($"/api/photos/{photoId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var photo = await response.Content.ReadFromJsonAsync<PhotoMetadata>(JsonOptions);
        Assert.NotNull(photo);
        Assert.Equal(photoId, photo.PhotoId);
        Assert.Equal(".jpg", photo.Extension);
    }

    // -- Download endpoint ----------------------------------------------------

    [Fact]
    public async Task Download_NonExistentPhoto_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/photos/nonexistent/download");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Download_AfterUpload_ReturnsOriginalFile()
    {
        var fileBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var uploadResult = await UploadSinglePhotoAsync(fileBytes, "download-test.jpg");
        var photoId = uploadResult.Files[0].PhotoId;

        var response = await _client.GetAsync($"/api/photos/{photoId}/download");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var downloadedBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(fileBytes, downloadedBytes);

        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
    }

    // -- Connection info endpoint ---------------------------------------------

    [Fact]
    public async Task ConnectionInfo_ReturnsExpectedFields()
    {
        var response = await _client.GetAsync("/api/connection-info");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("hostname", out _));
        Assert.True(root.TryGetProperty("port", out _));
        Assert.True(root.TryGetProperty("localUrl", out _));
    }

    // -- QR endpoint ----------------------------------------------------------

    [Fact]
    public async Task QrEndpoint_ReturnsSvg()
    {
        var response = await _client.GetAsync("/api/qr");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("image/svg+xml", contentType);

        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("<svg", svg);
    }

    // -- Prompt refinement endpoint -------------------------------------------

    [Fact]
    public async Task PromptRefine_ValidPrompt_ReturnsRefinedResult()
    {
        var body = new { Prompt = "remove the blue sky" };
        var response = await _client.PostAsJsonAsync("/api/prompt/refine", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("refinedPrompt", out var refined));
        Assert.Contains("remove the blue sky", refined.GetString());
        Assert.True(root.TryGetProperty("title", out var title));
        Assert.False(string.IsNullOrWhiteSpace(title.GetString()));
    }

    [Fact]
    public async Task PromptRefine_EmptyPrompt_ReturnsBadRequest()
    {
        var body = new { Prompt = "" };
        var response = await _client.PostAsJsonAsync("/api/prompt/refine", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PromptRefine_NullBody_ReturnsBadRequest()
    {
        var response = await _client.PostAsync("/api/prompt/refine",
            new StringContent("null", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Session endpoints ----------------------------------------------------

    [Fact]
    public async Task CreateSession_ValidPrompt_ReturnsSession()
    {
        var session = await CreateSessionAsync("make it sepia");

        Assert.False(string.IsNullOrEmpty(session.SessionId));
        Assert.Equal("make it sepia", session.UserPrompt);
        Assert.False(string.IsNullOrWhiteSpace(session.RefinedPrompt));
        Assert.False(string.IsNullOrWhiteSpace(session.Title));
        Assert.Equal(SessionStatus.Draft, session.Status);
    }

    [Fact]
    public async Task CreateSession_EmptyPrompt_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/sessions", new { Prompt = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetSessions_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/sessions");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSession_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/sessions/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSession_AfterCreate_ReturnsSession()
    {
        var created = await CreateSessionAsync("sharpen edges");

        var response = await _client.GetAsync($"/api/sessions/{created.SessionId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<SessionRecord>(JsonOptions);
        Assert.NotNull(session);
        Assert.Equal(created.SessionId, session.SessionId);
        Assert.Equal("sharpen edges", session.UserPrompt);
    }

    [Fact]
    public async Task SessionRefine_UpdatesPrompt()
    {
        var session = await CreateSessionAsync("original prompt");

        var body = new { Prompt = "updated prompt" };
        var response = await _client.PostAsJsonAsync($"/api/sessions/{session.SessionId}/refine", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Contains("updated prompt", root.GetProperty("refinedPrompt").GetString());
    }

    [Fact]
    public async Task SessionRefine_NonExistentSession_ReturnsNotFound()
    {
        var body = new { Prompt = "anything" };
        var response = await _client.PostAsJsonAsync("/api/sessions/nonexistent/refine", body);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SessionRefine_EmptyPrompt_ReturnsBadRequest()
    {
        var session = await CreateSessionAsync("test");
        var body = new { Prompt = "" };
        var response = await _client.PostAsJsonAsync($"/api/sessions/{session.SessionId}/refine", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // -- Session upload -------------------------------------------------------

    [Fact]
    public async Task SessionUpload_ValidFile_ReturnsUploadResponse()
    {
        var session = await CreateSessionAsync("enhance photo");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "files", "session-test.jpg");

        var response = await _client.PostAsync($"/api/sessions/{session.SessionId}/upload", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Single(result.Files);
        Assert.False(string.IsNullOrEmpty(result.Files[0].PhotoId));
        Assert.False(string.IsNullOrEmpty(result.Files[0].JobId));
    }

    [Fact]
    public async Task SessionUpload_NonExistentSession_ReturnsNotFound()
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]);
        content.Add(fileContent, "files", "test.jpg");

        var response = await _client.PostAsync("/api/sessions/nonexistent/upload", content);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SessionUpload_DisallowedExtension_ReturnsBadRequest()
    {
        var session = await CreateSessionAsync("test");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x00, 0x01]);
        content.Add(fileContent, "files", "malware.exe");

        var response = await _client.PostAsync($"/api/sessions/{session.SessionId}/upload", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SessionUpload_NoFiles_ReturnsBadRequest()
    {
        var session = await CreateSessionAsync("test");

        var response = await _client.PostAsync($"/api/sessions/{session.SessionId}/upload",
            new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SessionUpload_SavesFileToInputFolder()
    {
        var session = await CreateSessionAsync("test save");

        var fileBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "files", "input-test.jpg");

        var response = await _client.PostAsync($"/api/sessions/{session.SessionId}/upload", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>(JsonOptions);
        Assert.NotNull(result);

        // Verify the file was saved to the session's InputImages folder
        var photoId = result.Files[0].PhotoId;
        var expectedFile = Path.Combine(session.InputFolder, $"{photoId}.jpg");
        Assert.True(File.Exists(expectedFile), $"File should exist at {expectedFile}");

        var savedBytes = await File.ReadAllBytesAsync(expectedFile);
        Assert.Equal(fileBytes, savedBytes);
    }

    // -- Session results ------------------------------------------------------

    [Fact]
    public async Task SessionResults_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/sessions/nonexistent/results");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SessionResults_AfterUpload_ReturnsPhotoInfo()
    {
        var session = await CreateSessionAsync("test results");

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "files", "result-test.jpg");
        await _client.PostAsync($"/api/sessions/{session.SessionId}/upload", content);

        // Allow a moment for the worker to process
        await Task.Delay(500);

        var response = await _client.GetAsync($"/api/sessions/{session.SessionId}/results");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(session.SessionId, root.GetProperty("sessionId").GetString());
        Assert.True(root.TryGetProperty("photos", out var photos));
        Assert.True(photos.GetArrayLength() > 0);
    }

    // -- Session QR -----------------------------------------------------------

    [Fact]
    public async Task SessionQr_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/sessions/nonexistent/qr");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SessionQr_ValidSession_ReturnsSvg()
    {
        var session = await CreateSessionAsync("qr test");

        var response = await _client.GetAsync($"/api/sessions/{session.SessionId}/qr");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("image/svg+xml", contentType);

        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("<svg", svg);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<UploadResponse> UploadSinglePhotoAsync(byte[]? fileBytes = null, string fileName = "test.jpg")
    {
        fileBytes ??= [0xFF, 0xD8, 0xFF, 0xE0];
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "files", fileName);

        var response = await _client.PostAsync("/api/uploads", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>(JsonOptions);
        return result!;
    }

    private async Task<SessionRecord> CreateSessionAsync(string prompt)
    {
        var response = await _client.PostAsJsonAsync("/api/sessions", new { Prompt = prompt });
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<SessionRecord>(JsonOptions);
        return session!;
    }
}
