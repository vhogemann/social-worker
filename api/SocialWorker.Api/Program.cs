using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Chat;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.Auth;
using SocialWorker.Api.Features.Users;
using SocialWorker.Api.Features.Providers;
using SocialWorker.Api.Features.Sources;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Infrastructure.Llm;
using SocialWorker.Api.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection("LLM"));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddHttpClient<ChatService>();
builder.Services.AddHttpClient<ModelCapabilityProbe>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddMemoryCache();

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
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

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

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapAccountEndpoints();
app.MapChatEndpoints();
app.MapDraftEndpoints();
app.MapProvidersEndpoints();
app.MapSourcesEndpoints();
app.MapMediaEndpoints();

app.Run();