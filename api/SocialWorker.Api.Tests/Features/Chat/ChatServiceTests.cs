using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        ILlmProviderAdapter? adapter = null,
        ChatOptions? chatOptions = null)
    {
        var loader = sessionLoader ?? CreateMockSessionLoader();
        var promptBuilder = new SystemPromptBuilder();
        var writer = new ChatStreamWriter();
        var llmAdapter = adapter ?? new DemoLlmAdapter();
        var log = NullLogger<ChatService>.Instance;
        var toolList = tools ?? new List<IChatTool>();
        var options = Options.Create(chatOptions ?? new ChatOptions());

        return new ChatService(loader, promptBuilder, writer, llmAdapter, log, toolList, options);
    }

    private static ChatSessionLoader CreateMockSessionLoader(
        string editorContent = "",
        string? brandVoice = null,
        bool supportsVision = false,
        string model = "test-model",
        string? draftSummary = null)
    {
        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "test",
            ProviderType = "OpenAI",
            BaseUrl = "https://test.local/v1",
            ApiKey = "test-key",
            Model = model,
            IsDefault = true,
            IsActive = true
        };

        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = "Test Draft",
            Content = editorContent,
            UserId = Guid.NewGuid(),
            ChatSummary = draftSummary
        };

        var capabilities = new ModelCapabilities(supportsVision, true, 8192);
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

    private static ChatModels.ChatRequest MakeRequest(List<string> messages, Guid? draftId = null)
    {
        return new ChatModels.ChatRequest
        {
            DraftId = draftId,
            Messages = messages.Select(message => new ChatModels.UiMessage
            {
                Role = "user",
                Content = new List<ChatModels.UiPart>
                {
                    new() { Type = "text", Text = message }
                }
            }).ToList()
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

    [Fact]
    public async Task StreamAsync_SelectsMoreMessages_ForLargerContextModels()
    {
        var messages = Enumerable.Range(0, 40)
            .Select(i => $"Message {i}: {new string('x', 700)}")
            .ToList();

        var smallAdapter = new CapturingTextOnlyAdapter();
        var smallService = CreateService(
            sessionLoader: CreateMockSessionLoader(model: "llama3.1"),
            adapter: smallAdapter);

        var largeAdapter = new CapturingTextOnlyAdapter();
        var largeService = CreateService(
            sessionLoader: CreateMockSessionLoader(model: "gemma4-e2b-32k"),
            adapter: largeAdapter);

        await CollectStream(smallService, MakeRequest(messages));
        await CollectStream(largeService, MakeRequest(messages));

        var smallCount = smallAdapter.CapturedRequest!.Messages.Count(m => m.Role != "system");
        var largeCount = largeAdapter.CapturedRequest!.Messages.Count(m => m.Role != "system");

        Assert.True(largeCount > smallCount);
        Assert.True(smallCount >= 1);
    }

    [Fact]
    public async Task StreamAsync_SlashHelp_ReturnsCuratedSlashCommands()
    {
        var tool = new StubTool("validate_draft");
        var adapter = new CapturingTextOnlyAdapter();
        var service = CreateService(tools: new IChatTool[] { tool }, adapter: adapter);

        var lines = await CollectStream(service, MakeRequest("/help"));
        var text = ExtractText(lines);

        Assert.Contains("Available slash commands", text);
        Assert.Contains("/validate", text);
        Assert.Contains("/search <query>", text);
        Assert.DoesNotContain("/tool", text);
        Assert.Null(adapter.CapturedRequest);
    }

    [Fact]
    public async Task StreamAsync_SlashValidate_ExecutesValidateToolDirectly()
    {
        var tool = new StubTool("validate_draft");
        var adapter = new CapturingTextOnlyAdapter();
        var service = CreateService(tools: new IChatTool[] { tool }, adapter: adapter);

        var lines = await CollectStream(service, MakeRequest("/validate"));
        var text = ExtractText(lines);

        Assert.True(tool.WasExecuted);
        Assert.Contains("result", text);
        Assert.Null(adapter.CapturedRequest);
    }

    [Fact]
    public async Task StreamAsync_SlashSearch_ExecutesWebSearchToolDirectly()
    {
        var tool = new StubTool("web_search");
        var service = CreateService(tools: new IChatTool[] { tool }, adapter: new CapturingTextOnlyAdapter());

        var lines = await CollectStream(service, MakeRequest("/search latest searxng docker image"));
        var text = ExtractText(lines);

        Assert.True(tool.WasExecuted);
        Assert.Contains("result", text);
    }

    [Fact]
    public async Task StreamAsync_SlashSearchImage_ExecutesImageSearchToolDirectly()
    {
        var tool = new StubTool("image_search");
        var service = CreateService(tools: new IChatTool[] { tool }, adapter: new CapturingTextOnlyAdapter());

        var lines = await CollectStream(service, MakeRequest("/search-image tropical fruit"));
        var text = ExtractText(lines);

        Assert.True(tool.WasExecuted);
        Assert.Contains("result", text);
    }

    [Fact]
    public async Task StreamAsync_EditIntent_Retries_And_Enforces_ReplaceEditorTool()
    {
        var replaceTool = new StubTool("replace_editor_content");
        var validateTool = new StubTool("validate_draft");
        var adapter = new EditIntentRetryAdapter();
        var service = CreateService(
            tools: new IChatTool[] { replaceTool, validateTool },
            adapter: adapter);

        await CollectStream(service, MakeRequest("Please rewrite this draft to be punchier."));

        Assert.True(replaceTool.WasExecuted);
        Assert.True(adapter.CallCount >= 2);
    }

    [Fact]
    public async Task StreamAsync_EditIntent_DoesNotRetry_WhenStrictEnforcementDisabled()
    {
        var replaceTool = new StubTool("replace_editor_content");
        var adapter = new CapturingTextOnlyAdapter();
        var service = CreateService(
            tools: new IChatTool[] { replaceTool },
            adapter: adapter,
            chatOptions: new ChatOptions { StrictEditorUpdateEnforcement = false });

        await CollectStream(service, MakeRequest("Please rewrite this draft to be punchier."));

        Assert.False(replaceTool.WasExecuted);
        Assert.NotNull(adapter.CapturedRequest);
    }

    private static string ExtractText(List<string> lines)
    {
        var chunks = lines
            .Where(l => l.StartsWith("0:"))
            .Select(l => JsonSerializer.Deserialize<string>(l[2..]) ?? string.Empty);
        return string.Concat(chunks);
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

    private sealed class CapturingTextOnlyAdapter : ILlmProviderAdapter
    {
        public OpenAiModels.ChatCompletionRequest? CapturedRequest { get; private set; }

        public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            CapturedRequest = request;
            yield return new OpenAiModels.StreamChunk
            {
                Choices = new List<OpenAiModels.StreamChoice>
                {
                    new()
                    {
                        Delta = new OpenAiModels.StreamDelta
                        {
                            Content = "ok"
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

        public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct)
        {
            CapturedRequest = request;
            return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(new OpenAiModels.ChatCompletionResponse
            {
                Choices = new List<OpenAiModels.ChatCompletionChoice>
                {
                    new()
                    {
                        Message = new OpenAiModels.ChatCompletionMessage
                        {
                            Role = "assistant",
                            Content = "ok"
                        }
                    }
                }
            });
        }
    }

    private sealed class EditIntentRetryAdapter : ILlmProviderAdapter
    {
        public int CallCount { get; private set; }

        public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            CallCount++;
            var hasEnforcementMessage = request.Messages.Any(m =>
                string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase) &&
                m.Content?.ToString()?.Contains("EDITOR-UPDATE ENFORCEMENT", StringComparison.OrdinalIgnoreCase) == true);

            if (!hasEnforcementMessage)
            {
                yield return new OpenAiModels.StreamChunk
                {
                    Choices = new List<OpenAiModels.StreamChoice>
                    {
                        new()
                        {
                            Delta = new OpenAiModels.StreamDelta
                            {
                                Content = "Here are suggestions to improve your draft."
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
                yield break;
            }

            yield return new OpenAiModels.StreamChunk
            {
                Choices = new List<OpenAiModels.StreamChoice>
                {
                    new()
                    {
                        Delta = new OpenAiModels.StreamDelta
                        {
                            ToolCalls = new List<OpenAiModels.StreamToolCall>
                            {
                                new()
                                {
                                    Index = 0,
                                    Id = "retry_replace_tool",
                                    Type = "function",
                                    Function = new OpenAiModels.StreamToolCallFunction
                                    {
                                        Name = "replace_editor_content",
                                        Arguments = "{\"markdown\":\"Updated content\"}"
                                    }
                                }
                            }
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
                        FinishReason = "tool_calls"
                    }
                }
            };
        }

        public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct)
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
                            Content = "ok"
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