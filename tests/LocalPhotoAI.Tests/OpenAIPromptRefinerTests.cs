using System.Net;
using System.Text;
using LocalPhotoAI.Shared.Pipelines;

namespace LocalPhotoAI.Tests;

public class OpenAIPromptRefinerTests
{
    [Fact]
    public async Task RefineAsync_WithNullApiKey_FallsBackToStub()
    {
        var refiner = new OpenAIPromptRefiner(new HttpClient(), apiKey: null);
        var result = await refiner.RefineAsync("make it vintage");

        Assert.Contains("vintage", result.RefinedPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.GeneratedTitle));
    }

    [Fact]
    public async Task RefineAsync_WithEmptyApiKey_FallsBackToStub()
    {
        var refiner = new OpenAIPromptRefiner(new HttpClient(), apiKey: "  ");
        var result = await refiner.RefineAsync("apply blur");

        Assert.Contains("blur", result.RefinedPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.GeneratedTitle));
    }

    [Fact]
    public async Task RefineAsync_WhenApiFails_FallsBackToStub()
    {
        // Use a handler that always returns 500
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        var client = new HttpClient(handler);

        var refiner = new OpenAIPromptRefiner(client, apiKey: "sk-test-key");
        var result = await refiner.RefineAsync("grayscale the image");

        // Should fall back gracefully, not throw
        Assert.Contains("grayscale", result.RefinedPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(result.GeneratedTitle));
    }

    [Fact]
    public async Task RefineAsync_WithValidApiResponse_ParsesResult()
    {
        var responseBody = """
        {
          "choices": [
            {
              "message": {
                "content": "{\"refinedPrompt\": \"Apply grayscale filter to the image\", \"title\": \"grayscale-filter\"}"
              }
            }
          ]
        }
        """;

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var client = new HttpClient(handler);

        var refiner = new OpenAIPromptRefiner(client, apiKey: "sk-test-key");
        var result = await refiner.RefineAsync("make it black and white");

        Assert.Equal("Apply grayscale filter to the image", result.RefinedPrompt);
        Assert.Equal("grayscale-filter", result.GeneratedTitle);
    }

    [Fact]
    public async Task RefineAsync_WithCodeFencedResponse_ParsesResult()
    {
        var responseBody = """
        {
          "choices": [
            {
              "message": {
                "content": "```json\n{\"refinedPrompt\": \"Apply sepia tone\", \"title\": \"sepia-tone\"}\n```"
              }
            }
          ]
        }
        """;

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var client = new HttpClient(handler);

        var refiner = new OpenAIPromptRefiner(client, apiKey: "sk-test-key");
        var result = await refiner.RefineAsync("make it look old");

        Assert.Equal("Apply sepia tone", result.RefinedPrompt);
        Assert.Equal("sepia-tone", result.GeneratedTitle);
    }

    [Fact]
    public async Task RefineAsync_WithEmptyChoices_FallsBackToStub()
    {
        var responseBody = """{"choices": []}""";
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseBody);
        var client = new HttpClient(handler);

        var refiner = new OpenAIPromptRefiner(client, apiKey: "sk-test-key");
        var result = await refiner.RefineAsync("sharpen the photo");

        Assert.Contains("sharpen", result.RefinedPrompt, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A simple fake handler for unit testing HTTP calls without hitting the real API.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
