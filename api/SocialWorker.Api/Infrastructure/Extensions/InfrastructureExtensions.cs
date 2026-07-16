using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Accounts;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.PlatformVariants;
using SocialWorker.Api.Features.Prompts;
using SocialWorker.Api.Features.Auth;
using SocialWorker.Api.Infrastructure.Auth;
using SocialWorker.Api.Infrastructure.Background;

namespace SocialWorker.Api.Infrastructure.Extensions;

static partial class ServiceCollectionExtensions
{
    internal static void AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContextPool<AppDbContext>(o =>
            o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<BackgroundJobQueue>();
        builder.Services.AddHostedService<BackgroundJobHostedService>();

        builder.Services.AddScoped<AuthService>();
        builder.Services.AddScoped<DraftSegmentService>();
        builder.Services.AddScoped<DraftsService>();
        builder.Services.AddScoped<DraftChatSummaryService>();
        builder.Services.AddScoped<PlatformVariantService>();
        builder.Services.AddScoped<Features.Providers.Services.ProvidersService>();

        var jwtSecret = builder.Configuration["Auth:JwtSecret"]
            ?? "social_worker_super_secret_jwt_key_that_is_long_enough_for_security";

        builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
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
    }
}
