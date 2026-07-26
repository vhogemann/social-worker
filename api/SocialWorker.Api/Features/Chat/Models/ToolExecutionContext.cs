using System;
using System.Threading;

namespace SocialWorker.Api.Features.Chat.Models;

public sealed record ToolExecutionContext(
    Guid? DraftId,
    Guid UserId,
    CancellationToken CancellationToken = default)
{
    public static ToolExecutionContext Create(Guid userId, Guid? draftId = null, CancellationToken cancellationToken = default)
        => new(draftId, userId, cancellationToken);
}
