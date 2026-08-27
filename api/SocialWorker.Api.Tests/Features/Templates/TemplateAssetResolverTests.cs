using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Templates.Services;
using SocialWorker.Api.Tests.Features.ImageEngine;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Templates;

public sealed class TemplateAssetResolverTests : SqliteTestBase
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

    [Fact]
    public void ExtractMediaId_ParsesVariousFormats()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id, TemplateAssetResolver.ExtractMediaId($"/api/media/{id}"));
        Assert.Equal(id, TemplateAssetResolver.ExtractMediaId($"/api/media/{id}/"));
        Assert.Equal(id, TemplateAssetResolver.ExtractMediaId($"media://{id}"));
        Assert.Equal(id, TemplateAssetResolver.ExtractMediaId($"file://{id}"));
        Assert.Equal(id, TemplateAssetResolver.ExtractMediaId(id.ToString()));
        Assert.Null(TemplateAssetResolver.ExtractMediaId("https://example.com/image.png"));
        Assert.Null(TemplateAssetResolver.ExtractMediaId(""));
        Assert.Null(TemplateAssetResolver.ExtractMediaId(null!));
    }

    [Fact]
    public async Task ResolveTemplateImagesAsync_Resolves_Fixed_Src_Layers()
    {
        await using var db = CreateDbContext();
        db.Database.EnsureCreated();

        var imageEngine = new FakeImageEngineClient();
        var storage = new FileStorageProvider();
        var mediaService = new MediaService(db, imageEngine, storage);

        var asset = await mediaService.UploadTemplateAssetAsync(new MemoryStream(TinyPng), "watermark.png", "image/png", CancellationToken.None);

        var schema = $$"""
        {
          "name": "Branded Card",
          "layers": [
            { "type": "rect", "fill": "#ffffff" },
            { "type": "image", "src": "/api/media/{{asset.Id}}", "x": 10, "y": 10, "w": 100, "h": 100 }
          ]
        }
        """;

        var images = new Dictionary<string, (Stream Stream, string FileName, string MimeType)>();
        var disposables = new List<IDisposable>();

        try
        {
            await TemplateAssetResolver.ResolveTemplateImagesAsync(schema, null, images, disposables, mediaService, CancellationToken.None);

            Assert.True(images.ContainsKey($"/api/media/{asset.Id}"));
            Assert.NotEmpty(disposables);
        }
        finally
        {
            foreach (var d in disposables) d.Dispose();
        }
    }
}
