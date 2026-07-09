using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Features.Chat;

public sealed class ChatSessionLoader
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModelCapabilityProbe _probe;
    private readonly DraftTitleGenerator _titleGenerator;

    public ChatSessionLoader(
        IServiceScopeFactory scopeFactory,
        ModelCapabilityProbe probe,
        DraftTitleGenerator titleGenerator)
    {
        _scopeFactory = scopeFactory;
        _probe = probe;
        _titleGenerator = titleGenerator;
    }

    public async Task<ChatSessionContext> LoadAsync(
        Guid userId,
        Guid? draftId,
        string? editorContentRequest,
        List<ChatModels.UiMessage> messages,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct)
            ?? throw new InvalidOperationException("User not found or inactive.");

        LlmProvider? provider = null;
        if (user.PreferredProviderId.HasValue)
        {
            provider = await db.LlmProviders.FirstOrDefaultAsync(p => p.Id == user.PreferredProviderId.Value && p.IsActive, ct);
        }

        if (provider == null)
        {
            provider = await db.LlmProviders.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, ct);
        }

        if (provider == null)
        {
            throw new InvalidOperationException("No active LLM provider found.");
        }

        var credentials = new LlmCredentials(provider.BaseUrl, provider.ApiKey, provider.Model);
        var capabilities = await _probe.GetCapabilitiesAsync(provider);

        Draft draft;
        if (draftId.HasValue)
        {
            draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId.Value && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
                ?? throw new InvalidOperationException($"Draft {draftId.Value} not found or access denied");
        }
        else if (!string.IsNullOrEmpty(editorContentRequest))
        {
            draft = new Draft { Content = editorContentRequest, UserId = userId };
            db.Drafts.Add(draft);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            draft = new Draft { Content = "", UserId = userId };
            db.Drafts.Add(draft);
            await db.SaveChangesAsync(ct);
        }

        var editorContent = draft.Content ?? "";

        if (!string.IsNullOrEmpty(editorContentRequest) && editorContentRequest != editorContent)
        {
            draft.Content = editorContentRequest;
            draft.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            editorContent = editorContentRequest;
        }

        var mediaAssets = await db.MediaAssets.Where(m => m.DraftId == draft.Id).ToListAsync(ct);

        if (draft.Title == "Untitled" && messages.Count > 0)
        {
            await _titleGenerator.TryGenerateDraftTitleAsync(db, draft, messages, credentials, ct);
        }

        return new ChatSessionContext(
            provider,
            credentials,
            capabilities,
            draft,
            editorContent,
            mediaAssets);
    }
}
