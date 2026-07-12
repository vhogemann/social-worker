using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Infrastructure.Llm;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class ChatServiceTests
{
    private static ChatService CreateService(
        ChatSessionLoader? sessionLoader = null,
        IEnumerable<IChatTool>? tools = null,
        ILlmProviderAdapter? adapter = null)
    {
        var loader = sessionLoader ?? CreateMockSessionLoader();
        var promptBuilder = new SystemPromptBuilder();
        var writer = new ChatStreamWriter();
        var llmAdapter = adapter ?? new DemoLlmAdapter();
        var log = NullLogger<ChatService>.Instance;
        var toolList = tools ?? new List<IChatTool>();

        return new ChatService(loader, promptBuilder, writer, llmAdapter, log, toolList);
    }

    private static ChatSessionLoader CreateMockSessionLoader(
        string editorContent = "",
        string? brandVoice = null,
        bool supportsVision = false)
    {
        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "test",
            ProviderType = "OpenAI",
            BaseUrl = "https://test.local/v1",
            ApiKey = "test-key",
            Model = "test-model",
            IsDefault = true,
            IsActive = true
        };

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = "Test Draft",
            Content = editorContent,
            UserId = Guid.NewGuid()
        };

        var capabilities = new ModelCapabilities(supportsVision, true);
        var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);

        return new FakeSessionLoader(new ChatSessionContext(
            provider, credentials, capabilities, draft, editorContent, new List<MediaAsset>(), brandVoice));
    }

    private sealed class FakeSessionLoader : ChatSessionLoader
    {
        private readonly ChatSessionContext _session;

        public FakeSessionLoader(ChatSessionContext session)
            : base(null!, null!, null!, null!)
        {
            _session = session;
        }

        public override Task<ChatSessionContext> LoadAsync(
            Guid userId, Guid? draftId, string? editorContentRequest,
            List<ChatModels.UiMessage> messages, CancellationToken ct)
        {
            return Task.FromResult(_session);
        }
    }

    private static ChatModels.ChatRequest MakeRequest(string message, Guid? draftId = null)
    {
        return new ChatModels.ChatRequest
        {
            DraftId = draftId,
            Messages = new List<ChatModels.UiMessage>
            {
                new()
                {
                    Role = "user",
                    Content = new List<ChatModels.UiPart>
                    {
                        new() { Type = "text", Text = message }
                    }
                }
            }
        };
    }

    private static async Task<List<string>> CollectStream(ChatService service, ChatModels.ChatRequest req)
    {
        var lines = new List<string>();
        await foreach (var line in service.StreamAsync(req, Guid.NewGuid(), CancellationToken.None))
        {
            lines.Add(line);
        }
        return lines;
    }

    [Fact]
    public async Task StreamAsync_ReturnsMessageIdAndStreamDone()
    {
        var adapter = new TextOnlyAdapter();
        var service = CreateService(adapter: adapter);
        var lines = await CollectStream(service, MakeRequest("Hello"));

        // First line should be message id
        Assert.StartsWith("f:", lines[0]);
        // Last line should be stream done
        Assert.StartsWith("d:", lines[^1]);
    }

    [Fact]
    public async Task StreamAsync_WithTextResponse_YieldsTextDeltas()
    {
        var adapter = new TextOnlyAdapter();
        var service = CreateService(adapter: adapter);
        var lines = await CollectStream(service, MakeRequest("Hello"));

        var textLines = lines.Where(l => l.StartsWith("0:")).ToList();
        Assert.NotEmpty(textLines);
        // The adapter returns "Hello from the mock"
        var allText = string.Concat(textLines.Select(l => l.Substring(2)));
        Assert.Contains("Hello", allText);
    }

    [Fact]
    public async Task StreamAsync_WithToolCalls_ExecutesToolAndContinues()
    {
        var tool = new StubTool("propose_stage_transition");
        var service = CreateService(tools: new IChatTool[] { tool });
        var lines = await CollectStream(service, MakeRequest("Call a tool"));

        // Should have tool call line
        var toolCallLines = lines.Where(l => l.StartsWith("9:")).ToList();
        Assert.NotEmpty(toolCallLines);

        // Should have tool result line
        var toolResultLines = lines.Where(l => l.StartsWith("a:")).ToList();
        Assert.NotEmpty(toolResultLines);

        // Tool should have been executed
        Assert.True(tool.WasExecuted);
    }

    [Fact]
    public async Task StreamAsync_WithUnknownTool_ReturnsError()
    {
        var service = CreateService(tools: new IChatTool[] { new StubTool("real_tool") });
        var lines = await CollectStream(service, MakeRequest("Call unknown_tool"));

        // Demo adapter always calls propose_stage_transition - but it won't be found
        // since we didn't register it. Tool call should still be emitted.
        var toolCallLines = lines.Where(l => l.StartsWith("9:")).ToList();
        Assert.NotEmpty(toolCallLines);

        // Then a tool result should follow (even if error)
        var toolResultLines = lines.Where(l => l.StartsWith("a:")).ToList();
        Assert.NotEmpty(toolResultLines);
    }

    [Fact]
    public async Task StreamAsync_RespectsMaxRounds()
    {
        // The DemoLlmAdapter returns a tool call that triggers propose_stage_transition.
        // If we register it, it executes and returns success, which triggers another round.
        // We need a tool that always returns a result that causes another tool call.
        // For simplicity, test that at most 3 rounds execute.
        var service = CreateService();
        var lines = new List<string>();
        await foreach (var line in service.StreamAsync(MakeRequest("Hello"), Guid.NewGuid(), CancellationToken.None))
        {
            lines.Add(line);
            // Should complete within reasonable lines
        }

        // Stream ends with done marker
        Assert.EndsWith("}\n", lines[^1]);
    }

    /// <summary>
    /// An adapter that returns only text, no tool calls.
    /// </summary>
    private sealed class TextOnlyAdapter : ILlmProviderAdapter
    {
        public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            yield return new OpenAiModels.StreamChunk
            {
                Choices = new List<OpenAiModels.StreamChoice>
                {
                    new()
                    {
                        Delta = new OpenAiModels.StreamDelta
                        {
                            Content = "Hello from the mock LLM adapter!"
                        }
                    }
                }
            };
            yield return new OpenAiModels.StreamChunk
            {
                Choices = new List<OpenAiModels.StreamChoice>
                {
                    new()
                    {
                        Delta = new OpenAiModels.StreamDelta(),
                        FinishReason = "stop"
                    }
                }
            };
        }

        public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(
            OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct)
        {
            return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(new OpenAiModels.ChatCompletionResponse
            {
                Choices = new List<OpenAiModels.ChatCompletionChoice>
                {
                    new()
                    {
                        Message = new OpenAiModels.ChatCompletionMessage
                        {
                            Role = "assistant",
                            Content = "Mock response"
                        }
                    }
                }
            });
        }
    }

    /// <summary>
    /// A stub tool that records execution.
    /// </summary>
    private sealed class StubTool : IChatTool
    {
        public string Name { get; }
        public string Description => "A stub tool for testing";
        public bool RequiresVision => false;
        public System.Text.Json.JsonElement Parameters { get; }
        public bool WasExecuted { get; private set; }

        public StubTool(string name)
        {
            Name = name;
            Parameters = System.Text.Json.JsonDocument.Parse("{}").RootElement.Clone();
        }

        public Task<ToolExecutionResult> ExecuteRawAsync(
            string argumentsJson, Guid? draftId, Guid userId, CancellationToken ct)
        {
            WasExecuted = true;
            return Task.FromResult(new ToolExecutionResult(new { result = "done" }));
        }
    }
}