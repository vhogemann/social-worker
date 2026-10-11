using System;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Sources.Instagram;

public sealed record InstagramCredentials(string AccessToken, string CallerIgUserId);

public interface IInstagramCredentialProvider
{
    Task<InstagramCredentials?> GetCredentialsAsync(Guid? userId = null, CancellationToken ct = default);
}
