using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Chat;

public sealed class DemoLlmAdapter : ILlmProviderAdapter
{
    public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
        OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, [EnumeratorCancellation] CancellationToken ct)
    {
        yield return new OpenAiModels.StreamChunk
        {
            Choices = new List<OpenAiModels.StreamChoice>
            {
                new()
                {
                    Delta = new OpenAiModels.StreamDelta
                    {
                        Content = "I've reviewed your draft. Here are my suggestions to improve engagement and clarity:\n\n1. **Stronger hook** — Open with a question or bold statement\n2. **Shorten sentences** — Break long clauses into punchy standalone lines\n3. **Add a call to action** — Tell readers what to do next\n\nWould you like me to apply these changes via `replace_editor_content`?"
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
                    Delta = new OpenAiModels.StreamDelta
                    {
                        ToolCalls = new List<OpenAiModels.StreamToolCall>
                        {
                            new()
                            {
                                Index = 0,
                                Id = "demo_tool_1",
                                Type = "function",
                                Function = new OpenAiModels.StreamToolCallFunction
                                {
                                    Name = "propose_stage_transition",
                                    Arguments = """{"platform":"Bluesky","stage":"Ready","reasoning":"Draft has been reviewed and refined. Ready for platform adaptation."}"""
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
                    FinishReason = "stop"
                }
            }
        };
    }

    public Task<OpenAiModels.ChatCompletionResponse?> CompleteAsync(
        OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, CancellationToken ct)
    {
        var response = new OpenAiModels.ChatCompletionResponse
        {
            Choices = new List<OpenAiModels.ChatCompletionChoice>
            {
                new()
                {
                    Message = new OpenAiModels.ChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = "This is a demo response. The conversation has been summarized for context retention."
                    }
                }
            }
        };

        return Task.FromResult<OpenAiModels.ChatCompletionResponse?>(response);
    }
}