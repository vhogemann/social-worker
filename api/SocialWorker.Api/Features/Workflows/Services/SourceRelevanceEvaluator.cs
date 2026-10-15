using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Adapters;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.Workflows.Services;

public sealed record SourceRelevanceResult(
    int Score,
    bool ShouldPublish,
    string Reasoning,
    string? Category)
{
    public static SourceRelevanceResult Pass(string reason = "Passed relevance threshold.", string category = "Relevant") =>
        new(10, true, reason, category);

    public static SourceRelevanceResult Discard(int score, string reason, string category = "LowImpact") =>
        new(score, false, reason, category);
}

public interface ISourceRelevanceEvaluator
{
    Task<SourceRelevanceResult> EvaluateAsync(
        Draft draft,
        AutomationWorkflow workflow,
        CancellationToken ct = default);
}

public sealed class SourceRelevanceEvaluator : ISourceRelevanceEvaluator
{
    private static readonly Regex JsonExtractor = new(@"\{[\s\S]*\}", RegexOptions.Compiled);

    private readonly AppDbContext _db;
    private readonly LlmProviderService _providerService;
    private readonly ILlmProviderAdapter _adapter;
    private readonly ILogger<SourceRelevanceEvaluator> _logger;

    public SourceRelevanceEvaluator(
        AppDbContext db,
        LlmProviderService providerService,
        ILlmProviderAdapter adapter,
        ILogger<SourceRelevanceEvaluator> logger)
    {
        _db = db;
        _providerService = providerService;
        _adapter = adapter;
        _logger = logger;
    }

    public async Task<SourceRelevanceResult> EvaluateAsync(
        Draft draft,
        AutomationWorkflow workflow,
        CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == workflow.UserId, ct);
        if (user == null)
        {
            return SourceRelevanceResult.Pass("No associated workflow user found; passing by default.");
        }

        var provider = await _providerService.GetProviderForUserAsync(_db, user, ct);
        if (provider == null)
        {
            return SourceRelevanceResult.Pass("No configured LLM provider found; passing by default.");
        }

        var sourceTexts = new List<string>();
        var draftSources = await _db.DraftSources
            .Include(ds => ds.Source)
            .Where(ds => ds.DraftId == draft.Id)
            .ToListAsync(ct);

        foreach (var ds in draftSources)
        {
            var s = ds.Source;
            if (s == null) continue;

            var title = s.Title ?? "Untitled";
            var summary = s.Summary;
            var content = s.Content;
            var snippet = !string.IsNullOrWhiteSpace(summary)
                ? summary
                : (!string.IsNullOrWhiteSpace(content) ? (content.Length > 1500 ? content[..1500] : content) : s.Reference);

            sourceTexts.Add($"[Source: {title} ({s.Kind})]\n{snippet}");
        }

        if (sourceTexts.Count == 0 && !string.IsNullOrWhiteSpace(draft.Content))
        {
            sourceTexts.Add(draft.Content.Length > 1500 ? draft.Content[..1500] : draft.Content);
        }

        var contextSnippet = sourceTexts.Count > 0 ? string.Join("\n\n---\n\n", sourceTexts) : draft.Title;

        var systemPrompt = """
You are an editorial triage and impact gatekeeper for an automated social media publishing system.
Your job is to evaluate whether a news item, article, or video is relevant, impactful, and sufficiently engaging to justify writing and publishing a social thread.

Evaluate the content strictly based on:
1. Public interest & virality: Is this breaking, urgent, provocative, or substantively significant?
2. Alignment with the workflow's mission and theme.
3. Waste avoidance: Trivial administrative announcements, routine filler, clickbait without substance, or off-topic items should be assigned low scores (1-5).
4. Major political exposés, economic consequences for working people, hypocrisies, and compelling narratives should be assigned high scores (7-10).

Output ONLY valid JSON matching this schema:
{
  "score": <integer from 1 to 10>,
  "shouldPublish": <boolean: true if score >= 7, false if < 7>,
  "reasoning": "<concise explanation in Portuguese (1-2 sentences)>",
  "category": "<HighImpact|SolidNews|RoutineFiller|LowImpact|OffTopic>"
}
""";

        var userPrompt = $"""
[Workflow: {workflow.Title}]
Target Threshold: {workflow.MinRelevanceScore}/10

[Workflow Guidelines]:
{workflow.InstructionPrompt}

[Candidate Item Content]:
Title: {draft.Title}
{contextSnippet}

Evaluate relevance and output JSON:
""";

        var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);

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
            var response = res?.Choices?.FirstOrDefault()?.Message?.Content?.ToString();
            if (string.IsNullOrWhiteSpace(response))
            {
                return SourceRelevanceResult.Pass("Empty LLM response; passing by default.");
            }

            var match = JsonExtractor.Match(response);
            if (!match.Success)
            {
                return SourceRelevanceResult.Pass("Unable to parse JSON from LLM evaluation; passing by default.");
            }

            using var doc = JsonDocument.Parse(match.Value);
            var root = doc.RootElement;

            var score = root.TryGetProperty("score", out var scoreProp) && scoreProp.TryGetInt32(out var s) ? Math.Clamp(s, 1, 10) : 7;
            var reasoning = root.TryGetProperty("reasoning", out var rProp) ? rProp.GetString() ?? "" : "";
            var category = root.TryGetProperty("category", out var cProp) ? cProp.GetString() : "General";
            var shouldPublish = workflow.MinRelevanceScore <= 0
                ? true
                : (root.TryGetProperty("shouldPublish", out var spProp) && spProp.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? spProp.GetBoolean()
                    : score >= workflow.MinRelevanceScore);

            if (workflow.MinRelevanceScore > 0 && (score < workflow.MinRelevanceScore || !shouldPublish))
            {
                return SourceRelevanceResult.Discard(score, string.IsNullOrWhiteSpace(reasoning) ? "Below relevance threshold." : reasoning, category ?? "LowImpact");
            }

            return new SourceRelevanceResult(score, true, reasoning, category);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Source relevance evaluation failed for draft {DraftId}. Passing by default.", draft.Id);
            return SourceRelevanceResult.Pass($"Evaluation error: {ex.Message}");
        }
    }
}
