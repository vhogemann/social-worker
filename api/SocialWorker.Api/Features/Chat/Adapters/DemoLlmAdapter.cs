using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Chat;

public sealed class DemoLlmAdapter : ILlmProviderAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, DemoScenarioStep>? _scenarioSteps;

    public DemoLlmAdapter()
    {
        _scenarioSteps = LoadScenarioSteps();
    }

    public async IAsyncEnumerable<OpenAiModels.StreamChunk> CompleteStreamAsync(
        OpenAiModels.ChatCompletionRequest request, LlmCredentials credentials, [EnumeratorCancellation] CancellationToken ct)
    {
        var scenarioResponse = TryBuildScenarioResponse(request);
        if (scenarioResponse is not null)
        {
            yield return scenarioResponse;
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

    private OpenAiModels.StreamChunk? TryBuildScenarioResponse(OpenAiModels.ChatCompletionRequest request)
    {
        if (_scenarioSteps is null || _scenarioSteps.Count == 0)
        {
            return null;
        }

        var lastUserMessage = request.Messages.LastOrDefault(m =>
            string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
        var lastUserText = lastUserMessage?.Content?.ToString();
        if (string.IsNullOrWhiteSpace(lastUserText))
        {
            return null;
        }

        foreach (var kvp in _scenarioSteps)
        {
            if (lastUserText.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return BuildTextChunk(kvp.Value.Response);
            }
        }

        return null;
    }

    private static OpenAiModels.StreamChunk BuildTextChunk(string text)
    {
        return new OpenAiModels.StreamChunk
        {
            Choices = new List<OpenAiModels.StreamChoice>
            {
                new()
                {
                    Delta = new OpenAiModels.StreamDelta
                    {
                        Content = text
                    }
                }
            }
        };
    }

    private static Dictionary<string, DemoScenarioStep>? LoadScenarioSteps()
    {
        var profile = Environment.GetEnvironmentVariable("DEMO_LLM_PROFILE");
        if (string.IsNullOrWhiteSpace(profile))
        {
            return null;
        }

        var path = ResolveScenarioPath(profile);
        if (path is null)
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var scenario = JsonSerializer.Deserialize<DemoScenario>(json, JsonOptions);
            if (scenario?.Steps is null || scenario.Steps.Count == 0)
            {
                return null;
            }

            return scenario.Steps
                .Where(s => !string.IsNullOrWhiteSpace(s.MatchContains) && !string.IsNullOrWhiteSpace(s.Response))
                .ToDictionary(s => s.MatchContains!, s => s, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveScenarioPath(string profile)
    {
        var fileName = $"{profile}.json";
        var candidates = new List<string>
        {
            Path.Combine("/app", "Features", "Chat", "DemoScenarios", fileName),
            Path.Combine(AppContext.BaseDirectory, "Features", "Chat", "DemoScenarios", fileName)
        };

        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrEmpty(current); i++)
        {
            candidates.Add(Path.Combine(current, "api", "SocialWorker.Api", "Features", "Chat", "DemoScenarios", fileName));
            candidates.Add(Path.Combine(current, "SocialWorker.Api", "Features", "Chat", "DemoScenarios", fileName));
            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }
            current = parent.FullName;
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    private sealed class DemoScenario
    {
        public List<DemoScenarioStep> Steps { get; set; } = new();
    }

    private sealed class DemoScenarioStep
    {
        public string? MatchContains { get; set; }
        public string Response { get; set; } = "";
    }
}