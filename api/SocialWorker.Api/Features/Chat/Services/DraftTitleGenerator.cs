using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Chat;

public sealed class DraftTitleGenerator
{
    private readonly ILlmProviderAdapter _adapter;
    private readonly ILogger<DraftTitleGenerator> _log;

    public DraftTitleGenerator(ILlmProviderAdapter adapter, ILogger<DraftTitleGenerator> log)
    {
        _adapter = adapter;
        _log = log;
    }

    public async Task TryGenerateDraftTitleAsync(
        AppDbContext db,
        Draft draft,
        List<ChatModels.UiMessage> messages,
        LlmCredentials credentials,
        CancellationToken ct)
    {
        var firstMsg = messages.FirstOrDefault();
        if (firstMsg == null) return;
        var text = string.Join("\n", firstMsg.Content.Where(p => p.Type == "text").Select(p => p.Text ?? ""));
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            var summarizePayload = new OpenAiModels.ChatCompletionRequest
            {
                Model = credentials.Model,
                Messages = new()
                {
                    new()
                    {
                        Role = "system",
                        Content = "You are a helpful assistant. Summarize the user's first prompt into a clean 3-5 word title for their draft. Output ONLY the raw title without quotation marks, markdown formatting, or emojis."
                    },
                    new()
                    {
                        Role = "user",
                        Content = text
                    }
                },
                Stream = false
            };

            var response = await _adapter.CompleteAsync(summarizePayload, credentials, ct);
            var content = response?.Choices?.FirstOrDefault()?.Message?.Content;
            if (!string.IsNullOrWhiteSpace(content))
            {
                var cleanTitle = content.Trim('"', '\'', ' ', '\n', '\r');
                if (cleanTitle.Length > 100) cleanTitle = cleanTitle.Substring(0, 100);
                draft.Title = cleanTitle;
                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to auto-generate title for draft {Id}", draft.Id);
        }
    }
}
