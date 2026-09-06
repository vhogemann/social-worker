using System;

namespace SocialWorker.Api.Features.Scheduling.Models;

public sealed record ScheduledPublishingPayload(
    Guid QueueItemId,
    Guid ScheduleId,
    Guid DraftId,
    Guid PlatformThreadId
);
