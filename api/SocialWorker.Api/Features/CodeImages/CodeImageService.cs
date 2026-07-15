using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SocialWorker.Api.Features.Media;

namespace SocialWorker.Api.Features.CodeImages;

public sealed class CodeImageService
{
    private readonly MediaService _media;
    private readonly CodeImageRenderer _renderer;

    public CodeImageService(MediaService media, CodeImageRenderer renderer)
    {
        _media = media;
        _renderer = renderer;
    }

    public async Task<UploadMediaResult> RenderAndStoreAsync(
        Guid userId,
        Guid draftId,
        CodeBlock block,
        CodeTheme theme,
        CancellationToken ct)
    {
        var pngBytes = _renderer.Render(block, theme);

        var lang = string.IsNullOrWhiteSpace(block.Language) ? "code" : block.Language;
        var fileName = $"code-{lang}.png";
        var markdownLinkText = string.IsNullOrWhiteSpace(block.Language)
            ? "code snippet"
            : $"{block.Language.Trim().ToLowerInvariant()} code snippet";

        // Store the code fence as alt text for reversibility
        var codeFence = string.IsNullOrEmpty(block.Language)
            ? $"```\n{block.Code}\n```"
            : $"```{block.Language}\n{block.Code}\n```";

        using var stream = new MemoryStream(pngBytes);
        return await _media.UploadMediaAsync(
            userId,
            draftId,
            fileName,
            "image/png",
            stream,
            ct,
            altText: codeFence,
            markdownLinkText: markdownLinkText);
    }
}
