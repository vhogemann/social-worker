using System.Text;
using DbUp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Auth;
using SocialWorker.Api.Features.Users;
using SocialWorker.Api.Features.Providers;
using SocialWorker.Api.Features.Providers.Services;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Accounts;
using SocialWorker.Api.Features.Prompts;
using SocialWorker.Api.Features.CodeImages;
using SocialWorker.Api.Features.PlatformVariants;
using SocialWorker.Api.Features.Feeds;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation;
using SocialWorker.Api.Features.Publishing.Bluesky.Validation.Rules;
using SocialWorker.Api.Features.Publishing.Validation;
using SocialWorker.Api.Infrastructure.Llm;
using SocialWorker.Api.Infrastructure.Auth;
using SocialWorker.Api.Infrastructure.Background;
using SocialWorker.Api.Infrastructure.Search;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("LLM"));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<ChatOptions>(builder.Configuration.GetSection(ChatOptions.SectionName));
builder.Services.Configure<TranscriberOptions>(builder.Configuration.GetSection(TranscriberOptions.SectionName));
builder.Services.AddHttpClient<OpenAiProviderAdapter>();
builder.Services.AddScoped<ILlmProviderAdapter>(sp =>
{
    var demoProfile = Environment.GetEnvironmentVariable("DEMO_LLM_PROFILE");
    if (!string.IsNullOrWhiteSpace(demoProfile))
    {
        return new DemoLlmAdapter();
    }
    return sp.GetRequiredService<OpenAiProviderAdapter>();
});
builder.Services.AddScoped<DraftTitleGenerator>();
builder.Services.AddScoped<ChatSessionLoader>();
builder.Services.AddScoped<SystemPromptBuilder>();
builder.Services.AddScoped<ChatStreamWriter>();
builder.Services.AddScoped<ChatRequestPreparationService>();
builder.Services.AddScoped<ChatToolExecutor>();
builder.Services.AddScoped<ChatRoundProcessor>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddHttpClient<ModelCapabilityProbe>();
builder.Services.AddScoped<SourceExtractor>();
builder.Services.AddHttpClient<WebScraperService>();
builder.Services.AddHttpClient<FeedDiscoveryService>();
builder.Services.AddScoped<FeedOrchestrationService>();
builder.Services.AddSingleton<FeedPollingHostedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FeedPollingHostedService>());
builder.Services.AddHttpClient<ITranscriptExtractionService, TranscriptExtractionService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TranscriberOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddHttpClient<SummarizationService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TranscriberOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddScoped<SourceReconciliationService>();
builder.Services.AddScoped<SourceTranscriptionService>();
builder.Services.AddScoped<SourceSearchService>();
builder.Services.AddScoped<SourcesService>();
builder.Services.AddSingleton<ImageResizer>();
builder.Services.AddSingleton<FileStorageProvider>();
builder.Services.AddScoped<MediaService>();
builder.Services.AddScoped<DraftSegmentService>();
builder.Services.AddScoped<DraftsService>();
builder.Services.AddScoped<DraftChatSummaryService>();
builder.Services.AddScoped<PlatformVariantService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProvidersService>();
builder.Services.AddSingleton<PlatformContentPolicy>();
builder.Services.AddSingleton<IPlatformContentPolicy, BlueskyPlatformContentPolicy>();
builder.Services.AddSingleton<IPlatformContentPolicy, TwitterPlatformContentPolicy>();
builder.Services.AddSingleton<IPlatformContentPolicy, LinkedInPlatformContentPolicy>();
builder.Services.AddSingleton<IPlatformContentPolicy, InstagramPlatformContentPolicy>();
builder.Services.AddSingleton<IPlatformContentPolicy, FacebookPlatformContentPolicy>();
builder.Services.AddSingleton<BlueskyContentValidator>();
builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyMaxCharactersRule>();
builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyMaxImagesRule>();
builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyNoMixedImagesAndYouTubeRule>();
builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyUnsupportedMarkdownRule>();
builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyPlaceholderLinkRule>();
builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyPlaceholderMediaRule>();
builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyPlaceholderUrlRule>();
builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyMissingAltTextRule>();
builder.Services.AddSingleton<IValidateDraftRule<BlueskySegmentValidation>, BlueskyTitleLikeOpenerRule>();
builder.Services.AddScoped<BlueskyDraftValidator>();
builder.Services.AddScoped<IChatTool, ReplaceEditorContentTool>();
builder.Services.AddScoped<IChatTool, ProposeStageTransitionTool>();
builder.Services.AddScoped<IChatTool, ListSourcesTool>();
builder.Services.AddScoped<IChatTool, FetchSourceTool>();
builder.Services.AddScoped<IChatTool, ViewImageTool>();
builder.Services.AddScoped<IChatTool, PublishPlatformTool>();
builder.Services.AddScoped<IChatTool, WebSearchTool>();
builder.Services.AddScoped<IChatTool, AddSourceTool>();
builder.Services.AddScoped<IChatTool, ValidateDraftTool>();
builder.Services.AddScoped<IChatTool, AddImageSourceTool>();
builder.Services.AddScoped<IChatTool, ImageSearchTool>();
builder.Services.AddScoped<IChatTool, RenderCodeBlocksTool>();
builder.Services.AddScoped<IChatTool, FormatValidatePlatformContentTool>();
builder.Services.AddScoped<IChatTool, GeneratePlatformVariantsTool>();
builder.Services.AddScoped<IChatTool, SearchSourcesTool>();
builder.Services.AddScoped<IChatTool, SetBlueskyReplyTargetTool>();
builder.Services.AddSingleton<CodeImageRenderer>();
builder.Services.AddScoped<CodeImageService>();

builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection(SearchOptions.SectionName));
builder.Services.AddHttpClient<BraveSearchEngine>();
builder.Services.AddHttpClient<SearXngSearchEngine>();
builder.Services.AddScoped<ISearchEngine>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SearchOptions>>().Value;
    if (string.Equals(opts.Provider, "Brave", StringComparison.OrdinalIgnoreCase))
    {
        return sp.GetRequiredService<BraveSearchEngine>();
    }
    return sp.GetRequiredService<SearXngSearchEngine>();
});
builder.Services.AddHttpClient<IPublisher, BlueskyPublisher>();
builder.Services.AddHttpClient<IBlueskyReplyTargetResolver, BlueskyReplyTargetResolver>();
builder.Services.AddScoped<IPublisher, TwitterPublisher>();
builder.Services.AddScoped<IPublisher, LinkedInPublisher>();
builder.Services.AddScoped<IPublisher, FacebookPublisher>();
builder.Services.AddScoped<IPublisher, InstagramPublisher>();
builder.Services.AddScoped<IPublisherResolver, PublisherResolver>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<LlmProviderService>();
builder.Services.AddSingleton<BackgroundJobQueue>();
builder.Services.AddHostedService<BackgroundJobHostedService>();

builder.Services.AddDbContextPool<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var jwtSecret = builder.Configuration["Auth:JwtSecret"] ?? "social_worker_super_secret_jwt_key_that_is_long_enough_for_security";
builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var connectionString = builder.Configuration.GetConnectionString("Default");
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

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var authOpts = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthOptions>>().Value;
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
        db.Users.Add(new SocialWorker.Api.Data.Entities.AppUser
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

    var llmOpts = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<LlmOptions>>().Value;
    if (!await db.LlmProviders.AnyAsync())
    {
        var providerType = "OpenRouter";
        if (llmOpts.BaseUrl.Contains("ollama") || llmOpts.BaseUrl.Contains("11434"))
        {
            providerType = "Ollama";
        }

        db.LlmProviders.Add(new SocialWorker.Api.Data.Entities.LlmProvider
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
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

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

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/__tests/reset", async (AppDbContext db) =>
    {
        db.Posts.RemoveRange(db.Posts);
        db.PlatformThreads.RemoveRange(db.PlatformThreads);
        db.ThreadSegments.RemoveRange(db.ThreadSegments);
        db.DraftSources.RemoveRange(db.DraftSources);
        db.Sources.RemoveRange(db.Sources);
        db.MediaAssets.RemoveRange(db.MediaAssets);
        db.Drafts.RemoveRange(db.Drafts);
        await db.SaveChangesAsync();
        return Results.Ok(new { reset = true });
    });
}

app.Run();