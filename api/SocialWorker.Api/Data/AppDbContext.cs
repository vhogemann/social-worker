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
        });

        modelBuilder.Entity<Source>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(50);
            e.Property(x => x.Reference).HasMaxLength(500);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.Content).HasColumnType("text");
            e.Property(x => x.Summary).HasColumnType("text");
            e.Property(x => x.TranscriptStatus).HasConversion<string>().HasMaxLength(50);
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
            e.HasIndex(x => new { x.UserId, x.Platform }).IsUnique();
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
            e.Property(x => x.InstructionPrompt).HasColumnType("text");
            e.Property(x => x.IncludeFilters).HasMaxLength(500);
            e.Property(x => x.ExcludeFilters).HasMaxLength(500);
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId);
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
            e.HasIndex(x => x.FeedSubscriptionId);
            e.HasIndex(x => new { x.FeedSubscriptionId, x.ItemLink }).IsUnique();
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
        });
    }
}