using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Chat.Tools;

namespace SocialWorker.Api.Features.Chat.Services;

public sealed record ChatRunPreparation(
    ChatSessionContext Session,
    OpenAiModels.ChatCompletionRequest Payload,
    string CommandText,
    int HistoryBudget,
    int SelectedMessageCount,
    bool RequiresEditorUpdate,
    bool RequiresAllSourcesFetchedBeforeReplace,
    bool RequiresImageImportBeforeReplace,
    bool RequiresImageInspectionBeforeReplace);

public sealed class ChatRequestPreparationService
{
    private static readonly Regex EditorUpdateIntentRegex = new(
        @"\b(write|rewrite|re-write|edit|update|improve|polish|refine|fix|revise|shorten|expand|draft|compose|create|generate|rework|apply\s+(these|the)\s+changes)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AllSourcesIntentRegex = new(
        @"\b(all|every|each)\b.{0,48}\bsources?\b|\bsources?\b.{0,48}\b(all|every|each)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ImageIntentRegex = new(
        @"\b(image|images|photo|photos|picture|pictures|illustrat(e|ion|ive))\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ImageInspectionIntentRegex = new(
        @"\b(inspect|inspection|analy[sz]e|review|look\s+at|view)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ChatSessionLoader _sessionLoader;
    private readonly SystemPromptBuilder _promptBuilder;
    private readonly IEnumerable<IChatTool> _tools;
    private readonly ChatOptions _chatOptions;

    public ChatRequestPreparationService(
        ChatSessionLoader sessionLoader,
        SystemPromptBuilder promptBuilder,
        IEnumerable<IChatTool> tools,
        IOptions<ChatOptions> chatOptions)
    {
        _sessionLoader = sessionLoader;
        _promptBuilder = promptBuilder;
        _tools = tools;
        _chatOptions = chatOptions.Value;
    }

    public async Task<ChatRunPreparation> PrepareAsync(
        ChatModels.ChatRequest req,
        Guid userId,
        CancellationToken ct)
    {
        var session = await _sessionLoader.LoadAsync(userId, req.DraftId, req.Editor, req.Messages, ct);
        var systemPrompt = _promptBuilder.Build(
            req.System,
            session.EditorContent,
            session.MediaAssets,
            session.Capabilities.SupportsVision,
            session.DefaultBrandVoiceBody);

        var finalSystemPrompt = systemPrompt;
        if (!string.IsNullOrEmpty(session.Draft.ChatSummary))
        {
            finalSystemPrompt += $"\n\nContext summary of the conversation so far:\n{session.Draft.ChatSummary}";
        }

        var visibleTools = _tools
            .Where(t => !t.RequiresVision || session.Capabilities.SupportsVision)
            .Select(t => new OpenAiModels.OpenAiTool
            {
                Function = new()
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = t.Parameters,
                }
            })
            .ToList();

        var commandText = ExtractLatestUserText(req.Messages);
        var historyBudget = ChatContextWindowPolicy.CalculateHistoryBudgetTokens(
            session.Provider.ContextWindowTokens,
            session.Credentials.Model,
            finalSystemPrompt,
            visibleTools);
        var messagesToSend = ChatContextWindowPolicy.SelectRecentMessages(req.Messages, historyBudget);

        var convo = new List<OpenAiModels.OpenAiMessage>
        {
            new() { Role = "system", Content = finalSystemPrompt },
        };

        foreach (var message in messagesToSend)
        {
            var text = string.Join("\n", message.Content.Where(p => p.Type == "text").Select(p => p.Text ?? ""));
            convo.Add(new OpenAiModels.OpenAiMessage { Role = message.Role, Content = text });
        }

        var payload = new OpenAiModels.ChatCompletionRequest
        {
            Model = session.Credentials.Model,
            Messages = convo,
            Stream = true,
            Tools = visibleTools,
        };

        var requiresImageImportBeforeReplace = RequiresImageAttachmentFlow(commandText);

        return new ChatRunPreparation(
            session,
            payload,
            commandText,
            historyBudget,
            messagesToSend.Count,
            _chatOptions.StrictEditorUpdateEnforcement && RequiresEditorUpdateTool(commandText),
            RequiresAllSourcesFetch(commandText),
            requiresImageImportBeforeReplace,
            requiresImageImportBeforeReplace && RequiresImageInspection(commandText));
    }

    private static bool RequiresEditorUpdateTool(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.StartsWith('/'))
        {
            return false;
        }

        return EditorUpdateIntentRegex.IsMatch(text);
    }

    private static bool RequiresAllSourcesFetch(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return AllSourcesIntentRegex.IsMatch(text) &&
               text.Contains("fetch_source", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresImageAttachmentFlow(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ImageIntentRegex.IsMatch(text) &&
               (text.Contains("embed", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("attach", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("illustr", StringComparison.OrdinalIgnoreCase));
    }

    private static bool RequiresImageInspection(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ImageInspectionIntentRegex.IsMatch(text);
    }

    private static string ExtractLatestUserText(List<ChatModels.UiMessage> messages)
    {
        var latestUser = messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
        if (latestUser == null)
        {
            return string.Empty;
        }

        return string.Join("\n", latestUser.Content.Where(p => p.Type == "text").Select(p => p.Text ?? string.Empty)).Trim();
    }
}