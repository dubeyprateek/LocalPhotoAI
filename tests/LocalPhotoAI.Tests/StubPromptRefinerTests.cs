using LocalPhotoAI.Shared.Pipelines;

namespace LocalPhotoAI.Tests;

public class StubPromptRefinerTests
{
    [Fact]
    public async Task RefineAsync_WrapsPromptInStructuredFormat()
    {
        var refiner = new StubPromptRefiner();
        var result = await refiner.RefineAsync("remove background");

        Assert.Contains("remove background", result.RefinedPrompt);
        Assert.Contains("Apply the following transformation", result.RefinedPrompt);
        Assert.Contains("Preserve original resolution", result.RefinedPrompt);
    }

    [Fact]
    public async Task RefineAsync_GeneratesTitleFromFirstWords()
    {
        var refiner = new StubPromptRefiner();
        var result = await refiner.RefineAsync("remove the blue sky");

        Assert.Equal("remove-the-blue-sky", result.GeneratedTitle);
    }

    [Fact]
    public async Task RefineAsync_TitleLimitedToFourWords()
    {
        var refiner = new StubPromptRefiner();
        var result = await refiner.RefineAsync("one two three four five six");

        var wordCount = result.GeneratedTitle.Split('-').Length;
        Assert.Equal(4, wordCount);
        Assert.Equal("one-two-three-four", result.GeneratedTitle);
    }

    [Fact]
    public async Task RefineAsync_TitleIsLowercase()
    {
        var refiner = new StubPromptRefiner();
        var result = await refiner.RefineAsync("Remove Background Now");

        Assert.Equal(result.GeneratedTitle, result.GeneratedTitle.ToLowerInvariant());
    }

    [Fact]
    public async Task RefineAsync_TrimsWhitespace()
    {
        var refiner = new StubPromptRefiner();
        var result = await refiner.RefineAsync("  add blur  ");

        Assert.Contains("add blur", result.RefinedPrompt);
        Assert.DoesNotContain("  add blur  ", result.RefinedPrompt);
    }

    [Fact]
    public async Task RefineAsync_StripsNonAlphanumericFromTitle()
    {
        var refiner = new StubPromptRefiner();
        var result = await refiner.RefineAsync("remove! the @blue #sky");

        // Non-alphanumeric chars should be stripped from title
        Assert.DoesNotContain("!", result.GeneratedTitle);
        Assert.DoesNotContain("@", result.GeneratedTitle);
        Assert.DoesNotContain("#", result.GeneratedTitle);
    }

    [Fact]
    public async Task RefineAsync_EmptyPromptWords_ReturnsFallbackTitle()
    {
        var refiner = new StubPromptRefiner();
        // Only special characters, no alpha words
        var result = await refiner.RefineAsync("!@#$%");

        Assert.Equal("session", result.GeneratedTitle);
    }

    [Fact]
    public async Task RefineAsync_SingleWord_TitleIsSingleWord()
    {
        var refiner = new StubPromptRefiner();
        var result = await refiner.RefineAsync("enhance");

        Assert.Equal("enhance", result.GeneratedTitle);
    }
}
