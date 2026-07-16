using DbUp;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Accounts;
using SocialWorker.Api.Features.Auth;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.CodeImages;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.PlatformVariants;
using SocialWorker.Api.Features.Prompts;
using SocialWorker.Api.Features.Providers;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Features.Users;
using SocialWorker.Api.Infrastructure.Auth;
using SocialWorker.Api.Infrastructure.Llm;

namespace SocialWorker.Api.Infrastructure.Extensions;

static class AppStartupExtensions
{
    internal static async Task SeedDatabaseAsync(this WebApplication app)
    {
        var connectionString = app.Configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Default' is not configured.");
        }

        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
        if (!Directory.Exists(migrationsPath))
        {
            migrationsPath = Path.Combine(app.Environment.ContentRootPath, "Data", "Migrations");
        }

        var upgrader = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsFromFileSystem(migrationsPath)
            .LogToConsole()
            .Build();

        var migrationResult = upgrader.PerformUpgrade();
        if (!migrationResult.Successful)
        {
            throw new Exception($"Database migration failed: {migrationResult.Error}", migrationResult.Error);
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await SeedAdminUserAsync(scope.ServiceProvider, db);
        await SeedDefaultLlmProviderAsync(scope.ServiceProvider, db);
        await RecoverStuckDraftsAsync(db);
    }

    private static async Task SeedAdminUserAsync(IServiceProvider sp, AppDbContext db)
    {
        var authOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthOptions>>().Value;
        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        if (adminUser != null)
        {
            if (adminUser.PasswordHash == "placeholder")
            {
                adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(authOpts.AdminPassword);
                adminUser.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }
        else
        {
            var adminUserId = new Guid("11111111-1111-1111-1111-111111111111");
            db.Users.Add(new AppUser
            {
                Id = adminUserId,
                Username = "admin",
                Email = "admin@socialworker.localtest",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(authOpts.AdminPassword),
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedDefaultLlmProviderAsync(IServiceProvider sp, AppDbContext db)
    {
        if (await db.LlmProviders.AnyAsync())
        {
            return;
        }

        var llmOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LlmOptions>>().Value;
        var providerType = "OpenRouter";
        if (llmOpts.BaseUrl.Contains("ollama") || llmOpts.BaseUrl.Contains("11434"))
        {
            providerType = "Ollama";
        }

        db.LlmProviders.Add(new Data.Entities.LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = "System Default Provider",
            ProviderType = providerType,
            BaseUrl = llmOpts.BaseUrl,
            ApiKey = llmOpts.ApiKey,
            Model = llmOpts.Model,
            IsDefault = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task RecoverStuckDraftsAsync(AppDbContext db)
    {
        var stuckDrafts = await db.Drafts
            .Where(d => d.Status == DraftStatus.Sourcing || d.Status == DraftStatus.Formatting)
            .ToListAsync();
        foreach (var draft in stuckDrafts)
        {
            draft.Status = DraftStatus.Failed;
            draft.Title = $"[Interrupted Ingestion] {draft.Title}";
        }
        if (stuckDrafts.Any())
        {
            await db.SaveChangesAsync();
        }
    }
}

static partial class WebApplicationExtensions
{
    internal static void MapAllEndpoints(this WebApplication app)
    {
        app.MapAccountsEndpoints();
        app.MapAuthEndpoints();
        app.MapUserEndpoints();
        app.MapAccountEndpoints();
        app.MapChatEndpoints();
        app.MapDraftEndpoints();
        app.MapProvidersEndpoints();
        app.MapSourcesEndpoints();
        app.MapMediaEndpoints();
        app.MapPublishingEndpoints();
        app.MapBrandVoicePromptsEndpoints();
        app.MapCodeImageEndpoints();
        app.MapPlatformVariantsEndpoints();
        app.MapFeedsEndpoints();
    }
}
