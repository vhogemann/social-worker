using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Drafts;
using SocialWorker.Api.Features.PlatformVariants;
using SocialWorker.Api.Features.Sources;

namespace SocialWorker.Api.Tests;

public sealed class PlatformVariantTests : SqliteTestBase
{
    private readonly AppDbContext _db;
    private readonly DraftsService _draftsService;
    private readonly PlatformVariantService _variantService;
    private readonly AppUser _user;

    public PlatformVariantTests()
    {
        _db = CreateDbContext();
        _user = CreateSeedUser(_db);

        var sourcesService = new SourcesService(_db, null!, null!, null!);
        _draftsService = new DraftsService(_db, null!, sourcesService, null!, null!);
        _variantService = new PlatformVariantService(_db, _draftsService);
    }

    protected override void Cleanup()
    {
        _db.Dispose();
        base.Cleanup();
    }

    [Fact]
    public async Task GenerateVariantsAsync_CreatesVariantDraftsWithCorrectPlatform()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Original Post", "First segment\n---\nSecond segment", "Bluesky", default);

        var result = await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter", "LinkedIn" }, default);

        Assert.Equal(canonical.Id, result.Canonical.Id);
        Assert.Equal(2, result.Variants.Count);

        var twitterVariant = result.Variants.First(v => v.TargetPlatform == "Twitter");
        var linkedInVariant = result.Variants.First(v => v.TargetPlatform == "LinkedIn");

        Assert.Equal(canonical.Id, twitterVariant.CanonicalDraftId);
        Assert.Equal(canonical.Id, linkedInVariant.CanonicalDraftId);
        Assert.Equal("Original Post (Twitter)", twitterVariant.Title);
        Assert.Equal("Original Post (LinkedIn)", linkedInVariant.Title);
    }

    [Fact]
    public async Task GenerateVariantsAsync_ReusesCanonicalContent()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Segment A\n---\nSegment B", "Bluesky", default);

        var result = await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter" }, default);

        var variant = result.Variants[0];
        Assert.Equal(canonical.Content, variant.Content);
    }

    [Fact]
    public async Task GenerateVariantsAsync_SkipsSourcePlatform()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);

        var result = await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Bluesky", "Twitter" }, default);

        Assert.Single(result.Variants);
        Assert.Equal("Twitter", result.Variants[0].TargetPlatform);
    }

    [Fact]
    public async Task GenerateVariantsAsync_SkipsDuplicatePlatform()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);

        await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter" }, default);

        var result = await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter", "LinkedIn" }, default);

        Assert.Single(result.Variants);
        Assert.Equal("LinkedIn", result.Variants[0].TargetPlatform);
    }

    [Fact]
    public async Task GenerateVariantsAsync_ThrowsForNonexistentCanonical()
    {
        var fakeId = Guid.NewGuid();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _variantService.GenerateVariantsAsync(_user.Id, fakeId, new List<string> { "Twitter" }, default));
    }

    [Fact]
    public async Task GenerateVariantsAsync_ThrowsForEmptyPlatforms()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _variantService.GenerateVariantsAsync(_user.Id, canonical.Id, new List<string>(), default));

        Assert.Contains("platform", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateVariantsAsync_CreatesPlatformThreadForVariant()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);

        var result = await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter" }, default);

        var variant = result.Variants[0];
        var threads = await _draftsService.GetPlatformThreadsForDraftAsync(_user.Id, variant.Id, default);

        Assert.Single(threads);
        Assert.Equal("Twitter", threads[0].Platform);
        Assert.Equal("Draft", threads[0].Stage);
    }

    [Fact]
    public async Task GenerateVariantsAsync_SkipsInvalidPlatformName()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);

        var result = await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "InvalidPlatform", "Twitter" }, default);

        Assert.Single(result.Variants);
        Assert.Equal("Twitter", result.Variants[0].TargetPlatform);
    }

    [Fact]
    public async Task GetDraftFamilyAsync_ReturnsCanonicalAndAllVariants()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);
        await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter", "LinkedIn" }, default);

        var family = await _variantService.GetDraftFamilyAsync(_user.Id, canonical.Id, default);

        Assert.Equal(canonical.Id, family.Canonical.Id);
        Assert.Equal(2, family.Variants.Count);
    }

    [Fact]
    public async Task GetDraftFamilyAsync_WorksFromVariantDraft()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);
        await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter" }, default);

        var variant = await _db.Drafts
            .FirstAsync(d => d.CanonicalDraftId == canonical.Id);

        var family = await _variantService.GetDraftFamilyAsync(_user.Id, variant.Id, default);

        Assert.Equal(canonical.Id, family.Canonical.Id);
        Assert.Single(family.Variants);
        Assert.Equal(variant.Id, family.Variants[0].Id);
    }

    [Fact]
    public async Task GetDraftFamilyAsync_ThrowsForNonexistentDraft()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _variantService.GetDraftFamilyAsync(_user.Id, Guid.NewGuid(), default));
    }

    [Fact]
    public async Task GetVariantAsync_ReturnsCorrectVariant()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);
        await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter", "LinkedIn" }, default);

        var variant = await _variantService.GetVariantAsync(_user.Id, canonical.Id, "Twitter", default);

        Assert.NotNull(variant);
        Assert.Equal("Twitter", variant.TargetPlatform);
        Assert.Equal(canonical.Id, variant.CanonicalDraftId);
    }

    [Fact]
    public async Task GetVariantAsync_ReturnsNullForMissingVariant()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);

        var variant = await _variantService.GetVariantAsync(_user.Id, canonical.Id, "Twitter", default);

        Assert.Null(variant);
    }

    [Fact]
    public async Task GetVariantAsync_ReturnsNullForInvalidPlatform()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);

        var variant = await _variantService.GetVariantAsync(_user.Id, canonical.Id, "FakeBook", default);

        Assert.Null(variant);
    }

    [Fact]
    public async Task GetVariantsForDraftAsync_ReturnsAllVariants()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);
        await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter", "LinkedIn", "Instagram" }, default);

        var variants = await _variantService.GetVariantsForDraftAsync(_user.Id, canonical.Id, default);

        Assert.Equal(3, variants.Count);
        Assert.Contains(variants, v => v.TargetPlatform == "Twitter");
        Assert.Contains(variants, v => v.TargetPlatform == "LinkedIn");
        Assert.Contains(variants, v => v.TargetPlatform == "Instagram");
    }

    [Fact]
    public async Task GetVariantsForDraftAsync_WorksFromVariantDraft()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);
        await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter", "LinkedIn" }, default);

        var variant = await _db.Drafts
            .FirstAsync(d => d.CanonicalDraftId == canonical.Id);

        var variants = await _variantService.GetVariantsForDraftAsync(_user.Id, variant.Id, default);

        Assert.Equal(2, variants.Count);
    }

    [Fact]
    public async Task CreateDraftWithTargetPlatform_PersistsAndReturnsCorrectly()
    {
        var draft = await _draftsService.CreateDraftAsync(_user.Id, "Twitter Post", "Tweet content", "Twitter", default);

        Assert.Equal("Twitter", draft.TargetPlatform);
        Assert.Null(draft.CanonicalDraftId);

        var fetched = await _draftsService.GetDraftByIdAsync(_user.Id, draft.Id, default);
        Assert.Equal("Twitter", fetched.TargetPlatform);
        Assert.Null(fetched.CanonicalDraftId);
    }

    [Fact]
    public async Task CreateDraftDefaults_ToBluesky()
    {
        var draft = await _draftsService.CreateDraftAsync(_user.Id, "Default", "Content", null, default);

        Assert.Equal("Bluesky", draft.TargetPlatform);
    }

    [Fact]
    public async Task VariantDrafts_AreListedInGetDraftsForUser()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);
        await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter" }, default);

        var allDrafts = await _draftsService.GetDraftsForUserAsync(_user.Id, default);

        var variant = allDrafts.FirstOrDefault(d => d.TargetPlatform == "Twitter");
        Assert.NotNull(variant);
        Assert.Equal(canonical.Id, variant.CanonicalDraftId);
    }

    [Fact]
    public async Task UpdateDraftAsync_PreservesTargetPlatform()
    {
        var draft = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "LinkedIn", default);

        var updated = await _draftsService.UpdateDraftAsync(
            _user.Id, draft.Id, "Updated Title", null, null, null, null, null, default);

        Assert.Equal("LinkedIn", updated.TargetPlatform);
        Assert.Equal("Updated Title", updated.Title);
    }

    [Fact]
    public async Task VariantsAreIndependent_EditingOneDoesNotAffectOthers()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Original content", "Bluesky", default);
        await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter", "LinkedIn" }, default);

        var twitterVariant = await _variantService.GetVariantAsync(_user.Id, canonical.Id, "Twitter", default);
        Assert.NotNull(twitterVariant);

        await _draftsService.UpdateDraftAsync(
            _user.Id, twitterVariant.Id, null, "Modified Twitter content", null, null, null, null, default);

        var linkedInVariant = await _variantService.GetVariantAsync(_user.Id, canonical.Id, "LinkedIn", default);
        Assert.NotNull(linkedInVariant);
        Assert.Equal("Original content", linkedInVariant.Content);

        var canonicalReloaded = await _draftsService.GetDraftByIdAsync(_user.Id, canonical.Id, default);
        Assert.Equal("Original content", canonicalReloaded.Content);
    }

    [Fact]
    public async Task DeletingCanonical_DoesNotCascadeToVariants()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Test", "Content", "Bluesky", default);
        await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter" }, default);

        await _draftsService.UpdateDraftAsync(
            _user.Id, canonical.Id, null, null, "Deleted", null, null, null, default);

        var allDrafts = await _draftsService.GetDraftsForUserAsync(_user.Id, default);
        Assert.DoesNotContain(allDrafts, d => d.Id == canonical.Id);

        var variant = await _variantService.GetVariantAsync(_user.Id, canonical.Id, "Twitter", default);
        Assert.Null(variant);
    }

    [Fact]
    public async Task GenerateVariantsAsync_ReconcilesSegments()
    {
        var canonical = await _draftsService.CreateDraftAsync(_user.Id, "Multi-segment", "Segment A\n---\nSegment B\n---\nSegment C", "Bluesky", default);

        var result = await _variantService.GenerateVariantsAsync(
            _user.Id, canonical.Id, new List<string> { "Twitter" }, default);

        var variant = result.Variants[0];
        var segments = _db.ThreadSegments
            .Where(s => s.DraftId == variant.Id)
            .OrderBy(s => s.Position)
            .ToList();

        Assert.Equal(3, segments.Count);
        Assert.Equal("Segment A", segments[0].Content);
        Assert.Equal("Segment B", segments[1].Content);
        Assert.Equal("Segment C", segments[2].Content);
    }
}