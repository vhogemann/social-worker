using System;
using System.Collections.Generic;
using ColorCode;
using ColorCode.Common;
using ColorCode.Parsing;
using ColorCode.Styling;
using SkiaSharp;

namespace SocialWorker.Api.Features.CodeImages;

public static class CodeTokenizer
{
    private static readonly Lazy<StyleDictionary> DarkStyles = new(BuildDarkStyles);
    private static readonly Lazy<StyleDictionary> LightStyles = new(BuildLightStyles);

    public static IReadOnlyList<ColoredRun> Tokenize(string code, string languageHint, CodeTheme theme)
    {
        var language = FindLanguage(languageHint);
        if (language == null)
            return [new ColoredRun(code, theme.DefaultText)];

        try
        {
            var styles = theme.Kind == CodeThemeKind.Dark ? DarkStyles.Value : LightStyles.Value;
            var colorizer = new SkiaColorizer(styles, theme.DefaultText);
            return colorizer.Colorize(code, language);
        }
        catch
        {
            return [new ColoredRun(code, theme.DefaultText)];
        }
    }

    private static ILanguage? FindLanguage(string? hint)
    {
        if (string.IsNullOrWhiteSpace(hint)) return null;

        var normalized = hint.Trim().ToLowerInvariant();

        try
        {
            var lang = Languages.FindById(normalized);
            if (lang != null) return lang;
        }
        catch { }

        var alias = normalized switch
        {
            "js" => "javascript",
            "ts" => "typescript",
            "py" => "python",
            "rb" => "ruby",
            "cs" or "csharp" or "c#" => "c#",
            "cpp" or "c++" => "c++",
            "sh" or "bash" or "shell" => "bash",
            "ps" or "powershell" or "ps1" => "powershell",
            "rs" => "rust",
            "go" or "golang" => "go",
            "kt" or "kotlin" => "kotlin",
            "fs" or "fsharp" or "f#" => "f#",
            "vb" => "vb.net",
            _ => null,
        };

        if (alias == null) return null;

        try { return Languages.FindById(alias); }
        catch { return null; }
    }

    private static StyleDictionary BuildDarkStyles()
    {
        var d = new StyleDictionary();
        d.Add(new Style(ScopeName.PlainText)            { Foreground = "#F8F8F2" });
        d.Add(new Style(ScopeName.Keyword)              { Foreground = "#FF79C6" });
        d.Add(new Style(ScopeName.String)               { Foreground = "#F1FA8C" });
        d.Add(new Style(ScopeName.StringCSharpVerbatim) { Foreground = "#F1FA8C" });
        d.Add(new Style(ScopeName.Comment)              { Foreground = "#6272A4" });
        d.Add(new Style(ScopeName.XmlDocComment)        { Foreground = "#6272A4" });
        d.Add(new Style(ScopeName.XmlDocTag)            { Foreground = "#6272A4" });
        d.Add(new Style(ScopeName.Number)               { Foreground = "#BD93F9" });
        d.Add(new Style(ScopeName.PreprocessorKeyword)  { Foreground = "#FF79C6" });
        d.Add(new Style(ScopeName.Type)                 { Foreground = "#8BE9FD" });
        d.Add(new Style(ScopeName.ClassName)            { Foreground = "#8BE9FD" });
        d.Add(new Style(ScopeName.Constructor)          { Foreground = "#50FA7B" });
        d.Add(new Style(ScopeName.Operator)             { Foreground = "#FF79C6" });
        d.Add(new Style(ScopeName.Delimiter)            { Foreground = "#F8F8F2" });
        d.Add(new Style(ScopeName.HtmlTagDelimiter)     { Foreground = "#FF79C6" });
        d.Add(new Style(ScopeName.HtmlElementName)      { Foreground = "#8BE9FD" });
        d.Add(new Style(ScopeName.HtmlAttributeName)    { Foreground = "#50FA7B" });
        d.Add(new Style(ScopeName.HtmlAttributeValue)   { Foreground = "#F1FA8C" });
        d.Add(new Style(ScopeName.HtmlComment)          { Foreground = "#6272A4" });
        d.Add(new Style(ScopeName.HtmlOperator)         { Foreground = "#FF79C6" });
        d.Add(new Style(ScopeName.CssSelector)          { Foreground = "#50FA7B" });
        d.Add(new Style(ScopeName.CssPropertyName)      { Foreground = "#8BE9FD" });
        d.Add(new Style(ScopeName.CssPropertyValue)     { Foreground = "#F1FA8C" });
        d.Add(new Style(ScopeName.JsonKey)              { Foreground = "#8BE9FD" });
        d.Add(new Style(ScopeName.JsonString)           { Foreground = "#F1FA8C" });
        d.Add(new Style(ScopeName.JsonNumber)           { Foreground = "#BD93F9" });
        d.Add(new Style(ScopeName.JsonConst)            { Foreground = "#BD93F9" });
        return d;
    }

    private static StyleDictionary BuildLightStyles()
    {
        var d = new StyleDictionary();
        d.Add(new Style(ScopeName.PlainText)            { Foreground = "#383A42" });
        d.Add(new Style(ScopeName.Keyword)              { Foreground = "#A626A4" });
        d.Add(new Style(ScopeName.String)               { Foreground = "#50A14F" });
        d.Add(new Style(ScopeName.StringCSharpVerbatim) { Foreground = "#50A14F" });
        d.Add(new Style(ScopeName.Comment)              { Foreground = "#A0A1A7" });
        d.Add(new Style(ScopeName.XmlDocComment)        { Foreground = "#A0A1A7" });
        d.Add(new Style(ScopeName.XmlDocTag)            { Foreground = "#A0A1A7" });
        d.Add(new Style(ScopeName.Number)               { Foreground = "#986801" });
        d.Add(new Style(ScopeName.PreprocessorKeyword)  { Foreground = "#A626A4" });
        d.Add(new Style(ScopeName.Type)                 { Foreground = "#0184BC" });
        d.Add(new Style(ScopeName.ClassName)            { Foreground = "#0184BC" });
        d.Add(new Style(ScopeName.Constructor)          { Foreground = "#4078F2" });
        d.Add(new Style(ScopeName.Operator)             { Foreground = "#A626A4" });
        d.Add(new Style(ScopeName.Delimiter)            { Foreground = "#383A42" });
        d.Add(new Style(ScopeName.HtmlTagDelimiter)     { Foreground = "#E45649" });
        d.Add(new Style(ScopeName.HtmlElementName)      { Foreground = "#E45649" });
        d.Add(new Style(ScopeName.HtmlAttributeName)    { Foreground = "#986801" });
        d.Add(new Style(ScopeName.HtmlAttributeValue)   { Foreground = "#50A14F" });
        d.Add(new Style(ScopeName.HtmlComment)          { Foreground = "#A0A1A7" });
        d.Add(new Style(ScopeName.HtmlOperator)         { Foreground = "#E45649" });
        d.Add(new Style(ScopeName.CssSelector)          { Foreground = "#E45649" });
        d.Add(new Style(ScopeName.CssPropertyName)      { Foreground = "#0184BC" });
        d.Add(new Style(ScopeName.CssPropertyValue)     { Foreground = "#50A14F" });
        d.Add(new Style(ScopeName.JsonKey)              { Foreground = "#0184BC" });
        d.Add(new Style(ScopeName.JsonString)           { Foreground = "#50A14F" });
        d.Add(new Style(ScopeName.JsonNumber)           { Foreground = "#986801" });
        d.Add(new Style(ScopeName.JsonConst)            { Foreground = "#986801" });
        return d;
    }
}

internal sealed class SkiaColorizer : CodeColorizerBase
{
    private readonly List<ColoredRun> _runs = new();
    private readonly SKColor _defaultColor;

    public SkiaColorizer(StyleDictionary styles, SKColor defaultColor)
        : base(styles, null)
    {
        _defaultColor = defaultColor;
    }

    public IReadOnlyList<ColoredRun> Colorize(string sourceCode, ILanguage language)
    {
        _runs.Clear();
        languageParser.Parse(sourceCode, language, Write);
        return _runs.AsReadOnly();
    }

    protected override void Write(string parsedSourceCode, IList<Scope> scopes)
    {
        if (scopes.Count == 0)
        {
            _runs.Add(new ColoredRun(parsedSourceCode, _defaultColor));
            return;
        }

        var segments = new List<(int Start, int End, SKColor Color)>();
        CollectLeafSegments(scopes, segments);
        segments.Sort((a, b) => a.Start.CompareTo(b.Start));

        int pos = 0;
        foreach (var (start, end, color) in segments)
        {
            var safeStart = Math.Min(start, parsedSourceCode.Length);
            var safeEnd = Math.Min(end, parsedSourceCode.Length);
            if (safeStart > pos)
                _runs.Add(new ColoredRun(parsedSourceCode[pos..safeStart], _defaultColor));
            if (safeEnd > safeStart)
                _runs.Add(new ColoredRun(parsedSourceCode[safeStart..safeEnd], color));
            pos = safeEnd;
        }
        if (pos < parsedSourceCode.Length)
            _runs.Add(new ColoredRun(parsedSourceCode[pos..], _defaultColor));
    }

    private void CollectLeafSegments(IList<Scope> scopes, List<(int, int, SKColor)> segments)
    {
        foreach (var scope in scopes)
        {
            var color = _defaultColor;
            if (Styles.Contains(scope.Name))
            {
                var style = Styles[scope.Name];
                if (!string.IsNullOrEmpty(style.Foreground))
                    color = TryParseColor(style.Foreground, _defaultColor);
            }

            if (scope.Children.Count == 0)
                segments.Add((scope.Index, scope.Index + scope.Length, color));
            else
                CollectLeafSegments(scope.Children, segments);
        }
    }

    private static SKColor TryParseColor(string hex, SKColor fallback)
    {
        try { return SKColor.Parse(hex); }
        catch { return fallback; }
    }
}
