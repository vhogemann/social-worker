using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.BackgroundTasks.Abstractions;
using SocialWorker.Api.Features.BackgroundTasks.Models;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.Drafts.Handlers;

public sealed record DraftChatSummaryPayload(Guid UserId, Guid DraftId, string ChatHistoryJson);

public sealed class DraftChatSummaryTaskHandler : BackgroundTaskHandlerBase<DraftChatSummaryPayload>
{
    private readonly AppDbContext _db;
    private readonly ILlmProviderAdapter _adapter;
    private readonly LlmProviderService _providerService;
    private readonly ILogger<DraftChatSummaryTaskHandler> _logger;

    public override string TaskType => "draft-chat-summary";
    public override TimeSpan DefaultLeaseTimeout => TimeSpan.FromMinutes(3);
    public override int DefaultMaxAttempts => 2;

    public DraftChatSummaryTaskHandler(
        AppDbContext db,
        ILlmProviderAdapter adapter,
        LlmProviderService providerService,
        ILogger<DraftChatSummaryTaskHandler> logger)
    {
        _db = db;
        _adapter = adapter;
        _providerService = providerService;
        _logger = logger;
    }

    public override async Task<TaskResult> ExecuteAsync(
        DraftChatSummaryPayload payload,
        ITaskExecutionContext context,
        CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == payload.UserId && u.IsActive, ct);
        if (user == null)
        {
            return TaskResult.Fatal($"Active user {payload.UserId} not found");
        }

        var provider = await _providerService.GetProviderForUserAsync(_db, user);
        if (provider == null)
        {
            return TaskResult.Fatal("No configured LLM provider found for user");
        }

        var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);

        using var doc = JsonDocument.Parse(payload.ChatHistoryJson);
        if (!doc.RootElement.TryGetProperty("messages", out var messagesArray) || messagesArray.ValueKind != JsonValueKind.Array)
        {
            return TaskResult.Success();
        }

        var totalCount = messagesArray.GetArrayLength();
        var totalTokens = messagesArray.EnumerateArray().Sum(ChatContextWindowPolicy.EstimateStoredMessageTokens);

        var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.Id == payload.DraftId, ct);
        if (draft == null)
        {
            return TaskResult.Fatal($"Draft {payload.DraftId} not found");
        }

        var contextWindow = provider.ContextWindowTokens.GetValueOrDefault() > 0
            ? provider.ContextWindowTokens!.Value
            : ChatContextWindowPolicy.ResolveContextWindowTokens(provider.Model);
        var targetTailTokenBudget = Math.Clamp(contextWindow / 4, 1200, 12 * 1024);
        var shouldCompact = totalCount >= 20 || totalTokens > targetTailTokenBudget * 2;
        if (!shouldCompact)
        {
            return TaskResult.Success();
        }

        _logger.LogInformation("Triggering compaction for draft {DraftId}. Total messages: {TotalCount}, estimated tokens: {TotalTokens}, target tail token budget: {TailBudget}",
            payload.DraftId, totalCount, totalTokens, targetTailTokenBudget);

        var tailMessages = new List<JsonElement>();
        var tailTokens = 0;
        for (var i = totalCount - 1; i >= 0; i--)
        {
            var message = messagesArray[i];
            var messageTokens = ChatContextWindowPolicy.EstimateStoredMessageTokens(message);
            if (tailMessages.Count > 0 && tailTokens + messageTokens > targetTailTokenBudget)
            {
                break;
            }

            tailMessages.Add(message);
            tailTokens += messageTokens;
        }

        tailMessages.Reverse();
        int messagesToSummarize = Math.Max(0, totalCount - tailMessages.Count);
        if (messagesToSummarize <= 0)
        {
            return TaskResult.Success();
        }

        var existingSummaryContext = "";
        if (!string.IsNullOrEmpty(draft.ChatSummary))
        {
            existingSummaryContext = $"Existing Summary of earlier conversation:\n{draft.ChatSummary}\n\n";
        }

        var sb = new StringBuilder();
        sb.AppendLine("Please read this conversation history and provide a concise, structured summary. Focus on key user instructions, preferences, decisions, and outcomes that the assistant should remember. Do not lose context. Keep the summary under 300 words.");
        sb.AppendLine(existingSummaryContext);
        sb.AppendLine("New Conversation History:");

        for (int i = 0; i < messagesToSummarize; i++)
        {
            var msg = messagesArray[i];
            var role = msg.TryGetProperty("role", out var r) ? r.GetString() : "unknown";

            var contentText = "";
            if (msg.TryGetProperty("content", out var contentProp))
            {
                if (contentProp.ValueKind == JsonValueKind.String)
                {
                    contentText = contentProp.GetString() ?? "";
                }
                else if (contentProp.ValueKind == JsonValueKind.Array)
                {
                    var parts = new List<string>();
                    foreach (var part in contentProp.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var t))
                        {
                            parts.Add(t.GetString() ?? "");
                        }
                    }
                    contentText = string.Join(" ", parts);
                }
            }

            sb.AppendLine($"{role}: {contentText}");
        }

        var summarizeRequest = new OpenAiModels.ChatCompletionRequest
        {
            Model = credentials.Model,
            Messages = new List<OpenAiModels.OpenAiMessage>
            {
                new() { Role = "system", Content = "You are a helpful assistant that summarizes chat conversations to keep context tight." },
                new() { Role = "user", Content = sb.ToString() }
            },
            Stream = false
        };

        var response = await _adapter.CompleteAsync(summarizeRequest, credentials, ct);
        if (response?.Choices != null && response.Choices.Count > 0)
        {
            var newSummary = response.Choices[0].Message.Content?.ToString();
            if (!string.IsNullOrEmpty(newSummary))
            {
                var historyNode = JsonNode.Parse(payload.ChatHistoryJson)?.AsObject();
                var messageNodes = new JsonArray();
                foreach (var message in tailMessages)
                {
                    var cloned = JsonNode.Parse(message.GetRawText());
                    if (cloned != null)
                    {
                        messageNodes.Add(cloned);
                    }
                }

                if (historyNode != null)
                {
                    historyNode["messages"] = messageNodes;
                    draft.ChatHistory = historyNode.ToJsonString();
                }

                draft.ChatSummary = newSummary;
                draft.LastSummarizedMessageCount = 0;
                draft.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Compaction completed for draft {DraftId}. Summary covers {SummarizedCount} messages, raw history trimmed to {TailCount} messages.",
                    payload.DraftId, messagesToSummarize, tailMessages.Count);
            }
        }

        return TaskResult.Success();
    }
}
