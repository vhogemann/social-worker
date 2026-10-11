using System;

namespace SocialWorker.Api.Features.ReplyAgents.Models;

public sealed record WorkflowReplyCheckPostPayload(
    Guid WorkflowReplyAgentId,
    Guid PostId
);

public sealed record ReplyThreadSummaryPayload(
    Guid ThreadId,
    Guid UserId
);
