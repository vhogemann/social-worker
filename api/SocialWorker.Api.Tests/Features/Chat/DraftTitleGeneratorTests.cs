using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Infrastructure.Llm;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class DraftTitleGeneratorTests
{
    [Fact]
    public async Task TryGenerateDraftTitleAsync_DoesNotThrow_WhenNoMessages()
    {
        var adapter = new DemoLlmAdapter();
        var logger = NullLogger<DraftTitleGenerator>.Instance;
        var gen = new DraftTitleGenerator(adapter, logger);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "Untitled" };

        await gen.TryGenerateDraftTitleAsync(null!, draft, new(), null!, CancellationToken.None);

        Assert.Equal("Untitled", draft.Title);
    }

    [Fact]
    public async Task TryGenerateDraftTitleAsync_DoesNotThrow_WhenEmptyMessage()
    {
        var adapter = new DemoLlmAdapter();
        var logger = NullLogger<DraftTitleGenerator>.Instance;
        var gen = new DraftTitleGenerator(adapter, logger);
        var draft = new Draft { Id = Guid.NewGuid(), Title = "Untitled" };
        var msgs = new System.Collections.Generic.List<ChatModels.UiMessage>
        {
            new() { Content = new() { new() { Type = "text", Text = "   " } } }
        };

        await gen.TryGenerateDraftTitleAsync(null!, draft, msgs, null!, CancellationToken.None);

        Assert.Equal("Untitled", draft.Title);
    }
}