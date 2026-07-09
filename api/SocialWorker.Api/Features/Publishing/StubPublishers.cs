using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Publishing;

public class TwitterPublisher : IPublisher
{
    public string Platform => "Twitter";

    public Task<PublishResult> PublishAsync(PlatformThread thread, CancellationToken ct = default)
    {
        return Task.FromResult(new PublishResult
        {
            Success = false,
            ErrorMessage = "Not Implemented",
            AuthUrl = "https://twitter.com/i/oauth2/authorize?placeholder"
        });
    }
}

public class LinkedInPublisher : IPublisher
{
    public string Platform => "LinkedIn";

    public Task<PublishResult> PublishAsync(PlatformThread thread, CancellationToken ct = default)
    {
        return Task.FromResult(new PublishResult
        {
            Success = false,
            ErrorMessage = "Not Implemented",
            AuthUrl = "https://www.linkedin.com/oauth/v2/authorization?placeholder"
        });
    }
}

public class FacebookPublisher : IPublisher
{
    public string Platform => "Facebook";

    public Task<PublishResult> PublishAsync(PlatformThread thread, CancellationToken ct = default)
    {
        return Task.FromResult(new PublishResult
        {
            Success = false,
            ErrorMessage = "Not Implemented",
            AuthUrl = "https://www.facebook.com/v17.0/dialog/oauth?placeholder"
        });
    }
}

public class InstagramPublisher : IPublisher
{
    public string Platform => "Instagram";

    public Task<PublishResult> PublishAsync(PlatformThread thread, CancellationToken ct = default)
    {
        return Task.FromResult(new PublishResult
        {
            Success = false,
            ErrorMessage = "Not Implemented",
            AuthUrl = "https://api.instagram.com/oauth/authorize?placeholder"
        });
    }
}
