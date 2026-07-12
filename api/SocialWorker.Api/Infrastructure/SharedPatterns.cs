using System.Text.RegularExpressions;

namespace SocialWorker.Api.Infrastructure;

public static class SharedPatterns
{
    public static readonly Regex MediaRegex = MediaRegexCompiled();

    private static Regex MediaRegexCompiled() =>
        new(@"!\[(.*?)\]\(media://([0-9a-fA-F\-]{36})\)", RegexOptions.Compiled);
}