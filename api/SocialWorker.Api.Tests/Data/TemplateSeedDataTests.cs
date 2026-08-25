using System;
using System.Linq;
using System.Text.Json;
using SocialWorker.Api.Data.Seed;
using Xunit;

namespace SocialWorker.Api.Tests.Data;

public sealed class TemplateSeedDataTests
{
    [Fact]
    public void GetDefaultTemplates_ReturnsSixValidTemplates()
    {
        var templates = TemplateSeedData.GetDefaultTemplates();

        Assert.NotNull(templates);
        Assert.Equal(6, templates.Count);

        var ids = templates.Select(t => t.Id).Distinct().ToList();
        Assert.Equal(6, ids.Count);

        var names = templates.Select(t => t.Name).Distinct().ToList();
        Assert.Equal(6, names.Count);

        foreach (var template in templates)
        {
            Assert.False(string.IsNullOrWhiteSpace(template.Name));
            Assert.False(string.IsNullOrWhiteSpace(template.Description));
            Assert.True(template.Width > 0);
            Assert.True(template.Height > 0);
            Assert.False(string.IsNullOrWhiteSpace(template.Schema));

            // Validate that the schema string is valid JSON and contains slots and layers
            using var doc = JsonDocument.Parse(template.Schema);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("name", out _));
            Assert.True(root.TryGetProperty("width", out var w) && w.GetInt32() == template.Width);
            Assert.True(root.TryGetProperty("height", out var h) && h.GetInt32() == template.Height);
            Assert.True(root.TryGetProperty("slots", out var slots) && slots.ValueKind == JsonValueKind.Array);
            Assert.True(root.TryGetProperty("layers", out var layers) && layers.ValueKind == JsonValueKind.Array);
            Assert.True(layers.GetArrayLength() > 0);
        }
    }

    [Fact]
    public void DefaultTemplateIds_AreDeterministicAndExpected()
    {
        var templates = TemplateSeedData.GetDefaultTemplates();

        Assert.Contains(templates, t => t.Id == TemplateSeedData.QuoteCardSquareId && t.Name == "Quote Card (Square)");
        Assert.Contains(templates, t => t.Id == TemplateSeedData.QuoteCardPortraitId && t.Name == "Quote Card (Portrait)");
        Assert.Contains(templates, t => t.Id == TemplateSeedData.StatHighlightId && t.Name == "Stat Highlight");
        Assert.Contains(templates, t => t.Id == TemplateSeedData.AnnouncementCardId && t.Name == "Announcement Card");
        Assert.Contains(templates, t => t.Id == TemplateSeedData.CarouselSlideId && t.Name == "Carousel Slide");
        Assert.Contains(templates, t => t.Id == TemplateSeedData.CodeSnippetCardId && t.Name == "Code Snippet Card");
    }
}
