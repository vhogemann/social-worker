using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Data;

public class AppDbContext : DbContext
{
    public DbSet<Draft> Drafts => Set<Draft>();
    public DbSet<ThreadSegment> ThreadSegments => Set<ThreadSegment>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<LlmProvider> LlmProviders => Set<LlmProvider>();
    public DbSet<PlatformThread> PlatformThreads => Set<PlatformThread>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<DraftSource> DraftSources => Set<DraftSource>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<BrandVoicePrompt> BrandVoicePrompts => Set<BrandVoicePrompt>();
    public DbSet<DraftBlueskyMetadata> DraftBlueskyMetadata => Set<DraftBlueskyMetadata>();
    public DbSet<FeedSubscription> FeedSubscriptions => Set<FeedSubscription>();
    public DbSet<FeedIngestionQueueItem> FeedIngestionQueueItems => Set<FeedIngestionQueueItem>();
    public DbSet<AutomationWorkflow> AutomationWorkflows => Set<AutomationWorkflow>();
    public DbSet<AutomationWorkflowSource> AutomationWorkflowSources => Set<AutomationWorkflowSource>();
    public DbSet<AutomationWorkflowDestination> AutomationWorkflowDestinations => Set<AutomationWorkflowDestination>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<BackgroundTask> BackgroundTasks => Set<BackgroundTask>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ScheduleSlotRule> ScheduleSlotRules => Set<ScheduleSlotRule>();
    public DbSet<ScheduleQueueItem> ScheduleQueueItems => Set<ScheduleQueueItem>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.Role).HasMaxLength(50);
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.PreferredProvider)
                .WithMany()
                .HasForeignKey(x => x.PreferredProviderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Token).HasMaxLength(200);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Draft>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.Content).HasColumnType("text");
            e.Property(x => x.TargetPlatform).HasConversion<string>().HasMaxLength(50);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CanonicalDraft)
                .WithMany(x => x.Variants)
                .HasForeignKey(x => x.CanonicalDraftId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.CanonicalDraftId);
            e.HasOne(x => x.AutomationWorkflow)
                .WithMany(w => w.Drafts)
                .HasForeignKey(x => x.AutomationWorkflowId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.AutomationWorkflowId);
            e.HasOne(x => x.TargetAccount)
                .WithMany()
                .HasForeignKey(x => x.TargetAccountId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.TargetAccountId);
        });

        modelBuilder.Entity<ThreadSegment>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).HasColumnType("text");
            e.HasOne(x => x.Draft)
                .WithMany(d => d.Segments)
                .HasForeignKey(x => x.DraftId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.DraftId, x.Position }).IsUnique();
        });

        modelBuilder.Entity<PlatformThread>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Platform).HasMaxLength(100);
            e.Property(x => x.Stage).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.Content).HasColumnType("text");
            e.HasOne(x => x.Draft)
                .WithMany(d => d.Threads)
                .HasForeignKey(x => x.DraftId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.DraftId, x.Platform }).IsUnique();
            e.HasOne(x => x.Account)
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.AccountId);
        });

        modelBuilder.Entity<Source>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.Reference).HasMaxLength(500);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Content).HasColumnType("text");
            e.Property(x => x.Summary).HasColumnType("text");
            e.Property(x => x.ProcessingStatus).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.TranscriptPath).HasMaxLength(500);
            e.Property(x => x.YoutubeVideoId).HasMaxLength(11);
            e.Property(x => x.Sha256).HasMaxLength(64);
        });

        modelBuilder.Entity<DraftSource>(e =>
        {
            e.HasKey(x => new { x.DraftId, x.SourceId });
            e.HasOne(x => x.Draft)
                .WithMany(d => d.DraftSources)
                .HasForeignKey(x => x.DraftId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Source)
                .WithMany(s => s.DraftSources)
                .HasForeignKey(x => x.SourceId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.DraftId, x.SourceId }).IsUnique();
        });

        modelBuilder.Entity<MediaAsset>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(255);
            e.Property(x => x.MimeType).HasMaxLength(100);
            e.Property(x => x.AltText).HasMaxLength(1000);
            e.Property(x => x.FilePath).HasMaxLength(500);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.HasOne(x => x.Draft)
                .WithMany(d => d.MediaAssets)
                .HasForeignKey(x => x.DraftId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LlmProvider>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.ProviderType).HasMaxLength(50);
            e.Property(x => x.BaseUrl).HasMaxLength(500);
            e.Property(x => x.ApiKey).HasMaxLength(500);
            e.Property(x => x.Model).HasMaxLength(200);
            e.Property(x => x.ContextWindowTokens);
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Account>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Platform).HasMaxLength(100);
            e.Property(x => x.Handle).HasMaxLength(255);
            e.Property(x => x.Status).HasMaxLength(50);
            e.HasIndex(x => new { x.UserId, x.Platform, x.Handle }).IsUnique();
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Post>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Platform).HasMaxLength(100);
            e.Property(x => x.RemoteId).HasMaxLength(255);
            e.Property(x => x.Url).HasMaxLength(1000);
            e.HasOne(x => x.Draft)
                .WithMany()
                .HasForeignKey(x => x.DraftId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.PlatformThread)
                .WithMany()
                .HasForeignKey(x => x.PlatformThreadId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PlatformThreadId, x.SegmentIndex }).IsUnique();
        });

        modelBuilder.Entity<BrandVoicePrompt>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Body).HasColumnType("text");
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DraftBlueskyMetadata>(e =>
        {
            e.HasKey(x => x.DraftId);
            e.Property(x => x.ReplyRootUri).HasMaxLength(1000);
            e.Property(x => x.ReplyRootCid).HasMaxLength(255);
            e.Property(x => x.ReplyParentUri).HasMaxLength(1000);
            e.Property(x => x.ReplyParentCid).HasMaxLength(255);
            e.Property(x => x.ReplyParentUrl).HasMaxLength(1000);
            e.Property(x => x.ReplyParentAuthor).HasMaxLength(255);
            e.Property(x => x.ReplyParentText).HasColumnType("text");
            e.Property(x => x.ReplyParentAvatarUrl).HasMaxLength(2000);
            e.HasOne(x => x.Draft)
                .WithOne(d => d.BlueskyMetadata)
                .HasForeignKey<DraftBlueskyMetadata>(x => x.DraftId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FeedSubscription>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.FeedUrl).HasMaxLength(500);
            e.Property(x => x.WebsiteUrl).HasMaxLength(500);
            e.Property(x => x.IncludeFilters).HasMaxLength(500);
            e.Property(x => x.ExcludeFilters).HasMaxLength(500);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<AutomationWorkflow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.InstructionPrompt).HasColumnType("text");
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<AutomationWorkflowSource>(e =>
        {
            e.HasKey(x => new { x.AutomationWorkflowId, x.FeedSubscriptionId });
            e.HasOne(x => x.AutomationWorkflow)
                .WithMany(w => w.WorkflowSources)
                .HasForeignKey(x => x.AutomationWorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.FeedSubscription)
                .WithMany(s => s.WorkflowSources)
                .HasForeignKey(x => x.FeedSubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.FeedSubscriptionId);
        });

        modelBuilder.Entity<AutomationWorkflowDestination>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Platform).HasMaxLength(50);
            e.HasOne(x => x.AutomationWorkflow)
                .WithMany(w => w.Destinations)
                .HasForeignKey(x => x.AutomationWorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.AutomationWorkflowId);
            e.HasOne(x => x.Account)
                .WithMany()
                .HasForeignKey(x => x.AccountId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.AccountId);
            e.HasOne(x => x.Schedule)
                .WithMany()
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.ScheduleId);
        });

        modelBuilder.Entity<FeedIngestionQueueItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ItemTitle).HasMaxLength(500);
            e.Property(x => x.ItemLink).HasMaxLength(1000);
            e.Property(x => x.ItemDescription).HasColumnType("text");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.LastError).HasColumnType("text");
            e.HasOne(x => x.FeedSubscription)
                .WithMany()
                .HasForeignKey(x => x.FeedSubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AutomationWorkflow)
                .WithMany()
                .HasForeignKey(x => x.AutomationWorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.FeedSubscriptionId);
            e.HasIndex(x => x.AutomationWorkflowId);
            e.HasIndex(x => new { x.FeedSubscriptionId, x.AutomationWorkflowId, x.ItemLink }).IsUnique();
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
        });

        modelBuilder.Entity<Template>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Schema).HasColumnType("text");
            e.HasOne(x => x.ThumbnailMediaAsset)
                .WithMany()
                .HasForeignKey(x => x.ThumbnailMediaAssetId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<BackgroundTask>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TaskType).HasMaxLength(100);
            e.Property(x => x.PayloadJson).HasColumnType("text");
            e.Property(x => x.LockedByWorker).HasMaxLength(150);
            e.Property(x => x.LastError).HasColumnType("text");
            e.Property(x => x.DeduplicationKey).HasMaxLength(300);
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
            e.HasIndex(x => x.LockedUntil);
            e.HasIndex(x => new { x.TaskType, x.CreatedAt });
            e.HasIndex(x => x.DeduplicationKey);
        });

        modelBuilder.Entity<ApiKey>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.KeyPrefix).HasMaxLength(32);
            e.Property(x => x.KeyHash).HasMaxLength(128);
            e.Property(x => x.Scopes).HasMaxLength(500);
            e.HasIndex(x => x.KeyHash).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Schedule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Timezone).HasMaxLength(100);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<ScheduleSlotRule>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RecurrenceType).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.CronExpression).HasMaxLength(100);
            e.Property(x => x.DaysOfWeek)
                .HasConversion(
                    v => v == null ? null : string.Join(',', v),
                    v => string.IsNullOrWhiteSpace(v) ? null : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray())
                .HasMaxLength(50);
            e.HasOne(x => x.Schedule)
                .WithMany(s => s.SlotRules)
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ScheduleId);
        });

        modelBuilder.Entity<ScheduleQueueItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.LastError).HasColumnType("text");
            e.HasOne(x => x.Schedule)
                .WithMany(s => s.QueueItems)
                .HasForeignKey(x => x.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Draft)
                .WithMany()
                .HasForeignKey(x => x.DraftId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.PlatformThread)
                .WithMany(p => p.ScheduleQueueItems)
                .HasForeignKey(x => x.PlatformThreadId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.ScheduleId, x.Position });
            e.HasIndex(x => new { x.Status, x.AssignedSlotUtc });
            e.HasIndex(x => x.DraftId);
            e.HasIndex(x => x.PlatformThreadId);
        });
    }
}