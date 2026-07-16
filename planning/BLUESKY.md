# Bluesky Thread Expansion & DbUp SQL Migrations

This plan implements database migrations using **DbUp** (a lightweight, ready-to-use raw SQL migration library for .NET) and extracts Bluesky-specific reply/thread-expansion metadata into a dedicated `DraftBlueskyMetadata` entity with a 1:1 relationship to `Draft`.

## User Review Required

> [!WARNING]
> - **Breaking DB Change**: Nuking the existing EF migrations and starting from scratch with DbUp raw SQL migrations. This will wipe existing local dev databases (which will be recreated on next startup).
> - **DbUp PostgreSQL**: We will add the `dbup-postgresql` NuGet package to run raw `.sql` migration files sequentially from a `Data/Migrations/` directory.

## Proposed Changes

### 1. Domain Model Changes

We will create a new entity for Bluesky-specific metadata.

#### [NEW] [DraftBlueskyMetadata.cs](file:///home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Data/Entities/DraftBlueskyMetadata.cs)
```csharp
using System;

namespace SocialWorker.Api.Data.Entities;

public class DraftBlueskyMetadata
{
    public Guid DraftId { get; set; }
    public Draft Draft { get; set; } = null!;

    public string? ReplyRootUri { get; set; }
    public string? ReplyRootCid { get; set; }
    public string? ReplyParentUri { get; set; }
    public string? ReplyParentCid { get; set; }
    public string? ReplyParentUrl { get; set; }
    public string? ReplyParentAuthor { get; set; }
    public string? ReplyParentText { get; set; }
}
```

#### [MODIFY] [AppDbContext.cs](file:///home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Data/AppDbContext.cs)
- Add `public DbSet<DraftBlueskyMetadata> DraftBlueskyMetadata => Set<DraftBlueskyMetadata>();`.
- In `OnModelCreating`, configure the 1:1 relationship:
  ```csharp
  modelBuilder.Entity<DraftBlueskyMetadata>(e =>
  {
      e.HasKey(x => x.DraftId);
      e.HasOne(x => x.Draft)
          .WithOne()
          .HasForeignKey<DraftBlueskyMetadata>(x => x.DraftId)
          .OnDelete(DeleteBehavior.Cascade);
  });
  ```

---

### 2. SQL Migration System (DbUp)

1. **Nuke migrations folder**: We will delete all files inside `api/SocialWorker.Api/Migrations/`.
2. **Add NuGet Package**: Add `dbup-postgresql` to the API project.
3. **Generate Initial Schema Script**: 
   Generate a complete SQL script representing the new clean-slate database schema and save it to `api/SocialWorker.Api/Data/Migrations/0001_initial_schema.sql`.
4. **Wire DbUp in [Program.cs](file:///home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Program.cs)**:
   Replace EF migrations with DbUp:
   ```csharp
   var connectionString = builder.Configuration.GetConnectionString("Default");
   
   // Ensure database exists before upgrading (DbUp helper)
   DbUp.EnsureDatabase.For.PostgresqlDatabase(connectionString);

   var upgrader = DbUp.DeployChanges.To
       .PostgresqlDatabase(connectionString)
       .WithScriptsFromFileSystem(Path.Combine(AppContext.BaseDirectory, "Data", "Migrations"))
       .LogToConsole()
       .Build();

   var result = upgrader.PerformUpgrade();
   if (!result.Successful)
   {
       throw new Exception($"Database migration failed: {result.Error}");
   }
   ```

---

### 3. Backend Endpoint and Service Changes

#### [MODIFY] [DraftsService.cs](file:///home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Features/Drafts/Services/DraftsService.cs)
- Update DTO generation to fetch/include the `DraftBlueskyMetadata` fields if present.
- Add endpoint/method to set or clear the reply target for a draft.

#### [MODIFY] [BlueskyPublisher.cs](file:///home/vhogemann/Projects/social-worker/api/SocialWorker.Api/Features/Publishing/BlueskyPublisher.cs)
- Load `DraftBlueskyMetadata` for the draft.
- If present, inject `Reply` (`root` and `parent` refs) into the first published post record.

---

### 4. UI Components

#### [NEW] [ReplyTargetCard.tsx](file:///home/vhogemann/Projects/social-worker/web/src/components/EditorPanel/ReplyTargetCard.tsx)
- Render the active reply target post preview (Author, Content, Link).
- Show input field to set a reply target via URL if none is configured.

---

## Verification Plan

### Automated Tests
- Validate DbUp migrations run successfully on startup.
- Unit test `BlueskyPublisher` with mock reply metadata.
- Run the full test suite.

### Manual Verification
1. Nuke dev database volumes: `docker compose down -v`.
2. Restart: `docker compose up --build`.
3. Confirm clean startup and migration table (`schemaversions`) populated.
4. Verify setting a reply target on a draft, viewing it in the UI, and publishing it correctly replies on Bluesky.
