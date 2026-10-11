using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ReplyAgents.Models;

namespace SocialWorker.Api.Features.ReplyAgents.Abstractions;

public interface IReplyPlatformAdapter
{
    string Platform { get; }

    Task<IReadOnlyList<PlatformCommentItem>> FetchCommentsAsync(
        Post post,
        Account account,
        DateTime since,
        CancellationToken ct = default);

    Task<PublishReplyResult> PublishReplyAsync(
        Post post,
        Account account,
        PlatformCommentItem targetComment,
        string replyText,
        CancellationToken ct = default);
}
