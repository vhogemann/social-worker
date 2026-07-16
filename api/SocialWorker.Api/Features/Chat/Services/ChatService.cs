using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SocialWorker.Api.Features.Chat.Services;

public sealed class ChatService
{
    private readonly ChatStreamWriter _writer;
    private readonly ILogger<ChatService> _log;
    private readonly ChatOptions _chatOptions;
    private readonly ChatSlashCommandService _slashCommandService;
    private readonly ChatRequestPreparationService _requestPreparationService;
    private readonly ChatToolExecutor _toolExecutor;
    private readonly ChatRoundProcessor _roundProcessor;

    public ChatService(
        ChatSessionLoader sessionLoader,
        SystemPromptBuilder promptBuilder,
        ChatStreamWriter writer,
        ILlmProviderAdapter adapter,
        ILogger<ChatService> log,
        IEnumerable<IChatTool> tools,
        IOptions<ChatOptions> chatOptions,
        ChatSlashCommandService? slashCommandService = null,
        ChatRequestPreparationService? requestPreparationService = null,
        ChatToolExecutor? toolExecutor = null,
        ChatRoundProcessor? roundProcessor = null)
    {
        _writer = writer;
        _log = log;
        _chatOptions = chatOptions.Value;
        _slashCommandService = slashCommandService ?? new ChatSlashCommandService();
        _requestPreparationService = requestPreparationService ?? new ChatRequestPreparationService(sessionLoader, promptBuilder, tools, chatOptions);
        _toolExecutor = toolExecutor ?? new ChatToolExecutor(tools, Microsoft.Extensions.Logging.Abstractions.NullLogger<ChatToolExecutor>.Instance);
        _roundProcessor = roundProcessor ?? new ChatRoundProcessor(writer, adapter, _toolExecutor, Microsoft.Extensions.Logging.Abstractions.NullLogger<ChatRoundProcessor>.Instance);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ChatModels.ChatRequest req,
        Guid userId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var preparation = await _requestPreparationService.PrepareAsync(req, userId, ct);
        var session = preparation.Session;
        var payload = preparation.Payload;
        var commandText = preparation.CommandText;

        _log.LogInformation(
            "Chat context selection for model {Model}: total messages={TotalMessages}, selected messages={SelectedMessages}, estimated history budget tokens={HistoryBudget}",
            session.Credentials.Model,
            req.Messages.Count,
            preparation.SelectedMessageCount,
            preparation.HistoryBudget);

        yield return _writer.MessageId(req.UnstableAssistantMessageId);

        if (_slashCommandService.TryParse(commandText, out var slashCommand, out var slashArgs))
        {
            if (string.Equals(slashCommand, "validate", StringComparison.OrdinalIgnoreCase))
            {
                var toolCallId = $"slash-{Guid.NewGuid():N}";
                const string toolName = "validate_draft";
                const string toolArgsJson = "{}";

                yield return _writer.ToolCall(toolCallId, toolName, toolArgsJson);
                var toolResult = await ExecuteToolAsync(toolName, toolArgsJson, req.DraftId, userId, ct);
                yield return _writer.ToolResult(toolCallId, toolResult.ToDisplayPayload());
                yield return _writer.TextDelta(toolResult.ToDisplayText());
                yield return _writer.StepFinish("stop", false);
                yield return _writer.StreamDone();
                yield break;
            }

            var slashOutput = await _slashCommandService.ExecuteAsync(slashCommand, slashArgs, req.DraftId, userId, ExecuteToolAsync, ct);
            yield return _writer.TextDelta(slashOutput);
            yield return _writer.StepFinish("stop", false);
            yield return _writer.StreamDone();
            yield break;
        }

        var requiresEditorUpdate = preparation.RequiresEditorUpdate;
        var requiresAllSourcesFetchedBeforeReplace = preparation.RequiresAllSourcesFetchedBeforeReplace;
        var requiresImageImportBeforeReplace = preparation.RequiresImageImportBeforeReplace;
        var requiresImageInspectionBeforeReplace = preparation.RequiresImageInspectionBeforeReplace;
        var editorEnforcementRetried = false;
        var sawReplaceEditorToolCallOverall = false;
        var sawAddImageSourceOverall = false;
        var sawViewImageOverall = false;
        var listedSourceIds = new HashSet<Guid>();
        var fetchedSourceIds = new HashSet<Guid>();
        var hasStableValidatedDraft = false;
        var finalizationNudgeInjected = false;

        var maxRounds = Math.Clamp(_chatOptions.MaxToolExecutionRounds, 1, 16);
        for (var round = 0; round < maxRounds; round++)
        {
            var roundCtx = new ChatRoundContext(
                round,
                payload,
                session,
                req.DraftId,
                userId,
                ct,
                requiresAllSourcesFetchedBeforeReplace,
                listedSourceIds,
                fetchedSourceIds,
                requiresImageImportBeforeReplace,
                requiresImageInspectionBeforeReplace,
                sawAddImageSourceOverall,
                sawViewImageOverall);
            await foreach (var line in _roundProcessor.ProcessAsync(roundCtx))
            {
                yield return line;
            }

            sawReplaceEditorToolCallOverall = sawReplaceEditorToolCallOverall || roundCtx.SawReplaceEditorToolCall;
            sawAddImageSourceOverall = roundCtx.SawAddImageSource;
            sawViewImageOverall = roundCtx.SawViewImage;

            if (roundCtx.SawValidateDraftToolCall && !roundCtx.LastValidateHadBlockingErrors && sawReplaceEditorToolCallOverall)
            {
                hasStableValidatedDraft = true;
            }

            if (hasStableValidatedDraft && !finalizationNudgeInjected)
            {
                finalizationNudgeInjected = true;
                payload.Messages.Add(new OpenAiModels.OpenAiMessage
                {
                    Role = "system",
                    Content = "FINALIZATION: The draft is already updated and validated with no blocking errors. Do not call replace_editor_content or validate_draft again unless the user explicitly asks for another revision. Provide a concise final response to the user now."
                });
                continue;
            }

            if (hasStableValidatedDraft &&
                finalizationNudgeInjected &&
                roundCtx.CalledToolNames.Any(name =>
                    string.Equals(name, "replace_editor_content", StringComparison.Ordinal) ||
                    string.Equals(name, "validate_draft", StringComparison.Ordinal)))
            {
                yield return _writer.TextDelta("Draft updated and validated in the editor.");
                yield return _writer.StepFinish("stop", false);
                break;
            }

            if (roundCtx.ShouldStop &&
                requiresEditorUpdate &&
                !sawReplaceEditorToolCallOverall &&
                !editorEnforcementRetried)
            {
                editorEnforcementRetried = true;
                payload.Messages.Add(new OpenAiModels.OpenAiMessage
                {
                    Role = "system",
                    Content = ChatPromptCatalog.Current.Chat.EditorUpdateEnforcement
                });
                continue;
            }

            if (roundCtx.ShouldStop)
            {
                break;
            }
        }

        yield return _writer.StreamDone();
    }

    private async Task<ToolExecutionResult> ExecuteToolAsync(
        string name,
        string argumentsJson,
        Guid? draftId,
        Guid userId,
        CancellationToken ct)
    {
        return await _toolExecutor.ExecuteAsync(name, argumentsJson, draftId, userId, ct);
    }

}