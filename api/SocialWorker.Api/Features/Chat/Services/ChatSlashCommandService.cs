using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SocialWorker.Api.Features.Chat.Services;

public sealed class ChatSlashCommandService
{
    public bool TryParse(string text, out string command, out string args)
    {
        command = string.Empty;
        args = string.Empty;

        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith('/'))
        {
            return false;
        }

        var trimmed = text.Trim();
        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace < 0)
        {
            command = trimmed[1..].ToLowerInvariant();
            return true;
        }

        command = trimmed[1..firstSpace].ToLowerInvariant();
        args = trimmed[(firstSpace + 1)..].Trim();
        return true;
    }

    public async Task<string> ExecuteAsync(
        string command,
        string args,
        Guid? draftId,
        Guid userId,
        Func<string, string, Guid?, Guid, CancellationToken, Task<ToolExecutionResult>> executeTool,
        CancellationToken ct)
    {
        if (command == "help" || command == "tools")
        {
            var sb = new StringBuilder();
            sb.AppendLine("Available slash commands:");
            sb.AppendLine("- /validate");
            sb.AppendLine("- /search <query>");
            sb.AppendLine("- /search-image <query>");
            sb.AppendLine();
            sb.AppendLine("Use /search and /search-image for explicit query-driven tool execution.");
            return sb.ToString().Trim();
        }

        if (command == "validate")
        {
            var result = await executeTool("validate_draft", "{}", draftId, userId, ct);
            return result.ToDisplayText();
        }

        if (command == "search")
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                return "Usage: /search <query>. Example: /search latest Bluesky character limit";
            }

            var toolArgs = JsonSerializer.Serialize(new { query = args });
            var result = await executeTool("web_search", toolArgs, draftId, userId, ct);
            return result.ToDisplayText();
        }

        if (command == "search-image" || command == "images")
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                return "Usage: /search-image <query>. Example: /search-image pineapple product shots";
            }

            var toolArgs = JsonSerializer.Serialize(new { query = args });
            var result = await executeTool("image_search", toolArgs, draftId, userId, ct);
            return result.ToDisplayText();
        }

        return "Unknown slash command. Use /help to see available commands.";
    }
}
