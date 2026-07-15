namespace SocialWorker.Api.Features.Chat.Models;

public interface IChatToolResult
{
    string ToDisplayText();
}

public interface IChatBlockingValidationResult : IChatToolResult
{
    bool HasBlockingErrors { get; }
}
