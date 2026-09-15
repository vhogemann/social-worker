using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Data.Seed;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Templates;

public sealed class TemplateEndpointsTests : SqliteTestBase
{
    [Fact]
    public async Task CanAddAndQueryTemplateForUser()
    {
        await using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var template = Template.Create(
            user.Id,
            "Instagram Quote Card",
            "Square quote card with photo background",
            1080,
            1080,
            "{\"name\":\"quote\",\"layers\":[]}");

        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var queried = await db.Templates.FirstOrDefaultAsync(t => t.Id == template.Id && t.UserId == user.Id);

        Assert.NotNull(queried);
        Assert.Equal("Instagram Quote Card", queried.Name);
        Assert.Equal(user.Id, queried.UserId);
        Assert.Equal(1080, queried.Width);
        Assert.Equal(1080, queried.Height);
    }

    [Fact]
    public async Task TemplatesAreScopedToOwningUser()
    {
        await using var db = CreateDbContext();
        var userA = CreateSeedUser(db, "userA", "userA@test.com");
        var userB = CreateSeedUser(db, "userB", "userB@test.com");

        var templateA = Template.Create(userA.Id, "User A Template", null, 1080, 1080, "{}");
        var templateB = Template.Create(userB.Id, "User B Template", null, 1080, 1080, "{}");

        db.Templates.AddRange(templateA, templateB);
        await db.SaveChangesAsync();

        var userATemplates = await db.Templates.Where(t => t.UserId == userA.Id).ToListAsync();
        Assert.Single(userATemplates);
        Assert.Equal("User A Template", userATemplates[0].Name);

        var userBTemplates = await db.Templates.Where(t => t.UserId == userB.Id).ToListAsync();
        Assert.Single(userBTemplates);
        Assert.Equal("User B Template", userBTemplates[0].Name);
    }

    [Fact]
    public async Task CanUpdateTemplate()
    {
        await using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var template = Template.Create(user.Id, "Original Name", null, 1080, 1080, "{}");

        db.Templates.Add(template);
        await db.SaveChangesAsync();

        template.Name = "Updated Name";
        template.Description = "Updated description";
        template.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var queried = await db.Templates.FirstOrDefaultAsync(t => t.Id == template.Id && t.UserId == user.Id);
        Assert.NotNull(queried);
        Assert.Equal("Updated Name", queried.Name);
        Assert.Equal("Updated description", queried.Description);
    }

    [Fact]
    public async Task CanDeleteTemplate()
    {
        await using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var template = Template.Create(user.Id, "To Delete", null, 1080, 1080, "{}");

        db.Templates.Add(template);
        await db.SaveChangesAsync();

        db.Templates.Remove(template);
        await db.SaveChangesAsync();

        var queried = await db.Templates.FirstOrDefaultAsync(t => t.Id == template.Id);
        Assert.Null(queried);
    }

    [Fact]
    public async Task CanSeedDefaultTemplatesForUserAndNotDuplicate()
    {
        await using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var defaultDefs = TemplateSeedData.GetDefaultDefinitions();
        var toAdd = defaultDefs
            .Select(d => Template.Create(user.Id, d.Name, d.Description, d.Width, d.Height, d.Schema))
            .ToList();

        db.Templates.AddRange(toAdd);
        await db.SaveChangesAsync();

        var count = await db.Templates.CountAsync(t => t.UserId == user.Id);
        Assert.Equal(6, count);

        // Attempting to re-seed missing defaults when all 6 exist does not add duplicates
        var existingNames = await db.Templates
            .Where(t => t.UserId == user.Id)
            .Select(t => t.Name.ToLower())
            .ToListAsync();

        var missing = defaultDefs
            .Where(d => !existingNames.Contains(d.Name.ToLower()))
            .Select(d => Template.Create(user.Id, d.Name, d.Description, d.Width, d.Height, d.Schema))
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public async Task ReSeedDefaultTemplatesRestoresOnlyMissingTemplates()
    {
        await using var db = CreateDbContext();
        var user = CreateSeedUser(db);

        var allDefaults = TemplateSeedData.CreateDefaultTemplatesForUser(user.Id);
        db.Templates.AddRange(allDefaults);
        await db.SaveChangesAsync();

        // User deletes one template (e.g. "Announcement Card")
        var toDelete = await db.Templates.FirstAsync(t => t.UserId == user.Id && t.Name == "Announcement Card");
        db.Templates.Remove(toDelete);
        await db.SaveChangesAsync();

        Assert.Equal(5, await db.Templates.CountAsync(t => t.UserId == user.Id));

        // Re-seeding finds the missing "Announcement Card"
        var defaultDefs = TemplateSeedData.GetDefaultDefinitions();
        var existingNames = await db.Templates
            .Where(t => t.UserId == user.Id)
            .Select(t => t.Name.ToLower())
            .ToListAsync();

        var missing = defaultDefs
            .Where(d => !existingNames.Contains(d.Name.ToLower()))
            .Select(d => Template.Create(user.Id, d.Name, d.Description, d.Width, d.Height, d.Schema))
            .ToList();

        Assert.Single(missing);
        Assert.Equal("Announcement Card", missing[0].Name);

        db.Templates.AddRange(missing);
        await db.SaveChangesAsync();

        Assert.Equal(6, await db.Templates.CountAsync(t => t.UserId == user.Id));
    }

    [Fact]
    public void GenerateSampleSlots_ExtractsSlotsFromSchema()
    {
        var schema = """
        {
          "name": "Test",
          "slots": [
            { "name": "quote_text", "type": "text" },
            { "name": "author", "type": "text" },
            { "name": "metric_val", "type": "text" },
            { "name": "custom_field", "type": "text", "defaultValue": "Fixed Default" }
          ]
        }
        """;

        var slots = SocialWorker.Api.Features.Templates.Services.TemplateGeneratorService.GenerateSampleSlots(schema);

        Assert.Equal("Fixed Default", slots["custom_field"]);
        Assert.Equal("Simplicity is prerequisite for reliability.", slots["quote_text"]);
        Assert.Equal("Edsger W. Dijkstra", slots["author"]);
        Assert.Equal("94.8%", slots["metric_val"]);
    }

    [Fact]
    public void GenerateSampleSlots_HandlesEmptyOrInvalidSchemaGracefully()
    {
        var slotsEmpty = SocialWorker.Api.Features.Templates.Services.TemplateGeneratorService.GenerateSampleSlots("");
        Assert.NotNull(slotsEmpty);

        var slotsInvalid = SocialWorker.Api.Features.Templates.Services.TemplateGeneratorService.GenerateSampleSlots("invalid json");
        Assert.NotNull(slotsInvalid);
    }
}
