using System;
using System.Linq;
using System.Text.Json;
using SocialWorker.Api.Data.Seed;
using Xunit;

namespace SocialWorker.Api.Tests.Data;

public sealed class TemplateSeedDataTests
{
    [Fact]
    public void GetDefaultDefinitions_ReturnsSixValidTemplateDefinitions()
    {
        var definitions = TemplateSeedData.GetDefaultDefinitions();

        Assert.NotNull(definitions);
        Assert.Equal(6, definitions.Count);

        var names = definitions.Select(t => t.Name).Distinct().ToList();
        Assert.Equal(6, names.Count);

        foreach (var def in definitions)
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Name));
            Assert.False(string.IsNullOrWhiteSpace(def.Description));
            Assert.True(def.Width > 0);
            Assert.True(def.Height > 0);
            Assert.False(string.IsNullOrWhiteSpace(def.Schema));

            // Validate that the schema string is valid JSON and contains slots and layers
            using var doc = JsonDocument.Parse(def.Schema);
            var root = doc.RootElement;

            Assert.True(root.TryGetProperty("name", out _));
            Assert.True(root.TryGetProperty("width", out var w) && w.GetInt32() == def.Width);
            Assert.True(root.TryGetProperty("height", out var h) && h.GetInt32() == def.Height);
            Assert.True(root.TryGetProperty("slots", out var slots) && slots.ValueKind == JsonValueKind.Array);
            Assert.True(root.TryGetProperty("layers", out var layers) && layers.ValueKind == JsonValueKind.Array);
            Assert.True(layers.GetArrayLength() > 0);
        }
    }

    [Fact]
    public void CreateDefaultTemplatesForUser_GeneratesUserTemplatesWithUniqueIds()
    {
        var userId = Guid.NewGuid();
        var templates = TemplateSeedData.CreateDefaultTemplatesForUser(userId);

        Assert.Equal(6, templates.Count);
        Assert.All(templates, t => Assert.Equal(userId, t.UserId));
        Assert.Equal(6, templates.Select(t => t.Id).Distinct().Count());
    }
}
