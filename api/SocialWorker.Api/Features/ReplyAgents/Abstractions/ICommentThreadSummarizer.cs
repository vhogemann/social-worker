using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.ReplyAgents.Models;

namespace SocialWorker.Api.Features.ReplyAgents.Abstractions;

public interface ICommentThreadSummarizer
{
    Task<string?> CompactThreadAsync(
        string? existingSummary,
        IReadOnlyList<PlatformCommentItem> unsummarizedComments,
        AppUser user,
        CancellationToken ct = default);
}
