namespace SocialWorker.Api.Features.Chat;

public sealed class ChatOptions
{
    public const string SectionName = "Chat";

    public bool StrictEditorUpdateEnforcement { get; set; } = true;
    public int MaxToolExecutionRounds { get; set; } = 8;
}
