using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Chat;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Chat;

public sealed class DemoLlmAdapterTests
{
    [Fact]
    public async Task CompleteStreamAsync_UsesScenario_WhenProfileSetAndMessageMatches()
    {
        var original = Environment.GetEnvironmentVariable("DEMO_LLM_PROFILE");
        Environment.SetEnvironmentVariable("DEMO_LLM_PROFILE", "getting-started");
        try
        {
            var adapter = new DemoLlmAdapter();
            var req = MakeRequest("Can you review my draft and suggest improvements?");

            var chunks = await CollectChunks(adapter, req);
            var text = string.Concat(chunks.SelectMany(c => c.Choices).Select(c => c.Delta.Content).Where(s => !string.IsNullOrEmpty(s)));

            Assert.Contains("walkthrough", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Would you like me to apply", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEMO_LLM_PROFILE", original);
        }
    }

    [Fact]
    public async Task CompleteStreamAsync_FallsBackToDefault_WhenNoProfileSet()
    {
        var original = Environment.GetEnvironmentVariable("DEMO_LLM_PROFILE");
        Environment.SetEnvironmentVariable("DEMO_LLM_PROFILE", null);
        try
        {
            var adapter = new DemoLlmAdapter();
            var req = MakeRequest("Can you review my draft and suggest improvements?");

            var chunks = await CollectChunks(adapter, req);
            var text = string.Concat(chunks.SelectMany(c => c.Choices).Select(c => c.Delta.Content).Where(s => !string.IsNullOrEmpty(s)));
            var hasToolCalls = chunks.SelectMany(c => c.Choices).Any(c => c.Delta.ToolCalls is { Count: > 0 });

            Assert.Contains("Would you like me to apply these changes", text, StringComparison.OrdinalIgnoreCase);
            Assert.True(hasToolCalls);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DEMO_LLM_PROFILE", original);
        }
    }

    private static OpenAiModels.ChatCompletionRequest MakeRequest(string text)
    {
        return new OpenAiModels.ChatCompletionRequest
        {
            Model = "demo-model",
            Messages = new List<OpenAiModels.OpenAiMessage>
            {
                new()
                {
                    Role = "user",
                    Content = text
                }
            }
        };
    }

    private static async Task<List<OpenAiModels.StreamChunk>> CollectChunks(DemoLlmAdapter adapter, OpenAiModels.ChatCompletionRequest req)
    {
        var chunks = new List<OpenAiModels.StreamChunk>();
        await foreach (var chunk in adapter.CompleteStreamAsync(req, new LlmCredentials("https://demo", "key", "model"), CancellationToken.None))
        {
            chunks.Add(chunk);
        }
        return chunks;
    }
}
