using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure.Platforms;

namespace SocialWorker.Api.Features.Accounts.Tokens;

public class TokenRefreshHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TokenRefreshHostedService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);

    public TokenRefreshHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<TokenRefreshHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Short initial delay before first check
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRefreshTokensAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error occurred during background token refresh cycle.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task CheckAndRefreshTokensAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var refreshers = scope.ServiceProvider.GetServices<ITokenRefresher>().ToList();

        var threshold = DateTime.UtcNow.AddDays(7);
        var expiringAccounts = await db.Accounts
            .Where(a => a.Status == "Active" && a.TokenExpiresAt != null && a.TokenExpiresAt <= threshold)
            .ToListAsync(ct);

        if (expiringAccounts.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Found {Count} account(s) with tokens expiring within 7 days.", expiringAccounts.Count);

        foreach (var account in expiringAccounts)
        {
            if (ct.IsCancellationRequested) break;

            if (!SocialPlatformParser.TryParse(account.Platform, out var platform))
            {
                continue;
            }

            var refresher = refreshers.FirstOrDefault(r =>
                SocialPlatformParser.TryParse(r.Platform, out var rp) && rp == platform);

            if (refresher == null)
            {
                _logger.LogDebug("No token refresher found for platform {Platform}.", account.Platform);
                continue;
            }

            await refresher.RefreshAccountTokenAsync(account, ct);
        }
    }
}
