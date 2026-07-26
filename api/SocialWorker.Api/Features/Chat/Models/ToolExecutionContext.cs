using System;
using System.Threading;

namespace SocialWorker.Api.Features.Chat.Models;

public sealed record ToolExecutionContext(
    Guid? DraftId,
    Guid UserId,
    CancellationToken CancellationToken = default);
