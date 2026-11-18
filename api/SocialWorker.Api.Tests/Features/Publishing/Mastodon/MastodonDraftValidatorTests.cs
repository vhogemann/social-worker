using System;
using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Chat.Tools;
using SocialWorker.Api.Features.Publishing.Mastodon.Validation;
using SocialWorker.Api.Features.Publishing.Validation;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Mastodon;

public sealed class MastodonDraftValidatorTests
{
    private readonly MastodonDraftValidator _validator = new();

    [Fact]
    public void Validate_CountsLongUrlsAs23Characters()
    {
        var prefix = new string('a', 470);
        var longUrl = "https://example.com/very/long/path/that/exceeds/thirty/characters/by/a/lot/1234567890";
        var content = $"{prefix} {longUrl}";

        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Valid, result.OverallStatus);
        Assert.Single(result.Posts);
        Assert.Equal(494, result.Posts[0].CharacterCount);
    }

    [Fact]
    public void Validate_Exceeding500WeightedChars_Fails()
    {
        var content = new string('a', 501);

        var result = _validator.Validate(content, Array.Empty<MediaAsset>());

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Error && i.Message.Contains("500-character"));
    }

    [Fact]
    public void Validate_MoreThan4Images_Fails()
    {
        var assets = new List<MediaAsset>();
        var embeds = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var id = Guid.NewGuid();
            assets.Add(new MediaAsset { Id = id, FileName = $"img{i}.png", MimeType = "image/png", AltText = "alt" });
            embeds.Add($"![alt](media://{id})");
        }

        var result = _validator.Validate("Photos:\n" + string.Join(" ", embeds), assets);

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Error && i.Message.Contains("at most 4 images"));
    }

    [Fact]
    public void Validate_MixingVideoAndImage_Fails()
    {
        var videoId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var assets = new[]
        {
            new MediaAsset { Id = videoId, FileName = "clip.mp4", MimeType = "video/mp4", AltText = "clip" },
            new MediaAsset { Id = imageId, FileName = "pic.png", MimeType = "image/png", AltText = "pic" }
        };

        var result = _validator.Validate($"Check this out ![clip](media://{videoId}) ![pic](media://{imageId})", assets);

        Assert.Equal(ValidateDraftOverallStatus.Failed, result.OverallStatus);
        Assert.Contains(result.Posts[0].Issues, i => i.Severity == ValidateDraftSeverity.Error && i.Message.Contains("cannot mix video/GIF with images"));
    }

    [Fact]
    public void MastodonPlatformContentPolicy_ReturnsValidAndRules()
    {
        var policy = new PlatformContentPolicy();
        var rules = policy.GetAdaptationRules(SocialWorker.Api.Data.SocialPlatform.Mastodon);
        Assert.Contains("500 characters", rules);
        Assert.Contains("23 characters", rules);
    }
}
