using System;
using System.Text.RegularExpressions;

namespace SocialWorker.Api.Features.Publishing.Twitter;

public static class TwitterTextWeightCalculator
{
    public const int MaxUnits = 280;
    public const int UrlWeight = 23;

    private static readonly Regex UrlRegex = new(@"https?://[^\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static int CalculateWeight(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int totalWeight = 0;

        var matches = UrlRegex.Matches(text);
        totalWeight += matches.Count * UrlWeight;

        var textWithoutUrls = UrlRegex.Replace(text, string.Empty);

        for (int i = 0; i < textWithoutUrls.Length; i++)
        {
            int codePoint;
            if (char.IsSurrogatePair(textWithoutUrls, i))
            {
                codePoint = char.ConvertToUtf32(textWithoutUrls, i);
                i++;
            }
            else
            {
                codePoint = textWithoutUrls[i];
            }

            totalWeight += GetCodePointWeight(codePoint);
        }

        return totalWeight;
    }

    public static int GetRemainingUnits(string? text) => MaxUnits - CalculateWeight(text);

    public static bool IsValid(string? text) => CalculateWeight(text) <= MaxUnits;

    private static int GetCodePointWeight(int codePoint)
    {
        if (codePoint <= 0x10FF)
        {
            return 1;
        }

        if (codePoint >= 0x2000 && codePoint <= 0x200D)
        {
            return 1;
        }

        if (codePoint >= 0x2010 && codePoint <= 0x201F)
        {
            return 1;
        }

        if (codePoint >= 0x2032 && codePoint <= 0x2037)
        {
            return 1;
        }

        return 2;
    }
}
