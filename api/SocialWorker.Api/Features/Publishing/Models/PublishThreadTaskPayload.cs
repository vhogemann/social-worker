using System;

namespace SocialWorker.Api.Features.Publishing.Models;

public sealed record PublishThreadTaskPayload(
    Guid DraftId,
    Guid ThreadId,
    Guid UserId,
    string Platform,
    Guid? AccountId = null
);
