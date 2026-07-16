using SocialWorker.Api.Data;
using SocialWorker.Api.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddLlm();
builder.AddChat();
builder.AddSources();
builder.AddMedia();
builder.AddPublishing();
builder.AddSearch();
builder.AddFeeds();
builder.AddInfrastructure();

var app = builder.Build();

await app.SeedDatabaseAsync();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapAllEndpoints();

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
