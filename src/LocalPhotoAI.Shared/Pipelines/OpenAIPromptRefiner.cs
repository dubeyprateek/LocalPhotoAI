using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LocalPhotoAI.Shared.Pipelines;

/// <summary>
/// Prompt refiner that uses the OpenAI Chat Completions API to turn a
/// natural-language user prompt into a structured image-editing instruction
/// and a short session title.
///
/// Falls back to <see cref="StubPromptRefiner"/> behavior when the API key
/// is not configured or the API call fails.
/// </summary>
public partial class OpenAIPromptRefiner : IPromptRefiner
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly StubPromptRefiner _fallback = new();

    private const string SystemPrompt =
        """
        You are an image-editing assistant. The user will describe an image transformation they want.
        Your job is to:
        1. Rewrite their request as a clear, concise image-processing instruction.
        2. Generate a short (2-4 word) kebab-case title for the editing session.

        Respond ONLY with JSON in this exact format (no markdown, no extra text):
        {"refinedPrompt": "...", "title": "..."}

        Supported transformations the system can apply:
        grayscale, sepia, blur (with optional radius), sharpen, brighten, darken,
        contrast, invert/negative, resize WxH, rotate degrees, flip horizontal, flip vertical.

        If the user asks for something not in this list, map it to the closest supported
        transformation and mention what you chose in the refined prompt.
        """;

    public OpenAIPromptRefiner(HttpClient httpClient, string? apiKey, string model = "gpt-4o-mini")
    {
        _http = httpClient;
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<PromptRefineResult> RefineAsync(string userPrompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return await _fallback.RefineAsync(userPrompt, cancellationToken);

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = JsonContent.Create(new
            {
                model = _model,
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,
                max_tokens = 200
            });

            var response = await _http.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<OpenAIResponse>(cancellationToken: cancellationToken);
            var messageContent = body?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(messageContent))
                return await _fallback.RefineAsync(userPrompt, cancellationToken);

            // Strip markdown code fences if present
            var json = StripCodeFences(messageContent);

            var parsed = JsonSerializer.Deserialize<RefineJson>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed is null || string.IsNullOrWhiteSpace(parsed.RefinedPrompt))
                return await _fallback.RefineAsync(userPrompt, cancellationToken);

            return new PromptRefineResult
            {
                RefinedPrompt = parsed.RefinedPrompt,
                GeneratedTitle = string.IsNullOrWhiteSpace(parsed.Title)
                    ? GenerateFallbackTitle(userPrompt)
                    : parsed.Title
            };
        }
        catch
        {
            // On any failure (network, rate limit, parse error) fall back gracefully
            return await _fallback.RefineAsync(userPrompt, cancellationToken);
        }
    }

    private static string StripCodeFences(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];
            if (trimmed.EndsWith("```"))
                trimmed = trimmed[..^3];
        }
        return trimmed.Trim();
    }

    private static string GenerateFallbackTitle(string prompt)
    {
        var words = NonAlphanumericRegex().Replace(prompt.Trim(), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var title = string.Join("-", words.Take(4)).ToLowerInvariant();
        return string.IsNullOrEmpty(title) ? "session" : title;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9\s]")]
    private static partial Regex NonAlphanumericRegex();

    // -- JSON deserialization types -------------------------------------------

    private sealed class RefineJson
    {
        public string RefinedPrompt { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    private sealed class OpenAIResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public MessageContent? Message { get; set; }
    }

    private sealed class MessageContent
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
