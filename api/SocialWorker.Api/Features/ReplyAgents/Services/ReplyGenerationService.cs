using System;
using System.Collections.Generic;
using System.Linq;
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

public sealed class ReplyGenerationService : IReplyGenerationService
{
    private readonly AppDbContext _db;
    private readonly LlmProviderService _providerService;
    private readonly ILlmProviderAdapter _adapter;
    private readonly ILogger<ReplyGenerationService> _logger;

    public ReplyGenerationService(
        AppDbContext db,
        LlmProviderService providerService,
        ILlmProviderAdapter adapter,
        ILogger<ReplyGenerationService> logger)
    {
        _db = db;
        _providerService = providerService;
        _adapter = adapter;
        _logger = logger;
    }

    public async Task<GeneratedReplyResult> GenerateReplyAsync(
        EnrichedCommentContext enrichedContext,
        string originalPostContent,
        string personaPrompt,
        string platform,
        string? threadSummary,
        AppUser user,
        CancellationToken ct = default)
    {
        var provider = await _providerService.GetProviderForUserAsync(_db, user, ct);
        if (provider == null)
        {
            return GeneratedReplyResult.Failed("No configured LLM provider found for user.");
        }

        var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);

        var maxChars = string.Equals(platform, "MetaThreads", StringComparison.OrdinalIgnoreCase) ? 500 : 300;

        var systemPrompt = $"""
You are an expert social media agent composing a public reply on {platform}.
Your persona and guidelines:
{personaPrompt}

Rules:
1. Keep the response strictly under {maxChars} characters (this is a hard platform limit).
2. Answer questions directly and provide value. If the comment references an image/screenshot or linked article, address it contextually.
3. Be humble, polite, and authentic. Do NOT sound like an automated corporate bot.
4. Do NOT use hashtags unless specifically relevant.
5. Do NOT use emojis.
6. Output ONLY the raw reply text. No quotes, no markdown headers, no preamble.
""";

        var userPrompt = $"""
[Original Post]:
{originalPostContent}

{(string.IsNullOrWhiteSpace(threadSummary) ? "" : $"[Prior Conversation Summary]:\n{threadSummary}\n\n")}[Target Comment & Media]:
{enrichedContext.FormattedContextForLlm}

Write a reply to @{enrichedContext.Comment.RemoteAuthorHandle}:
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
            var reply = res?.Choices?.FirstOrDefault()?.Message?.Content?.ToString()?.Trim();
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate reply via LLM.");
            return GeneratedReplyResult.Failed($"Generation error: {ex.Message}");
        }
    }
}
