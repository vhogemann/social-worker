using System;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.ReplyAgents.Abstractions;

public sealed record PostResolutionResult(
    bool Success,
    Post? Post,
    string? Error = null)
{
    public static PostResolutionResult Ok(Post post) => new(true, post, null);
    public static PostResolutionResult Fail(string error) => new(false, null, error);
}

public interface IExternalPostResolver
{
    Task<PostResolutionResult> ResolveOrCreatePostAsync(
        Guid userId,
        string postUrlOrId,
        string? platformHint = null,
        CancellationToken ct = default);
}
