using System.Text.RegularExpressions;

namespace SocialWorker.Api.Infrastructure;

public sealed record MediaReference(Guid MediaId, string AltText);

public static class SharedPatterns
{
    public static readonly Regex MediaRegex = MediaRegexCompiled();
    public static readonly Regex MediaUriRegex = MediaUriRegexCompiled();
    public static readonly Regex DanglingMediaLinkRegex = DanglingMediaLinkRegexCompiled();
    public static readonly Regex YoutubeMarkdownRegex = YoutubeMarkdownRegexCompiled();

    public static string StripMediaMarkdown(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        var stripped = MediaRegex.Replace(content, "");
        stripped = DanglingMediaLinkRegex.Replace(stripped, "");
        stripped = MediaUriRegex.Replace(stripped, "");
        return stripped;
    }

    public static List<MediaReference> ExtractMediaReferences(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return new List<MediaReference>();
        }

        var references = new List<MediaReference>();
        foreach (Match match in MediaRegex.Matches(content))
        {
            if (Guid.TryParse(match.Groups[2].Value, out var mediaId))
            {
                references.Add(new MediaReference(mediaId, match.Groups[1].Value));
            }
        }

        return references;
    }

    public static int CountMediaReferences(string content)
    {
        return ExtractMediaReferences(content).Count;
    }

    private static Regex MediaRegexCompiled() =>
        new(@"!\[([\s\S]*?)\]\(media://([0-9a-fA-F\-]{36})\)", RegexOptions.Compiled);

    private static Regex MediaUriRegexCompiled() =>
        new(@"media://([0-9a-fA-F\-]{36})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static Regex DanglingMediaLinkRegexCompiled() =>
        new(@"\]?\(media://([0-9a-fA-F\-]{36})\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static Regex YoutubeMarkdownRegexCompiled() =>
        new(@"!\[([\s\S]*?)\]\((https?://(?:www\.)?(?:youtube\.com/watch[^)]*|youtu\.be/[^)]+))\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
}