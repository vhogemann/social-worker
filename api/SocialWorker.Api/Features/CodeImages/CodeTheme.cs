using SkiaSharp;

namespace SocialWorker.Api.Features.CodeImages;

public enum CodeThemeKind { Dark, Light }

public sealed class CodeTheme
{
    public CodeThemeKind Kind { get; init; }
    public SKColor Background { get; init; }
    public SKColor ChromeBackground { get; init; }
    public SKColor DefaultText { get; init; }
    public SKColor LineNumberText { get; init; }
    public SKColor DotClose { get; init; }
    public SKColor DotMinimize { get; init; }
    public SKColor DotExpand { get; init; }

    public static readonly CodeTheme Dark = new()
    {
        Kind = CodeThemeKind.Dark,
        Background = SKColor.Parse("#282A36"),
        ChromeBackground = SKColor.Parse("#21222C"),
        DefaultText = SKColor.Parse("#F8F8F2"),
        LineNumberText = SKColor.Parse("#6272A4"),
        DotClose = SKColor.Parse("#FF5F57"),
        DotMinimize = SKColor.Parse("#FEBC2E"),
        DotExpand = SKColor.Parse("#28C840"),
    };

    public static readonly CodeTheme Light = new()
    {
        Kind = CodeThemeKind.Light,
        Background = SKColor.Parse("#F8F8F8"),
        ChromeBackground = SKColor.Parse("#E8E8E8"),
        DefaultText = SKColor.Parse("#383A42"),
        LineNumberText = SKColor.Parse("#A0A1A7"),
        DotClose = SKColor.Parse("#FF5F57"),
        DotMinimize = SKColor.Parse("#FEBC2E"),
        DotExpand = SKColor.Parse("#28C840"),
    };

    public static CodeTheme FromString(string? name) => name?.ToLowerInvariant() switch
    {
        "light" => Light,
        _ => Dark,
    };
}
