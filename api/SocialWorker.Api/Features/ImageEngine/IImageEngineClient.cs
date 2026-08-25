using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.ImageEngine;

public interface IImageEngineClient
{
    Task<ImageTransformResult> ResizeImageAsync(
        Stream stream,
        string mimeType,
        int maxDimension = 1200,
        CancellationToken ct = default);

    Task<ImageTransformResult> FormatAspectRatioAsync(
        Stream stream,
        string mimeType,
        string targetRatio = "1:1",
        string fitMode = "blur_padding",
        CancellationToken ct = default);

    Task<byte[]> RenderTemplateAsync(
        string templateJson,
        IDictionary<string, string>? slots = null,
        IDictionary<string, (Stream Stream, string FileName, string MimeType)>? images = null,
        CancellationToken ct = default);

    Task<bool> HealthCheckAsync(CancellationToken ct = default);
}
