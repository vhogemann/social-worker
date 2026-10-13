using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Chat.Services;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Infrastructure.Llm;
using SocialWorker.Api.Infrastructure.Search;

namespace SocialWorker.Api.Features.ReplyAgents.Services;

public sealed class ReplyGenerationService : IReplyGenerationService
{
    private static readonly HashSet<string> ExcludedToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "replace_editor_content",
        "publish_platform",
        "format_validate_platform_content",
        "generate_platform_variants",
        "propose_stage_transition",
        "set_bluesky_reply_target",
        "validate_draft"
    };

    private readonly AppDbContext _db;
    private readonly LlmProviderService _providerService;
    private readonly ILlmProviderAdapter _adapter;
    private readonly ILogger<ReplyGenerationService> _logger;
    private readonly ISearchEngine? _searchEngine;
    private readonly ChatToolExecutor? _toolExecutor;
    private readonly IEnumerable<IChatTool>? _tools;

    public ReplyGenerationService(
        AppDbContext db,
        LlmProviderService providerService,
        ILlmProviderAdapter adapter,
        ILogger<ReplyGenerationService> logger,
        ISearchEngine? searchEngine = null,
        ChatToolExecutor? toolExecutor = null,
        IEnumerable<IChatTool>? tools = null)
    {
        _db = db;
        _providerService = providerService;
        _adapter = adapter;
        _logger = logger;
        _searchEngine = searchEngine;
        _toolExecutor = toolExecutor;
        _tools = tools;
    }

    public async Task<GeneratedReplyResult> GenerateReplyAsync(
        EnrichedCommentContext enrichedContext,
        string originalPostContent,
        string personaPrompt,
        string platform,
        string? threadSummary,
        AppUser user,
        Guid? draftId = null,
        CancellationToken ct = default)
    {
        var provider = await _providerService.GetProviderForUserAsync(_db, user, ct);
        if (provider == null)
        {
            return GeneratedReplyResult.Failed("No configured LLM provider found for user.");
        }

        var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);

        var maxChars = string.Equals(platform, "MetaThreads", StringComparison.OrdinalIgnoreCase) ? 500 : 300;

        var availableTools = new List<OpenAiModels.OpenAiTool>();

        if (_tools != null && _toolExecutor != null)
        {
            foreach (var t in _tools.Where(t => !ExcludedToolNames.Contains(t.Name)))
            {
                availableTools.Add(new OpenAiModels.OpenAiTool
                {
                    Type = "function",
                    Function = new OpenAiModels.OpenAiFunction
                    {
                        Name = t.Name,
                        Description = t.Description,
                        Parameters = t.Parameters
                    }
                });
            }
        }
        else if (_searchEngine != null)
        {
            availableTools.Add(new OpenAiModels.OpenAiTool
            {
                Type = "function",
                Function = new OpenAiModels.OpenAiFunction
                {
                    Name = "web_search",
                    Description = "Search the live web for facts, evidence, data, citations, or up-to-date information to verify or defend an argument.",
                    Parameters = JsonDocument.Parse("""
                    {
                      "type": "object",
                      "properties": {
                        "query": {
                          "type": "string",
                          "description": "The search keywords or query."
                        }
                      },
                      "required": ["query"]
                    }
                    """).RootElement
                }
            });
        }

        var tools = availableTools.Count > 0 ? availableTools : null;

        var toolsInstruction = availableTools.Count > 0
            ? $"\n7. You have access to tools ({string.Join(", ", availableTools.Select(t => t.Function.Name))}). If the comment references external claims, videos, images, or links, or if you need to gather facts or inspect evidence before replying, invoke the appropriate tools."
            : "";

        var systemPrompt = $"""
You are an expert social media agent composing a public reply on {platform}.
Your persona and guidelines:
{personaPrompt}

Rules:
1. Keep the response strictly under {maxChars} characters (this is a hard platform limit).
2. Answer questions directly and provide value. If the comment references an image/screenshot or linked article, address it contextually.
3. Faithfully embody your assigned persona, tone, and goals. Sound human, never like an automated bot.
4. Do NOT use hashtags unless specifically relevant.
5. Do NOT use emojis.
6. Output ONLY the raw reply text. No quotes, no markdown headers, no preamble.{toolsInstruction}
""";

        var userPrompt = $"""
[Original Post]:
{originalPostContent}

{(string.IsNullOrWhiteSpace(threadSummary) ? "" : $"[Prior Conversation Summary]:\n{threadSummary}\n\n")}[Target Comment & Media]:
{enrichedContext.FormattedContextForLlm}

Write a reply to @{enrichedContext.Comment.RemoteAuthorHandle}:
""";

        var messages = new List<OpenAiModels.OpenAiMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userPrompt }
        };

        try
        {
            const int maxTurns = 5;
            for (var turn = 0; turn < maxTurns; turn++)
            {
                var isFinalTurn = turn == maxTurns - 1;
                var req = new OpenAiModels.ChatCompletionRequest
                {
                    Model = credentials.Model,
                    Messages = messages,
                    Tools = isFinalTurn ? null : tools,
                    Stream = false
                };

                var res = await _adapter.CompleteAsync(req, credentials, ct);
                var choiceMessage = res?.Choices?.FirstOrDefault()?.Message;
                if (choiceMessage == null)
                {
                    return GeneratedReplyResult.Failed("LLM generated an empty reply.");
                }

                if (choiceMessage.ToolCalls != null && choiceMessage.ToolCalls.Count > 0 && !isFinalTurn)
                {
                    messages.Add(new OpenAiModels.OpenAiMessage
                    {
                        Role = "assistant",
                        Content = choiceMessage.Content,
                        ToolCalls = choiceMessage.ToolCalls
                    });

                    foreach (var toolCall in choiceMessage.ToolCalls)
                    {
                        if (_toolExecutor != null && !ExcludedToolNames.Contains(toolCall.Function?.Name ?? ""))
                        {
                            var toolContext = ToolExecutionContext.Create(user.Id, draftId, ct);
                            var toolResult = await _toolExecutor.ExecuteAsync(toolCall.Function!.Name, toolCall.Function.Arguments ?? "{}", toolContext);
                            var toolMessages = toolResult.ToMessages(toolCall.Id);
                            messages.AddRange(toolMessages);
                        }
                        else if (string.Equals(toolCall.Function?.Name, "web_search", StringComparison.OrdinalIgnoreCase) && _searchEngine != null)
                        {
                            string query = "";
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(toolCall.Function?.Arguments))
                                {
                                    using var doc = JsonDocument.Parse(toolCall.Function.Arguments);
                                    if (doc.RootElement.TryGetProperty("query", out var qProp))
                                    {
                                        query = qProp.GetString() ?? "";
                                    }
                                }
                            }
                            catch (JsonException)
                            {
                            }

                            var searchResults = string.IsNullOrWhiteSpace(query)
                                ? new List<SearchResult>()
                                : await _searchEngine.SearchAsync(query, ct);

                            var formattedResults = searchResults.Count == 0
                                ? "No search results found."
                                : string.Join("\n\n", searchResults.Take(4).Select(r => $"Title: {r.Title}\nSnippet: {r.Snippet}\nURL: {r.Url}"));

                            messages.Add(new OpenAiModels.OpenAiMessage
                            {
                                Role = "tool",
                                ToolCallId = toolCall.Id,
                                Content = formattedResults
                            });
                        }
                    }

                    continue;
                }

                var reply = choiceMessage.Content?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(reply))
                {
                    return GeneratedReplyResult.Failed("LLM generated an empty reply.");
                }

                reply = reply.Trim('"', '\'');
                if (reply.Length > maxChars)
                {
                    reply = reply[..maxChars].TrimEnd();
                }

                return GeneratedReplyResult.Succeeded(reply);
            }

            return GeneratedReplyResult.Failed("Maximum tool call depth reached without generating a reply.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate reply via LLM.");
            return GeneratedReplyResult.Failed($"Generation error: {ex.Message}");
        }
    }
}
