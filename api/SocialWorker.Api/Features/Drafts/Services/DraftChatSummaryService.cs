using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Infrastructure.Background;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.Drafts;

public sealed class DraftChatSummaryService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly BackgroundJobQueue? _queue;

    public DraftChatSummaryService(IServiceScopeFactory? scopeFactory, BackgroundJobQueue? queue)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
    }

    public void TriggerBackgroundSummarization(Guid userId, Guid draftId, string chatHistoryJson)
    {
        if (_scopeFactory == null || _queue == null)
        {
            return;
        }

        _queue.Enqueue(new BackgroundJobQueue.Job("chat-summary", async ct =>
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adapter = scope.ServiceProvider.GetRequiredService<ILlmProviderAdapter>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DraftsService>>();
            var providerService = scope.ServiceProvider.GetRequiredService<LlmProviderService>();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (user == null) return;

            var provider = await providerService.GetProviderForUserAsync(db, user);
            if (provider == null) return;

            var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);

            using var doc = JsonDocument.Parse(chatHistoryJson);
            if (!doc.RootElement.TryGetProperty("messages", out var messagesArray) || messagesArray.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var totalCount = messagesArray.GetArrayLength();
            var totalTokens = messagesArray.EnumerateArray().Sum(ChatContextWindowPolicy.EstimateStoredMessageTokens);

            var draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId);
            if (draft == null) return;

            var contextWindow = provider.ContextWindowTokens.GetValueOrDefault() > 0
                ? provider.ContextWindowTokens!.Value
                : ChatContextWindowPolicy.ResolveContextWindowTokens(provider.Model);
            var targetTailTokenBudget = Math.Clamp(contextWindow / 4, 1200, 12 * 1024);
            var shouldCompact = totalCount >= 20 || totalTokens > targetTailTokenBudget * 2;
            if (!shouldCompact)
            {
                return;
            }

            logger.LogInformation("Triggering compaction for draft {DraftId}. Total messages: {TotalCount}, estimated tokens: {TotalTokens}, target tail token budget: {TailBudget}", draftId, totalCount, totalTokens, targetTailTokenBudget);

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
            if (messagesToSummarize <= 0) return;

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

            var response = await adapter.CompleteAsync(summarizeRequest, credentials, ct);
            if (response?.Choices != null && response.Choices.Count > 0)
            {
                var newSummary = response.Choices[0].Message.Content?.ToString();
                if (!string.IsNullOrEmpty(newSummary))
                {
                    var historyNode = JsonNode.Parse(chatHistoryJson)?.AsObject();
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
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("Compaction completed for draft {DraftId}. Summary covers {SummarizedCount} messages, raw history trimmed to {TailCount} messages.", draftId, messagesToSummarize, tailMessages.Count);
                }
            }
        }));
    }
}
