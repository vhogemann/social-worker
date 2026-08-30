using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Publishing.Bluesky;
using SocialWorker.Api.Infrastructure.Markdown;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing;

public sealed class BlueskyContentPreparationServiceTests : SqliteTestBase
{
    [Fact]
    public void EnsureUnderBlueskyBlobSizeLimit_CompressesOversizedImage()
    {
        var random = new Random(42);
        using var bitmap = new SkiaSharp.SKBitmap(1000, 1000);
        for (int y = 0; y < 1000; y++)
        {
            for (int x = 0; x < 1000; x++)
            {
                bitmap.SetPixel(x, y, new SkiaSharp.SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)));
            }
        }

        using var img = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var largeBytes = data.ToArray();
        Assert.True(largeBytes.Length > 1_000_000, $"Test image length was {largeBytes.Length}, expected > 1MB");

        var (compressedBytes, mimeType) = BlueskyContentPreparationService.EnsureUnderBlueskyBlobSizeLimit(largeBytes, "image/png");

        Assert.True(compressedBytes.Length <= 950_000, $"Compressed bytes ({compressedBytes.Length}) must be <= 950KB");
        Assert.Equal("image/jpeg", mimeType);
    }


    [Fact]
    public void EnsureUnderBlueskyBlobSizeLimit_LeavesSmallImageUntouched()
    {
        var smallBytes = new byte[1000];
        var (resultBytes, mimeType) = BlueskyContentPreparationService.EnsureUnderBlueskyBlobSizeLimit(smallBytes, "image/png");

        Assert.Same(smallBytes, resultBytes);
        Assert.Equal("image/png", mimeType);
    }
}
