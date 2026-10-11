using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

public sealed class CommentThreadSummarizer : ICommentThreadSummarizer
{
    private readonly AppDbContext _db;
    private readonly LlmProviderService _providerService;
    private readonly ILlmProviderAdapter _adapter;
    private readonly ILogger<CommentThreadSummarizer> _logger;

    public CommentThreadSummarizer(
        AppDbContext db,
        LlmProviderService providerService,
        ILlmProviderAdapter adapter,
        ILogger<CommentThreadSummarizer> logger)
    {
        _db = db;
        _providerService = providerService;
        _adapter = adapter;
        _logger = logger;
    }

    public async Task<string?> CompactThreadAsync(
        string? existingSummary,
        IReadOnlyList<PlatformCommentItem> unsummarizedComments,
        AppUser user,
        CancellationToken ct = default)
    {
        if (unsummarizedComments == null || unsummarizedComments.Count == 0)
        {
            return existingSummary;
        }

        var provider = await _providerService.GetProviderForUserAsync(_db, user, ct);
        if (provider == null) return existingSummary;

        var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(existingSummary))
        {
            sb.AppendLine("Prior Conversation Summary:");
            sb.AppendLine(existingSummary);
            sb.AppendLine();
        }

        sb.AppendLine("New Conversation Turns:");
        foreach (var c in unsummarizedComments)
        {
            sb.AppendLine($"@{c.RemoteAuthorHandle}: {c.Text}");
        }

        var systemPrompt = """
You are a conversation summarizer for public social media reply threads.
Provide a concise, factual summary of the discussion so far under 120 words.
Focus on questions asked, answers given, key issues raised, and resolutions.
Output ONLY the plain text summary without headers or formatting.
""";

        try
        {
            var req = new OpenAiModels.ChatCompletionRequest
            {
                Model = credentials.Model,
                Messages = new List<OpenAiModels.OpenAiMessage>
                {
                    new() { Role = "system", Content = systemPrompt },
                    new() { Role = "user", Content = sb.ToString() }
                },
                Stream = false
            };

            var res = await _adapter.CompleteAsync(req, credentials, ct);
            var summary = res?.Choices?.FirstOrDefault()?.Message?.Content?.ToString()?.Trim();
            return !string.IsNullOrWhiteSpace(summary) ? summary : existingSummary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compact comment thread via LLM.");
            return existingSummary;
        }
    }
}
