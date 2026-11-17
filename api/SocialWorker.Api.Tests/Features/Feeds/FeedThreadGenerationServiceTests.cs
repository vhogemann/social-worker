using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Models;
using SocialWorker.Api.Features.Chat.Services;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Feeds;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Feeds;

public sealed class FeedThreadGenerationServiceTests : SqliteTestBase
{
    private sealed class FakeChatStreamService : ChatService
    {
        private readonly Func<ChatModels.ChatRequest, IAsyncEnumerable<string>> _streamFactory;

        public FakeChatStreamService(Func<ChatModels.ChatRequest, IAsyncEnumerable<string>> streamFactory)
            : base(null!, NullLogger<ChatService>.Instance, Microsoft.Extensions.Options.Options.Create(new ChatOptions()), null!, null!, null!, null!)
        {
            _streamFactory = streamFactory;
        }

        public override async IAsyncEnumerable<string> StreamAsync(
            ChatModels.ChatRequest req,
            Guid userId,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var item in _streamFactory(req).WithCancellation(ct))
            {
                yield return item;
            }
        }
    }

    [Fact]
    public async Task GenerateCanonicalThreadAsync_WhenLlmStreamsTextWithoutCallingReplaceEditorContent_PopulatesDraftContent()
    {
        using var db = CreateDbContext();
        var user = CreateSeedUser(db);
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Title = "Why NASA's X-59 has a long nose",
            Status = DraftStatus.Editing,
            Content = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Drafts.Add(draft);
        await db.SaveChangesAsync();

        const string generatedPostText = "Why does NASA’s X-59 have such a long nose? Real Engineering explores how its shape softens sonic booms.\n\n![X-59](https://youtube.com/watch?v=123)";

        var fakeChat = new FakeChatStreamService(req =>
        {
            async IAsyncEnumerable<string> Stream()
            {
                yield return "f:{\"messageId\":\"m_123\"}\n";
                // LLM validates in memory
                yield return "9:{\"toolCallId\":\"call_1\",\"toolName\":\"validate_draft\",\"args\":{\"content\":\"" + generatedPostText.Replace("\n", "\\n") + "\",\"platform\":\"Bluesky\"}}\n";
                // LLM streams final text directly without calling replace_editor_content
                yield return "0:\"" + generatedPostText.Replace("\n", "\\n") + "\"\n";
                yield return "e:{\"finishReason\":\"stop\"}\n";
            }
            return Stream();
        });

        var service = new FeedThreadGenerationService(
            db,
            fakeChat,
            NullLogger<FeedThreadGenerationService>.Instance,
            new DraftSegmentService(db));

        var success = await service.GenerateCanonicalThreadAsync(
            draft,
            "https://youtube.com/watch?v=123",
            "Write a concise post promoting Real Engineering.",
            user.Id,
            CancellationToken.None);

        Assert.True(success);

        var savedDraft = await db.Drafts.Include(d => d.Segments).FirstOrDefaultAsync(d => d.Id == draft.Id);
        Assert.NotNull(savedDraft);
        Assert.False(string.IsNullOrWhiteSpace(savedDraft.Content), "Draft Content should have been saved from the generated response.");
        Assert.Contains("NASA’s X-59", savedDraft.Content);
        Assert.NotEmpty(savedDraft.Segments);
    }
}
