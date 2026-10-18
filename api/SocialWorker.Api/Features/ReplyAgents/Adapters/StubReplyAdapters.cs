using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ReplyAgents.Abstractions;
using SocialWorker.Api.Features.ReplyAgents.Models;

namespace SocialWorker.Api.Features.ReplyAgents.Adapters;


public sealed class LinkedInReplyAdapter : IReplyPlatformAdapter
{
    public string Platform => "LinkedIn";

    public Task<IReadOnlyList<PlatformCommentItem>> FetchCommentsAsync(
        Post post,
        Account account,
        DateTime since,
        CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<PlatformCommentItem>>(Array.Empty<PlatformCommentItem>());
    }

    public Task<PublishReplyResult> PublishReplyAsync(
        Post post,
        Account account,
        PlatformCommentItem targetComment,
        string replyText,
        CancellationToken ct = default)
    {
        return Task.FromResult(PublishReplyResult.Failed("LinkedIn comment replies are not yet implemented."));
    }

    public Task<LikeCommentResult> LikeCommentAsync(
        Post post,
        Account account,
        PlatformCommentItem targetComment,
        CancellationToken ct = default)
    {
        return Task.FromResult(LikeCommentResult.NotSupported("LinkedIn comment liking is not yet implemented."));
    }
}
