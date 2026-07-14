using System.Text.RegularExpressions;

namespace SocialWorker.Api.Infrastructure;

public static class SharedPatterns
{
    public static readonly Regex MediaRegex = MediaRegexCompiled();
    public static readonly Regex YoutubeMarkdownRegex = YoutubeMarkdownRegexCompiled();

    private static Regex MediaRegexCompiled() =>
        new(@"!\[(.*?)\]\(media://([0-9a-fA-F\-]{36})\)", RegexOptions.Compiled);

    private static Regex YoutubeMarkdownRegexCompiled() =>
        new(@"!\[(.*?)\]\((https?://(?:www\.)?(?:youtube\.com/watch[^)]*|youtu\.be/[^)]+))\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
}