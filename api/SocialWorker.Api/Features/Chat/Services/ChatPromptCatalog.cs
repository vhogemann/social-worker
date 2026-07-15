using System;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SocialWorker.Api.Features.Chat.Services;

public sealed class ChatPromptCatalog
{
    private static readonly Lazy<ChatPromptCatalogData> Cached = new(LoadCatalog);

    public static ChatPromptCatalogData Current => Cached.Value;

    private static ChatPromptCatalogData LoadCatalog()
    {
        var defaults = ChatPromptCatalogData.Default();

        try
        {
            var paths = new[]
            {
                "CHAT_PROMPTS.yaml",
                "/app/CHAT_PROMPTS.yaml",
                "../CHAT_PROMPTS.yaml"
            };

            string? foundPath = null;
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    foundPath = path;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(foundPath))
            {
                return defaults;
            }

            var yaml = File.ReadAllText(foundPath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var parsed = deserializer.Deserialize<ChatPromptCatalogData>(yaml);
            if (parsed?.Chat == null)
            {
                return defaults;
            }

            parsed.Chat.SystemPromptPath = string.IsNullOrWhiteSpace(parsed.Chat.SystemPromptPath)
                ? defaults.Chat.SystemPromptPath
                : parsed.Chat.SystemPromptPath;

            parsed.Chat.EditorUpdateEnforcement = string.IsNullOrWhiteSpace(parsed.Chat.EditorUpdateEnforcement)
                ? defaults.Chat.EditorUpdateEnforcement
                : parsed.Chat.EditorUpdateEnforcement;

            parsed.Chat.VisionEnabledInstruction = string.IsNullOrWhiteSpace(parsed.Chat.VisionEnabledInstruction)
                ? defaults.Chat.VisionEnabledInstruction
                : parsed.Chat.VisionEnabledInstruction;

            parsed.Chat.VisionDisabledInstruction = string.IsNullOrWhiteSpace(parsed.Chat.VisionDisabledInstruction)
                ? defaults.Chat.VisionDisabledInstruction
                : parsed.Chat.VisionDisabledInstruction;

            return parsed;
        }
        catch
        {
            return defaults;
        }
    }
}

public sealed class ChatPromptCatalogData
{
    public ChatPromptSection Chat { get; set; } = new();

    public static ChatPromptCatalogData Default()
    {
        return new ChatPromptCatalogData
        {
            Chat = new ChatPromptSection
            {
                SystemPromptPath = "SYSTEM_PROMPT.md",
                EditorUpdateEnforcement = "EDITOR-UPDATE ENFORCEMENT: The user's request requires directly updating draft content. You must call replace_editor_content with the full updated markdown, then call validate_draft. Do not ask for confirmation and do not only describe changes.",
                VisionEnabledInstruction = "You have access to view the attached images using the 'view_image' tool. If the user asks you to describe, analyze, or draft content based on an attached image, or if the image alt text is empty (alt: \"\"), you MUST call the 'view_image' tool with the image's Guid ID to inspect its visual content before responding.",
                VisionDisabledInstruction = "Vision is not available with the current model. You can still reference images by their metadata (filename, dimensions)."
            }
        };
    }
}

public sealed class ChatPromptSection
{
    public string SystemPromptPath { get; set; } = "SYSTEM_PROMPT.md";
    public string EditorUpdateEnforcement { get; set; } = string.Empty;
    public string VisionEnabledInstruction { get; set; } = string.Empty;
    public string VisionDisabledInstruction { get; set; } = string.Empty;
}
