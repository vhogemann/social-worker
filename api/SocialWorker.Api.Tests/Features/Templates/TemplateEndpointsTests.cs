using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data.Entities;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Templates;

public sealed class TemplateEndpointsTests : SqliteTestBase
{
    [Fact]
    public async Task CanAddAndQueryTemplate()
    {
        await using var db = CreateDbContext();

        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Instagram Quote Card",
            Description = "Square quote card with photo background",
            Width = 1080,
            Height = 1080,
            Schema = "{\"name\":\"quote\",\"layers\":[]}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var queried = await db.Templates.FirstOrDefaultAsync(t => t.Id == template.Id);

        Assert.NotNull(queried);
        Assert.Equal("Instagram Quote Card", queried.Name);
        Assert.Equal(1080, queried.Width);
        Assert.Equal(1080, queried.Height);
    }

    [Fact]
    public async Task CanUpdateTemplate()
    {
        await using var db = CreateDbContext();

        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Width = 1080,
            Height = 1080,
            Schema = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Templates.Add(template);
        await db.SaveChangesAsync();

        template.Name = "Updated Name";
        template.Description = "Updated description";
        template.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var queried = await db.Templates.FirstOrDefaultAsync(t => t.Id == template.Id);
        Assert.NotNull(queried);
        Assert.Equal("Updated Name", queried.Name);
        Assert.Equal("Updated description", queried.Description);
    }

    [Fact]
    public async Task CanDeleteTemplate()
    {
        await using var db = CreateDbContext();

        var template = new Template
        {
            Id = Guid.NewGuid(),
            Name = "To Delete",
            Width = 1080,
            Height = 1080,
            Schema = "{}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Templates.Add(template);
        await db.SaveChangesAsync();

        db.Templates.Remove(template);
        await db.SaveChangesAsync();

        var queried = await db.Templates.FirstOrDefaultAsync(t => t.Id == template.Id);
        Assert.Null(queried);
    }
}
