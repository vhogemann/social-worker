using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SkiaSharp;

namespace SocialWorker.Api.Features.CodeImages;

public sealed class CodeImageRenderer : IDisposable
{
    private const float FontSize = 14f;
    private const float LineHeightMultiplier = 1.6f;
    private const float HorizontalPadding = 44f;
    private const float VerticalPadding = 28f;
    private const float ChromeHeight = 52f;
    private const float LineNumWidth = 44f;
    private const float DotRadius = 6f;
    private const float DotY = ChromeHeight / 2f;
    private const float FirstDotX = 22f;
    private const float DotSpacing = 20f;
    private const float CornerRadius = 14f;
    private const float LangPadding = 12f;
    private const int MinWidth = 560;
    private const int MaxWidth = 1200;

    private readonly SKTypeface _typeface;

    public CodeImageRenderer()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "SocialWorker.Api.Features.CodeImages.Fonts.JetBrainsMono-Regular.ttf";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded font resource not found: {resourceName}");
        _typeface = SKTypeface.FromStream(stream);
    }

    public byte[] Render(CodeBlock block, CodeTheme theme)
    {
        var code = block.Code.Replace("\t", "    ");
        var runs = CodeTokenizer.Tokenize(code, block.Language, theme);
        var lines = SplitRunsIntoLines(runs);

        using var measurePaint = MakeTextPaint(theme.DefaultText);
        var charWidth = measurePaint.MeasureText("M");
        var fontMetrics = measurePaint.FontMetrics;
        var ascent = -fontMetrics.Ascent;
        var lineHeight = (float)Math.Ceiling((ascent + fontMetrics.Descent + fontMetrics.Leading) * LineHeightMultiplier);

        var maxLineChars = 0;
        foreach (var line in lines)
        {
            var lineLen = 0;
            foreach (var run in line) lineLen += run.Text.Length;
            if (lineLen > maxLineChars) maxLineChars = lineLen;
        }

        var textAreaWidth = maxLineChars * charWidth;
        var contentWidth = LineNumWidth + textAreaWidth + HorizontalPadding * 2;
        var canvasWidth = (int)Math.Clamp(Math.Ceiling(contentWidth), MinWidth, MaxWidth);
        var canvasHeight = (int)Math.Ceiling(ChromeHeight + VerticalPadding + lines.Count * lineHeight + VerticalPadding);

        using var bitmap = new SKBitmap(canvasWidth, canvasHeight);
        using var canvas = new SKCanvas(bitmap);

        canvas.Clear(SKColors.Transparent);

        DrawBackground(canvas, theme, canvasWidth, canvasHeight);
        DrawChrome(canvas, theme, canvasWidth, block.Language);
        DrawCode(canvas, lines, theme, measurePaint, lineHeight, ascent, canvasWidth, canvasHeight);

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static void DrawBackground(SKCanvas canvas, CodeTheme theme, int width, int height)
    {
        using var bgPaint = new SKPaint { Color = theme.Background, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(0, 0, width, height), CornerRadius), bgPaint);
    }

    private void DrawChrome(SKCanvas canvas, CodeTheme theme, int width, string language)
    {
        canvas.Save();

        using var clipPath = new SKPath();
        clipPath.AddRoundRect(new SKRoundRect(new SKRect(0, 0, width, ChromeHeight + CornerRadius), CornerRadius));
        canvas.ClipPath(clipPath, SKClipOperation.Intersect, antialias: true);

        using var chromePaint = new SKPaint { Color = theme.ChromeBackground, IsAntialias = true };
        canvas.DrawRect(0, 0, width, ChromeHeight + CornerRadius, chromePaint);

        canvas.Restore();

        // Separator line
        using var linePaint = new SKPaint { Color = theme.Background.WithAlpha(120), StrokeWidth = 1f };
        canvas.DrawLine(0, ChromeHeight, width, ChromeHeight, linePaint);

        // Window dots
        DrawDot(canvas, FirstDotX, DotY, DotRadius, theme.DotClose);
        DrawDot(canvas, FirstDotX + DotSpacing, DotY, DotRadius, theme.DotMinimize);
        DrawDot(canvas, FirstDotX + DotSpacing * 2, DotY, DotRadius, theme.DotExpand);

        // Language label
        if (!string.IsNullOrWhiteSpace(language))
        {
            using var langPaint = MakeTextPaint(theme.LineNumberText);
            langPaint.TextSize = 11f;
            var langText = language.ToUpperInvariant();
            var langWidth = langPaint.MeasureText(langText);
            canvas.DrawText(langText, width - langWidth - LangPadding, DotY + langPaint.TextSize * 0.35f, langPaint);
        }
    }

    private static void DrawDot(SKCanvas canvas, float x, float y, float radius, SKColor color)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawCircle(x, y, radius, paint);
    }

    private void DrawCode(
        SKCanvas canvas,
        List<List<ColoredRun>> lines,
        CodeTheme theme,
        SKPaint measurePaint,
        float lineHeight,
        float ascent,
        int canvasWidth,
        int canvasHeight)
    {
        var codeLeft = HorizontalPadding + LineNumWidth;
        var baselineY = ChromeHeight + VerticalPadding + ascent;

        for (int i = 0; i < lines.Count; i++)
        {
            var y = baselineY + i * lineHeight;

            // Line number
            using var numPaint = MakeTextPaint(theme.LineNumberText);
            var numText = (i + 1).ToString();
            var numWidth = numPaint.MeasureText(numText);
            canvas.DrawText(numText, HorizontalPadding + LineNumWidth - numWidth - 8f, y, numPaint);

            // Code runs
            var x = codeLeft;
            foreach (var run in lines[i])
            {
                if (run.Text.Length == 0) continue;
                using var textPaint = MakeTextPaint(run.Color);
                canvas.DrawText(run.Text, x, y, textPaint);
                x += measurePaint.MeasureText(run.Text);
            }
        }

        _ = canvasWidth;
        _ = canvasHeight;
    }

    private SKPaint MakeTextPaint(SKColor color) => new()
    {
        Color = color,
        Typeface = _typeface,
        TextSize = FontSize,
        IsAntialias = true,
        SubpixelText = true,
        LcdRenderText = true,
    };

    private static List<List<ColoredRun>> SplitRunsIntoLines(IReadOnlyList<ColoredRun> runs)
    {
        var lines = new List<List<ColoredRun>>();
        var current = new List<ColoredRun>();

        foreach (var run in runs)
        {
            var parts = run.Text.Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    current.Add(new ColoredRun(parts[i], run.Color));

                if (i < parts.Length - 1)
                {
                    lines.Add(current);
                    current = new List<ColoredRun>();
                }
            }
        }

        lines.Add(current);
        return lines;
    }

    public void Dispose() => _typeface.Dispose();
}
