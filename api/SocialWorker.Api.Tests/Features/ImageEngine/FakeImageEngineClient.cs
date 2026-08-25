using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.ImageEngine;

namespace SocialWorker.Api.Tests.Features.ImageEngine;

public sealed class FakeImageEngineClient : IImageEngineClient
{
    public byte[] DummyBytes { get; set; } = new byte[] { 1, 2, 3, 4 };
    public int ResizedWidth { get; set; } = 100;
    public int ResizedHeight { get; set; } = 100;

    public async Task<ImageTransformResult> ResizeImageAsync(
        Stream stream,
        string mimeType,
        int maxDimension = 1200,
        CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        return new ImageTransformResult(bytes.Length > 0 ? bytes : DummyBytes, ResizedWidth, ResizedHeight, mimeType);
    }

    public async Task<ImageTransformResult> FormatAspectRatioAsync(
        Stream stream,
        string mimeType,
        string targetRatio = "1:1",
        string fitMode = "blur_padding",
        CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        return new ImageTransformResult(bytes.Length > 0 ? bytes : DummyBytes, 1080, 1080, mimeType);
    }

    public Task<byte[]> RenderTemplateAsync(
        string templateJson,
        IDictionary<string, string>? slots = null,
        IDictionary<string, (Stream Stream, string FileName, string MimeType)>? images = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(DummyBytes);
    }

    public Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        return Task.FromResult(true);
    }
}
