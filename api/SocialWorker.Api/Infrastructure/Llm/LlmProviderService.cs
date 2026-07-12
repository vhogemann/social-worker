using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Infrastructure.Llm;

public sealed class LlmProviderService
{
    public async Task<LlmProvider?> GetProviderForUserAsync(AppDbContext db, AppUser user, CancellationToken ct = default)
    {
        LlmProvider? provider = null;
        if (user.PreferredProviderId.HasValue)
        {
            provider = await db.LlmProviders.FirstOrDefaultAsync(p => p.Id == user.PreferredProviderId.Value && p.IsActive, ct);
        }

        provider ??= await db.LlmProviders.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, ct);
        return provider;
    }
}