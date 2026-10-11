using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.ReplyAgents.Services;

public sealed class CommentRankingService : ICommentRankingService
{
    private static readonly Regex JsonExtractor = new(@"\{[\s\S]*\}", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly LlmProviderService _providerService;
    private readonly ILlmProviderAdapter _adapter;
    private readonly ILogger<CommentRankingService> _logger;

    public CommentRankingService(
        AppDbContext db,
        LlmProviderService providerService,
        ILlmProviderAdapter adapter,
        ILogger<CommentRankingService> logger)
    {
        _db = db;
        _providerService = providerService;
        _adapter = adapter;
        _logger = logger;
    }

    public async Task<CommentRankingResult> RankCommentAsync(
        EnrichedCommentContext enrichedContext,
        string originalPostContent,
        string? threadSummary,
        AppUser user,
        CancellationToken ct = default)
    {
        var provider = await _providerService.GetProviderForUserAsync(_db, user, ct);
        if (provider == null)
        {
            return CommentRankingResult.DefaultSkip("No configured LLM provider found for user.");
        }

        var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);

        var systemPrompt = """
You are a social media comment triage and ranking evaluator.
Your job is to evaluate whether a public comment/reply to a post is worth responding to.

Consider all available context:
- The comment text, and any attached images (screenshots, code errors) or linked articles.
- Intent categories:
  * GenuineQuestion: User is sincerely asking for information, clarification, or help. (High reply worthiness)
  * InsightfulFeedback: Thoughtful feedback, constructive critique, or interesting debate. (High reply worthiness)
  * CasualPraise: Simple compliment (e.g. "Nice post!"). (Moderate reply worthiness, score 5-6)
  * TerminalClosure: Conversation-closing acknowledgement (e.g. "Thanks!", "Got it", "Makes sense"). Flag isTerminal=true, shouldReply=false.
  * LowEffort: Meaningless gibberish, one-word reaction, emoji-only. (Low reply worthiness, score 1-4)
  * HostileTroll: Personal attacks, bad-faith bait, hostility, harassment. (Do NOT reply, score 1)
  * Spam: Advertisements, unsolicited self-promotion, phishing. (Do NOT reply, score 1)

Output ONLY valid JSON matching this schema:
{
  "score": <integer from 1 to 10>,
  "category": "<GenuineQuestion|InsightfulFeedback|CasualPraise|TerminalClosure|LowEffort|HostileTroll|Spam>",
  "shouldReply": <boolean: true only if score >= 7 and not terminal/troll/spam>,
  "isTerminal": <boolean: true if conversation reached natural closure>,
  "reasoning": "<1-2 sentence concise explanation>"
}
""";

        var userPrompt = $"""
[Original Post]:
{originalPostContent}

{(string.IsNullOrWhiteSpace(threadSummary) ? "" : $"[Prior Conversation Summary]:\n{threadSummary}\n\n")}[New Comment & Media]:
{enrichedContext.FormattedContextForLlm}

Evaluate this comment.
""";

        try
        {
            var req = new OpenAiModels.ChatCompletionRequest
            {
                Model = credentials.Model,
                Messages = new List<OpenAiModels.OpenAiMessage>
                {
                    new() { Role = "system", Content = systemPrompt },
                    new() { Role = "user", Content = userPrompt }
                },
                Stream = false
            };

            var res = await _adapter.CompleteAsync(req, credentials, ct);
            var content = res?.Choices?.FirstOrDefault()?.Message?.Content?.ToString();
            if (string.IsNullOrWhiteSpace(content))
            {
                return CommentRankingResult.DefaultSkip("LLM returned empty ranking response.");
            }

            var match = JsonExtractor.Match(content);
            if (match.Success)
            {
                var parsed = JsonSerializer.Deserialize<CommentRankingResult>(match.Value, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (parsed != null)
                {
                    return parsed;
                }
            }

            return CommentRankingResult.DefaultSkip("Failed to parse JSON ranking result.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rank comment via LLM.");
            return CommentRankingResult.DefaultSkip($"Ranking error: {ex.Message}");
        }
    }
}
