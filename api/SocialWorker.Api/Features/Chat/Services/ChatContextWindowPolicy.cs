using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SocialWorker.Api.Features.Chat.Services;

internal static class ChatContextWindowPolicy
{
    private static readonly Regex KTokenRegex = new(@"(?<!\d)(\d{1,4})k(?!\d)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static int ResolveContextWindowTokens(string model)
    {
        if (string.IsNullOrWhiteSpace(model)) return 8192;

        var lowered = model.ToLowerInvariant();
        var match = KTokenRegex.Match(lowered);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var thousands))
        {
            return thousands * 1024;
        }

        if (lowered.Contains("claude-4") || lowered.Contains("claude-3.7") || lowered.Contains("claude-3-7") || lowered.Contains("claude-3.5") || lowered.Contains("claude-3-5"))
            return 200 * 1024;
        if (lowered.Contains("gpt-5") || lowered.Contains("gpt-4.1") || lowered.Contains("gpt-4o") || lowered.StartsWith("o1") || lowered.StartsWith("o3") || lowered.StartsWith("o4"))
            return 128 * 1024;
        if (lowered.Contains("gemini"))
            return 128 * 1024;
        if (lowered.Contains("llama") || lowered.Contains("mistral") || lowered.Contains("gemma"))
            return 8 * 1024;

        return 8 * 1024;
    }

    public static int CalculateHistoryBudgetTokens(int? configuredContextWindowTokens, string model, string systemPrompt, object? tools)
    {
        var contextWindow = configuredContextWindowTokens.GetValueOrDefault() > 0
            ? configuredContextWindowTokens!.Value
            : ResolveContextWindowTokens(model);
        var systemTokens = EstimateTextTokens(systemPrompt) + 32;
        var toolsTokens = tools is null ? 0 : EstimateSerializedTokens(tools) + 64;
        var responseReserve = Math.Clamp(contextWindow / 5, 1024, 16 * 1024);
        return Math.Max(512, contextWindow - systemTokens - toolsTokens - responseReserve);
    }

    public static List<ChatModels.UiMessage> SelectRecentMessages(List<ChatModels.UiMessage> messages, int tokenBudget)
    {
        if (messages.Count == 0) return messages;

        var selected = new List<ChatModels.UiMessage>();
        var used = 0;

        for (var i = messages.Count - 1; i >= 0; i--)
        {
            var msg = messages[i];
            var msgTokens = EstimateUiMessageTokens(msg);
            if (selected.Count > 0 && used + msgTokens > tokenBudget)
            {
                break;
            }

            selected.Add(msg);
            used += msgTokens;
        }

        selected.Reverse();
        return selected;
    }

    public static int EstimateUiMessageTokens(ChatModels.UiMessage message)
    {
        var text = string.Join("\n", message.Content.Where(p => p.Type == "text").Select(p => p.Text ?? ""));
        return EstimateTextTokens(text) + 12;
    }

    public static int EstimateStoredMessageTokens(JsonElement message)
    {
        var role = message.TryGetProperty("role", out var roleProp) ? roleProp.GetString() ?? "" : "";
        var contentText = "";
        if (message.TryGetProperty("content", out var contentProp))
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
                    if (part.TryGetProperty("text", out var textProp))
                    {
                        parts.Add(textProp.GetString() ?? "");
                    }
                }
                contentText = string.Join(" ", parts);
            }
            else
            {
                contentText = contentProp.ToString();
            }
        }

        return EstimateTextTokens(role) + EstimateTextTokens(contentText) + 12;
    }

    public static int EstimateTextTokens(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return Math.Max(1, (text.Length + 3) / 4);
    }

    public static int EstimateSerializedTokens(object value)
    {
        var json = JsonSerializer.Serialize(value);
        return EstimateTextTokens(json);
    }
}