using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Accounts;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Drafts.Services;
using SocialWorker.Api.Features.PlatformVariants;
using SocialWorker.Api.Features.Prompts;
using SocialWorker.Api.Features.Auth;
using SocialWorker.Api.Infrastructure.Auth;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        builder.Services.AddDbContextPool<AppDbContext>(o =>
            o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

        builder.Services.AddMemoryCache();
        builder.Services.AddValidatorsFromAssemblyContaining<Program>();

        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<DraftSegmentService>();
        builder.Services.AddScoped<DraftsService>();
        builder.Services.AddScoped<DraftChatSummaryService>();
        builder.Services.AddScoped<PlatformVariantService>();
        builder.Services.AddScoped<IDraftAutoRepairService, DraftAutoRepairService>();

        builder.Services.AddScoped<Features.Providers.Services.ProvidersService>();
        builder.Services.AddScoped<ApiKeyService>();
        builder.Services.AddScoped<OAuthStateService>();
        builder.Services.AddScoped<AccountUpsertService>();

        builder.Services.AddSingleton<Features.Mcp.Services.McpSessionManager>();
        builder.Services.AddScoped<Features.Mcp.Services.McpToolRegistry>();
        builder.Services.AddScoped<Features.Mcp.Services.McpResourceRegistry>();
        builder.Services.AddScoped<Features.Mcp.Services.McpServer>();


        var jwtSecret = builder.Configuration["Auth:JwtSecret"]
            ?? "social_worker_super_secret_jwt_key_that_is_long_enough_for_security";

        builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
        builder.Services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = "Composite";
            o.DefaultChallengeScheme = "Composite";
        })
        .AddPolicyScheme("Composite", "JWT or API Key", o =>
        {
            o.ForwardDefaultSelector = context =>
            {
                if (context.Request.Headers.ContainsKey("X-API-Key") ||
                    context.Request.Query.ContainsKey("api_key") ||
                    (context.Request.Headers.Authorization.ToString() is { } auth && auth.Contains("sw_live_")))
                {
                    return ApiKeyAuthenticationHandler.SchemeName;
                }

                return JwtBearerDefaults.AuthenticationScheme;
            };
        })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
            };
        })
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, null);


        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
        });

        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()));
    }
}
