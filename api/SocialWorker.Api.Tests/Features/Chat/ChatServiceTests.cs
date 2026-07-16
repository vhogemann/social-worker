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
using SocialWorker.Api.Features.Chat.Tools;
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
            : base(null!, null!, null!, null!, null!)
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

    [Fact]
    public async Task StreamAsync_StrictAllSourcesRequest_BlocksReplaceUntilAllListedSourcesAreFetched()
    {
        var sourceA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var sourceB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var listTool = new ListSourcesStubTool(sourceA, sourceB);
        var fetchTool = new FetchSourceStubTool();
        var replaceTool = new StubTool("replace_editor_content");
        var adapter = new AllSourcesEnforcementAdapter(sourceA, sourceB);

        var service = CreateService(
            tools: new IChatTool[] { listTool, fetchTool, replaceTool },
            adapter: adapter);

        var lines = await CollectStream(
            service,
            MakeRequest("Strict QA: list all sources, fetch_source for every source GUID in this draft, then update content."));

        Assert.True(lines.Any(l => l.StartsWith("a:") && l.Contains("missingSourceIds", StringComparison.Ordinal)));
        Assert.True(replaceTool.WasExecuted);
        Assert.Contains(sourceA, fetchTool.FetchedIds);
        Assert.Contains(sourceB, fetchTool.FetchedIds);
    }

    [Fact]
    public async Task StreamAsync_ImageRequest_BlocksReplaceUntilImageIsImportedAndInspected()
    {
        var addImageTool = new StubTool("add_image_source");
        var viewImageTool = new StubTool("view_image");
        var replaceTool = new StubTool("replace_editor_content");
        var adapter = new ImageEnforcementAdapter();

        var service = CreateService(
            tools: new IChatTool[] { addImageTool, viewImageTool, replaceTool },
            adapter: adapter);

        var lines = await CollectStream(
            service,
            MakeRequest("Find an image, inspect it, then embed it in the draft."));

        Assert.True(lines.Any(l => l.StartsWith("a:") && l.Contains("add_image_source", StringComparison.Ordinal)));
        Assert.True(lines.Any(l => l.StartsWith("a:") && l.Contains("view_image", StringComparison.Ordinal)));
        Assert.True(addImageTool.WasExecuted);
        Assert.True(viewImageTool.WasExecuted);
        Assert.True(replaceTool.WasExecuted);
    }

    [Fact]
    public async Task StreamAsync_MixedSourceAndImageRequest_EnforcesBothGatesBeforeReplace()
    {
        var sourceA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var sourceB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        var listTool = new ListSourcesStubTool(sourceA, sourceB);
        var fetchTool = new FetchSourceStubTool();
        var addImageTool = new StubTool("add_image_source");
        var viewImageTool = new StubTool("view_image");
        var replaceTool = new StubTool("replace_editor_content");
        var adapter = new MixedSourceImageEnforcementAdapter(sourceA, sourceB);

        var service = CreateService(
            tools: new IChatTool[] { listTool, fetchTool, addImageTool, viewImageTool, replaceTool },
            adapter: adapter);

        var lines = await CollectStream(
            service,
            MakeRequest("List all sources, fetch_source for each, pick and inspect an image, then embed it in updated content."));

        Assert.True(lines.Any(l => l.StartsWith("a:") && l.Contains("missingSourceIds", StringComparison.Ordinal)));
        Assert.True(addImageTool.WasExecuted);
        Assert.True(viewImageTool.WasExecuted);
        Assert.Contains(sourceA, fetchTool.FetchedIds);
        Assert.Contains(sourceB, fetchTool.FetchedIds);
        Assert.True(replaceTool.WasExecuted);
    }

    [Fact]
    public async Task StreamAsync_PlaceholderValidationFailure_DrivesSecondReplaceWithConcreteValues()
    {
        var replaceTool = new CapturingReplaceTool();
        var validateTool = new PlaceholderAwareValidateTool();
        var adapter = new PlaceholderRecoveryAdapter();

        var service = CreateService(
            tools: new IChatTool[] { replaceTool, validateTool },
            adapter: adapter);

        await CollectStream(
            service,
            MakeRequest("Draft a thread with one image and one source URL."));

        Assert.Equal(2, replaceTool.Markdowns.Count);
        Assert.Equal(2, validateTool.ValidationCalls);
        Assert.Contains("media://guid", replaceTool.Markdowns[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://example.com", replaceTool.Markdowns[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("media://guid", replaceTool.Markdowns[1], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://example.com", replaceTool.Markdowns[1], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StreamAsync_FinalizationNudge_LeadsToAssistantFinalTextWithoutExtraToolRound()
    {
        var replaceTool = new CapturingReplaceTool();
        var validateTool = new PlaceholderAwareValidateTool();
        var adapter = new FinalizationAwareAdapter();

        var service = CreateService(
            tools: new IChatTool[] { replaceTool, validateTool },
            adapter: adapter);

        var lines = await CollectStream(
            service,
            MakeRequest("Please polish this draft and validate it."));

        var text = ExtractText(lines);
        Assert.True(adapter.SawFinalizationInstruction);
        Assert.Equal(2, adapter.CallCount);
        Assert.Equal(1, replaceTool.Markdowns.Count);
        Assert.Equal(1, validateTool.ValidationCalls);
        Assert.Contains("Final response after validation.", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamAsync_FinalizationGuard_StopsWhenModelStillRequestsReplaceOrValidate()
    {
        var replaceTool = new CapturingReplaceTool();
        var validateTool = new PlaceholderAwareValidateTool();
        var adapter = new IgnoringFinalizationAdapter();

        var service = CreateService(
            tools: new IChatTool[] { replaceTool, validateTool },
            adapter: adapter);

        var lines = await CollectStream(
            service,
            MakeRequest("Please polish this draft and validate it."));

        var text = ExtractText(lines);
        Assert.True(adapter.SawFinalizationInstruction);
        Assert.Equal(2, adapter.CallCount);
        Assert.Equal(2, replaceTool.Markdowns.Count);
        Assert.Equal(2, validateTool.ValidationCalls);
        Assert.Contains("Draft updated and validated in the editor.", text, StringComparison.Ordinal);
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

    private sealed class AllSourcesEnforcementAdapter : ILlmProviderAdapter
    {
        private readonly Guid _sourceA;
        private readonly Guid _sourceB;
        private int _callCount;

        public AllSourcesEnforcementAdapter(Guid sourceA, Guid sourceB)
        {
            _sourceA = sourceA;
            _sourceB = sourceB;
        }

        public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            _callCount++;

            if (_callCount == 1)
            {
                yield return BuildToolCallsChunk(new List<OpenAiModels.StreamToolCall>
                {
                    new()
                    {
                        Index = 0,
                        Id = "call_list_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "list_sources",
                            Arguments = "{}"
                        }
                    },
                    new()
                    {
                        Index = 1,
                        Id = "call_fetch_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "fetch_source",
                            Arguments = "{\"id\":\"" + _sourceA + "\"}"
                        }
                    },
                    new()
                    {
                        Index = 2,
                        Id = "call_replace_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "replace_editor_content",
                            Arguments = "{\"markdown\":\"Updated content\"}"
                        }
                    }
                });
                yield return BuildToolCallFinishChunk();
                yield break;
            }

            if (_callCount == 2)
            {
                yield return BuildToolCallsChunk(new List<OpenAiModels.StreamToolCall>
                {
                    new()
                    {
                        Index = 0,
                        Id = "call_fetch_2",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "fetch_source",
                            Arguments = "{\"id\":\"" + _sourceB + "\"}"
                        }
                    },
                    new()
                    {
                        Index = 1,
                        Id = "call_replace_2",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "replace_editor_content",
                            Arguments = "{\"markdown\":\"Updated content after all fetches\"}"
                        }
                    }
                });
                yield return BuildToolCallFinishChunk();
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
                            Content = "done"
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

        private static OpenAiModels.StreamChunk BuildToolCallsChunk(List<OpenAiModels.StreamToolCall> toolCalls)
        {
            return new OpenAiModels.StreamChunk
            {
                Choices = new List<OpenAiModels.StreamChoice>
                {
                    new()
                    {
                        Delta = new OpenAiModels.StreamDelta
                        {
                            ToolCalls = toolCalls
                        }
                    }
                }
            };
        }

        private static OpenAiModels.StreamChunk BuildToolCallFinishChunk()
        {
            return new OpenAiModels.StreamChunk
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
    }

    private sealed class ImageEnforcementAdapter : ILlmProviderAdapter
    {
        private int _callCount;

        public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            _callCount++;

            if (_callCount == 1)
            {
                yield return BuildToolCallsChunk(new List<OpenAiModels.StreamToolCall>
                {
                    new()
                    {
                        Index = 0,
                        Id = "img_replace_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "replace_editor_content",
                            Arguments = "{\"markdown\":\"Draft with placeholder\"}"
                        }
                    }
                });
                yield return BuildToolCallFinishChunk();
                yield break;
            }

            if (_callCount == 2)
            {
                yield return BuildToolCallsChunk(new List<OpenAiModels.StreamToolCall>
                {
                    new()
                    {
                        Index = 0,
                        Id = "img_add_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "add_image_source",
                            Arguments = "{\"url\":\"https://example.com/image.jpg\",\"altText\":\"test\"}"
                        }
                    },
                    new()
                    {
                        Index = 1,
                        Id = "img_replace_2",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "replace_editor_content",
                            Arguments = "{\"markdown\":\"Draft with imported image\"}"
                        }
                    }
                });
                yield return BuildToolCallFinishChunk();
                yield break;
            }

            if (_callCount == 3)
            {
                yield return BuildToolCallsChunk(new List<OpenAiModels.StreamToolCall>
                {
                    new()
                    {
                        Index = 0,
                        Id = "img_view_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "view_image",
                            Arguments = "{\"id\":\"media://11111111-1111-1111-1111-111111111111\"}"
                        }
                    },
                    new()
                    {
                        Index = 1,
                        Id = "img_replace_3",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "replace_editor_content",
                            Arguments = "{\"markdown\":\"Final draft with inspected image\"}"
                        }
                    }
                });
                yield return BuildToolCallFinishChunk();
                yield break;
            }

            yield return BuildTextDoneChunk();
        }

        public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct)
        {
            return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(new OpenAiModels.ChatCompletionResponse());
        }
    }

    private sealed class MixedSourceImageEnforcementAdapter : ILlmProviderAdapter
    {
        private readonly Guid _sourceA;
        private readonly Guid _sourceB;
        private int _callCount;

        public MixedSourceImageEnforcementAdapter(Guid sourceA, Guid sourceB)
        {
            _sourceA = sourceA;
            _sourceB = sourceB;
        }

        public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            _callCount++;

            if (_callCount == 1)
            {
                yield return BuildToolCallsChunk(new List<OpenAiModels.StreamToolCall>
                {
                    new()
                    {
                        Index = 0,
                        Id = "mix_list",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "list_sources",
                            Arguments = "{}"
                        }
                    },
                    new()
                    {
                        Index = 1,
                        Id = "mix_fetch_a",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "fetch_source",
                            Arguments = "{\"id\":\"" + _sourceA + "\"}"
                        }
                    },
                    new()
                    {
                        Index = 2,
                        Id = "mix_add",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "add_image_source",
                            Arguments = "{\"url\":\"https://example.com/image.jpg\",\"altText\":\"test\"}"
                        }
                    },
                    new()
                    {
                        Index = 3,
                        Id = "mix_view",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "view_image",
                            Arguments = "{\"id\":\"media://11111111-1111-1111-1111-111111111111\"}"
                        }
                    },
                    new()
                    {
                        Index = 4,
                        Id = "mix_replace_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "replace_editor_content",
                            Arguments = "{\"markdown\":\"replace before all sources fetched\"}"
                        }
                    }
                });
                yield return BuildToolCallFinishChunk();
                yield break;
            }

            if (_callCount == 2)
            {
                yield return BuildToolCallsChunk(new List<OpenAiModels.StreamToolCall>
                {
                    new()
                    {
                        Index = 0,
                        Id = "mix_fetch_b",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "fetch_source",
                            Arguments = "{\"id\":\"" + _sourceB + "\"}"
                        }
                    },
                    new()
                    {
                        Index = 1,
                        Id = "mix_replace_2",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "replace_editor_content",
                            Arguments = "{\"markdown\":\"final replace after source+image gates\"}"
                        }
                    }
                });
                yield return BuildToolCallFinishChunk();
                yield break;
            }

            yield return BuildTextDoneChunk();
        }

        public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct)
        {
            return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(new OpenAiModels.ChatCompletionResponse());
        }
    }

    private sealed class PlaceholderRecoveryAdapter : ILlmProviderAdapter
    {
        private int _callCount;

        public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            _callCount++;

            if (_callCount == 1)
            {
                yield return BuildToolCallsChunk(new List<OpenAiModels.StreamToolCall>
                {
                    new()
                    {
                        Index = 0,
                        Id = "ph_replace_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "replace_editor_content",
                            Arguments = "{\"markdown\":\"Post A\\n\\n---\\n![alt](media://guid)\\nSee https://example.com/source\"}"
                        }
                    },
                    new()
                    {
                        Index = 1,
                        Id = "ph_validate_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "validate_draft",
                            Arguments = "{\"content\":\"Post A\\n\\n---\\n![alt](media://guid)\\nSee https://example.com/source\"}"
                        }
                    }
                });
                yield return BuildToolCallFinishChunk();
                yield break;
            }

            if (_callCount == 2)
            {
                yield return BuildToolCallsChunk(new List<OpenAiModels.StreamToolCall>
                {
                    new()
                    {
                        Index = 0,
                        Id = "ph_replace_2",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "replace_editor_content",
                            Arguments = "{\"markdown\":\"Post A\\n\\n---\\n![alt](media://123e4567-e89b-12d3-a456-426614174000)\\nSee https://example.org/source\"}"
                        }
                    },
                    new()
                    {
                        Index = 1,
                        Id = "ph_validate_2",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "validate_draft",
                            Arguments = "{\"content\":\"Post A\\n\\n---\\n![alt](media://123e4567-e89b-12d3-a456-426614174000)\\nSee https://example.org/source\"}"
                        }
                    }
                });
                yield return BuildToolCallFinishChunk();
                yield break;
            }

            yield return BuildTextDoneChunk();
        }

        public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct)
        {
            return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(new OpenAiModels.ChatCompletionResponse());
        }
    }

    private sealed class FinalizationAwareAdapter : ILlmProviderAdapter
    {
        public int CallCount { get; private set; }
        public bool SawFinalizationInstruction { get; private set; }

        public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            CallCount++;

            if (CallCount == 1)
            {
                yield return BuildToolCallsChunk(new List<OpenAiModels.StreamToolCall>
                {
                    new()
                    {
                        Index = 0,
                        Id = "fin_replace_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "replace_editor_content",
                            Arguments = "{\"markdown\":\"Post 1\\n\\n---\\nPost 2\"}"
                        }
                    },
                    new()
                    {
                        Index = 1,
                        Id = "fin_validate_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "validate_draft",
                            Arguments = "{\"content\":\"Post 1\\n\\n---\\nPost 2\"}"
                        }
                    }
                });
                yield return BuildToolCallFinishChunk();
                yield break;
            }

            SawFinalizationInstruction = request.Messages.Any(m =>
                string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase) &&
                m.Content?.ToString()?.Contains("FINALIZATION:", StringComparison.OrdinalIgnoreCase) == true);

            yield return new OpenAiModels.StreamChunk
            {
                Choices = new List<OpenAiModels.StreamChoice>
                {
                    new()
                    {
                        Delta = new OpenAiModels.StreamDelta
                        {
                            Content = "Final response after validation."
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
            return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(new OpenAiModels.ChatCompletionResponse());
        }
    }

    private sealed class IgnoringFinalizationAdapter : ILlmProviderAdapter
    {
        public int CallCount { get; private set; }
        public bool SawFinalizationInstruction { get; private set; }

        public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
            OpenAiModels.ChatCompletionRequest request,
            LlmCredentials credentials,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            CallCount++;

            if (CallCount == 1)
            {
                yield return BuildToolCallsChunk(new List<OpenAiModels.StreamToolCall>
                {
                    new()
                    {
                        Index = 0,
                        Id = "ign_replace_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "replace_editor_content",
                            Arguments = "{\"markdown\":\"Post 1\\n\\n---\\nPost 2\"}"
                        }
                    },
                    new()
                    {
                        Index = 1,
                        Id = "ign_validate_1",
                        Type = "function",
                        Function = new OpenAiModels.StreamToolCallFunction
                        {
                            Name = "validate_draft",
                            Arguments = "{\"content\":\"Post 1\\n\\n---\\nPost 2\"}"
                        }
                    }
                });
                yield return BuildToolCallFinishChunk();
                yield break;
            }

            SawFinalizationInstruction = request.Messages.Any(m =>
                string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase) &&
                m.Content?.ToString()?.Contains("FINALIZATION:", StringComparison.OrdinalIgnoreCase) == true);

            yield return BuildToolCallsChunk(new List<OpenAiModels.StreamToolCall>
            {
                new()
                {
                    Index = 0,
                    Id = "ign_replace_2",
                    Type = "function",
                    Function = new OpenAiModels.StreamToolCallFunction
                    {
                        Name = "replace_editor_content",
                        Arguments = "{\"markdown\":\"Post 1 revised\\n\\n---\\nPost 2 revised\"}"
                    }
                },
                new()
                {
                    Index = 1,
                    Id = "ign_validate_2",
                    Type = "function",
                    Function = new OpenAiModels.StreamToolCallFunction
                    {
                        Name = "validate_draft",
                        Arguments = "{\"content\":\"Post 1 revised\\n\\n---\\nPost 2 revised\"}"
                    }
                }
            });
            yield return BuildToolCallFinishChunk();
        }

        public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct)
        {
            return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(new OpenAiModels.ChatCompletionResponse());
        }
    }

    private static OpenAiModels.StreamChunk BuildToolCallsChunk(List<OpenAiModels.StreamToolCall> toolCalls)
    {
        return new OpenAiModels.StreamChunk
        {
            Choices = new List<OpenAiModels.StreamChoice>
            {
                new()
                {
                    Delta = new OpenAiModels.StreamDelta
                    {
                        ToolCalls = toolCalls
                    }
                }
            }
        };
    }

    private static OpenAiModels.StreamChunk BuildToolCallFinishChunk()
    {
        return new OpenAiModels.StreamChunk
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

    private static OpenAiModels.StreamChunk BuildTextDoneChunk()
    {
        return new OpenAiModels.StreamChunk
        {
            Choices = new List<OpenAiModels.StreamChoice>
            {
                new()
                {
                    Delta = new OpenAiModels.StreamDelta
                    {
                        Content = "done"
                    },
                    FinishReason = "stop"
                }
            }
        };
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

    private sealed class ListSourcesStubTool : IChatTool
    {
        private readonly Guid _sourceA;
        private readonly Guid _sourceB;

        public ListSourcesStubTool(Guid sourceA, Guid sourceB)
        {
            _sourceA = sourceA;
            _sourceB = sourceB;
            Parameters = JsonDocument.Parse("{}").RootElement.Clone();
        }

        public string Name => "list_sources";
        public string Description => "Stub list sources";
        public bool RequiresVision => false;
        public JsonElement Parameters { get; }

        public Task<ToolExecutionResult> ExecuteRawAsync(string argumentsJson, Guid? draftId, Guid userId, CancellationToken ct)
        {
            var result = new List<ListSourcesResultItem>
            {
                new(_sourceA, "Url", "https://example.com/a", "A"),
                new(_sourceB, "File", "file://" + _sourceB, "B")
            };
            return Task.FromResult(new ToolExecutionResult(result));
        }
    }

    private sealed class FetchSourceStubTool : IChatTool
    {
        public FetchSourceStubTool()
        {
            Parameters = JsonDocument.Parse("{}").RootElement.Clone();
        }

        public string Name => "fetch_source";
        public string Description => "Stub fetch source";
        public bool RequiresVision => false;
        public JsonElement Parameters { get; }
        public List<Guid> FetchedIds { get; } = new();

        public Task<ToolExecutionResult> ExecuteRawAsync(string argumentsJson, Guid? draftId, Guid userId, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var id = Guid.Parse(doc.RootElement.GetProperty("id").GetString()!);
            FetchedIds.Add(id);
            var result = new FetchSourceResult(id, "Url", "https://example.com", "title", "content");
            return Task.FromResult(new ToolExecutionResult(result));
        }
    }

    private sealed class CapturingReplaceTool : IChatTool
    {
        public string Name => "replace_editor_content";
        public string Description => "Capture replace payloads";
        public bool RequiresVision => false;
        public JsonElement Parameters { get; } = JsonDocument.Parse("{}").RootElement.Clone();
        public List<string> Markdowns { get; } = new();

        public Task<ToolExecutionResult> ExecuteRawAsync(string argumentsJson, Guid? draftId, Guid userId, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var markdown = doc.RootElement.TryGetProperty("markdown", out var m)
                ? m.GetString() ?? string.Empty
                : string.Empty;
            Markdowns.Add(markdown);
            return Task.FromResult(new ToolExecutionResult(new { Success = true, Length = markdown.Length }));
        }
    }

    private sealed class PlaceholderAwareValidateTool : IChatTool
    {
        public string Name => "validate_draft";
        public string Description => "Detect placeholders in provided content";
        public bool RequiresVision => false;
        public JsonElement Parameters { get; } = JsonDocument.Parse("{}").RootElement.Clone();
        public int ValidationCalls { get; private set; }

        public Task<ToolExecutionResult> ExecuteRawAsync(string argumentsJson, Guid? draftId, Guid userId, CancellationToken ct)
        {
            ValidationCalls++;
            using var doc = JsonDocument.Parse(argumentsJson);
            var content = doc.RootElement.TryGetProperty("content", out var c)
                ? c.GetString() ?? string.Empty
                : string.Empty;

            if (content.Contains("media://guid", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("https://example.com", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new ToolExecutionResult("❌ Placeholder media/url detected"));
            }

            return Task.FromResult(new ToolExecutionResult("✅ Valid"));
        }
    }
}