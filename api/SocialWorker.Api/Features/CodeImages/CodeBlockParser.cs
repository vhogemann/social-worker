using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SocialWorker.Api.Features.CodeImages;

public static class CodeBlockParser
{
    private static readonly Regex FenceRegex = new(
        @"```(\w*)\r?\n([\s\S]*?)```",
        RegexOptions.Compiled);

    public static IReadOnlyList<CodeBlock> Parse(string markdown)
    {
        var result = new List<CodeBlock>();
        foreach (Match m in FenceRegex.Matches(markdown))
        {
            var lang = m.Groups[1].Value.Trim();
            var code = m.Groups[2].Value.TrimEnd('\r', '\n');
            result.Add(new CodeBlock(lang, code));
        }
        return result;
    }
}
