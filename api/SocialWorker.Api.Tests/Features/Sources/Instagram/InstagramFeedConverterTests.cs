using System;
using System.Collections.Generic;
using System.Linq;
using SocialWorker.Api.Features.Sources.Instagram;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Sources.Instagram;

public sealed class InstagramFeedConverterTests
{
    [Fact]
    public void ToFeed_ConvertsProfileAndMedia_ToStandardFeed()
    {
        var timestamp = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
        var profile = new InstagramProfileDiscovery(
            Id: "1784140001",
            Username: "flaviobolsonaro",
            Name: "Flávio Bolsonaro",
            ProfilePictureUrl: "https://example.com/pic.jpg",
            Biography: "Senador da República pelo RJ",
            Media: new InstagramMediaList(
                Data: new List<InstagramMediaItem>
                {
                    new(
                        Id: "1804291823",
                        Caption: "URGENTE: Veja o que disseram no Senado hoje sobre a proposta.",
                        MediaType: "IMAGE",
                        MediaUrl: "https://example.com/media1.jpg",
                        ThumbnailUrl: null,
                        Permalink: "https://www.instagram.com/p/DB12345",
                        Timestamp: timestamp,
                        LikeCount: 1500,
                        CommentsCount: 200),
                    new(
                        Id: "1804291824",
                        Caption: "Boa tarde a todos os patriotas.\nSeguimos firmes.",
                        MediaType: "VIDEO",
                        MediaUrl: "https://example.com/video.mp4",
                        ThumbnailUrl: "https://example.com/thumb.jpg",
                        Permalink: "https://www.instagram.com/p/DB67890",
                        Timestamp: timestamp.AddHours(-2),
                        LikeCount: 3000,
                        CommentsCount: 500)
                },
                Paging: null));

        var feed = InstagramFeedConverter.ToFeed(profile);

        Assert.NotNull(feed);
        Assert.Equal("Flávio Bolsonaro (@flaviobolsonaro) on Instagram", feed.Title);
        Assert.Equal("https://www.instagram.com/flaviobolsonaro", feed.Link);
        Assert.Equal(2, feed.Items.Count);

        var first = feed.Items.First();
        Assert.Equal("URGENTE: Veja o que disseram no Senado hoje sobre a proposta.", first.Title);
        Assert.Equal("https://www.instagram.com/p/DB12345", first.Link);
        Assert.Equal("URGENTE: Veja o que disseram no Senado hoje sobre a proposta.", first.Description);
        Assert.Equal("1804291823", first.Id);
        Assert.NotNull(first.PublishingDate);

        var second = feed.Items.Last();
        Assert.Equal("Boa tarde a todos os patriotas.", second.Title);
        Assert.Equal("https://www.instagram.com/p/DB67890", second.Link);
        Assert.Contains("Seguimos firmes.", second.Description);
        var rssItem = Assert.IsType<CodeHollow.FeedReader.Feeds.Rss20FeedItem>(second.SpecificItem);
        Assert.NotNull(rssItem.Enclosure);
        Assert.Equal("https://example.com/video.mp4", rssItem.Enclosure.Url);
        Assert.Equal("video/mp4", rssItem.Enclosure.MediaType);
    }
}
