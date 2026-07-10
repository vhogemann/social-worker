using System.Collections.Generic;
using System.Text;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Features.Chat;

public sealed class SystemPromptBuilder
{
    private static string GetBasePrompt()
    {
        const string defaultPrompt = "You are a helpful assistant that helps the user draft social media threads. "
          + "When the user asks you to write or update content, call replace_editor_content with the full markdown. "
          + "Use --- on its own line to separate thread segments (each segment is one post).\n"
          + "You have access to reference sources (attached files or URLs detected in the draft). "
          + "To view the list of available sources, call the 'list_sources' tool. "
          + "To read the actual cached text content of a source, call the 'fetch_source' tool with its Guid ID. "
          + "If the user asks you to summarize, explain, or draft posts based on a link or file attachment, you MUST first call 'list_sources' to locate it, and then call 'fetch_source' with the corresponding ID to read the source text before responding.";

        try
        {
            var paths = new[]
            {
                "SYSTEM_PROMPT.md",
                "/app/SYSTEM_PROMPT.md",
                "../SYSTEM_PROMPT.md"
            };

            foreach (var path in paths)
            {
                if (System.IO.File.Exists(path))
                {
                    return System.IO.File.ReadAllText(path);
                }
            }
        }
        catch
        {
            // Fallback
        }

        return defaultPrompt;
    }

    public string Build(string? customSystemPrompt, string editorContent, List<MediaAsset> mediaAssets, bool supportsVision)
    {
        var sb = new StringBuilder();

        var basePrompt = !string.IsNullOrWhiteSpace(customSystemPrompt)
            ? customSystemPrompt
            : GetBasePrompt();

        sb.Append(basePrompt);

        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("--- EDITOR CONTENT START ---");
        sb.AppendLine(string.IsNullOrEmpty(editorContent) ? "(editor is currently empty)" : editorContent);
        sb.Append("--- EDITOR CONTENT END ---");

        var imagesMetadata = BuildImagesMetadata(mediaAssets);
        if (!string.IsNullOrEmpty(imagesMetadata))
        {
            sb.Append(imagesMetadata);
        }

        sb.AppendLine();
        sb.AppendLine();
        if (supportsVision)
        {
            sb.Append("You have access to view the attached images using the 'view_image' tool. If the user asks you to describe, analyze, or draft content based on an attached image, or if the image alt text is empty (alt: \"\"), you MUST call the 'view_image' tool with the image's Guid ID to inspect its visual content before responding.");
        }
        else
        {
            sb.Append("Vision is not available with the current model. You can still reference images by their metadata (filename, dimensions).");
        }

        return sb.ToString();
    }

    private static string BuildImagesMetadata(List<MediaAsset> mediaAssets)
    {
        if (mediaAssets == null || mediaAssets.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("--- ATTACHED IMAGES ---");
        int idx = 1;
        foreach (var asset in mediaAssets)
        {
            sb.AppendLine($"{idx++}. {asset.FileName} (media://{asset.Id}) - {asset.Width}x{asset.Height}, {asset.SizeBytes / 1024} KB, alt: \"{asset.AltText}\"");
        }

        return sb.ToString().TrimEnd();
    }
}
