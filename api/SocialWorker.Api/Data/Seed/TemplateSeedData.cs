using System;
using System.Collections.Generic;
using SocialWorker.Api.Data.Entities;

namespace SocialWorker.Api.Data.Seed;

public static class TemplateSeedData
{
    public static readonly Guid QuoteCardSquareId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid QuoteCardPortraitId = Guid.Parse("a1000000-0000-0000-0000-000000000002");
    public static readonly Guid StatHighlightId = Guid.Parse("a1000000-0000-0000-0000-000000000003");
    public static readonly Guid AnnouncementCardId = Guid.Parse("a1000000-0000-0000-0000-000000000004");
    public static readonly Guid CarouselSlideId = Guid.Parse("a1000000-0000-0000-0000-000000000005");
    public static readonly Guid CodeSnippetCardId = Guid.Parse("a1000000-0000-0000-0000-000000000006");

    public static List<Template> GetDefaultTemplates()
    {
        var now = DateTime.UtcNow;

        return new List<Template>
        {
            new Template
            {
                Id = QuoteCardSquareId,
                Name = "Quote Card (Square)",
                Description = "Modern square 1:1 quote card with optional background image, dark scrim, quote text, author attribution, and tagline.",
                Width = 1080,
                Height = 1080,
                Schema = """
                {
                  "name": "Quote Card (Square)",
                  "width": 1080,
                  "height": 1080,
                  "slots": [
                    { "name": "background_image", "type": "image", "required": false, "description": "Optional background photo" },
                    { "name": "quote_text", "type": "text", "required": true, "description": "Main quote text" },
                    { "name": "author", "type": "text", "required": false, "description": "Author name or attribution" },
                    { "name": "tagline", "type": "text", "required": false, "description": "Category or topic tagline" }
                  ],
                  "layers": [
                    { "type": "rect", "fill": "#0f172a", "x": 0, "y": 0, "w": "100%", "h": "100%" },
                    { "type": "image", "slot": "background_image", "fit": "cover", "x": 0, "y": 0, "w": "100%", "h": "100%" },
                    { "type": "rect", "fill": "rgba(15, 23, 42, 0.75)", "x": 0, "y": 0, "w": "100%", "h": "100%" },
                    { "type": "rect", "fill": "#38bdf8", "x": 100, "y": 140, "w": 48, "h": 6, "borderRadius": 3 },
                    { "type": "text", "slot": "tagline", "x": 160, "y": 132, "fontSize": 20, "fontFamily": "Inter, sans-serif", "fontWeight": "bold", "color": "#38bdf8", "textTransform": "uppercase" },
                    { "type": "text", "slot": "quote_text", "x": 100, "y": 280, "maxWidth": 880, "fontSize": 44, "fontFamily": "Inter, sans-serif", "fontWeight": "bold", "lineHeight": 1.35, "color": "#f8fafc", "align": "left" },
                    { "type": "rect", "fill": "rgba(255, 255, 255, 0.15)", "x": 100, "y": 880, "w": 880, "h": 2 },
                    { "type": "text", "slot": "author", "x": 100, "y": 920, "fontSize": 28, "fontFamily": "Inter, sans-serif", "fontWeight": "normal", "color": "#94a3b8", "prefix": "— " }
                  ]
                }
                """,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Template
            {
                Id = QuoteCardPortraitId,
                Name = "Quote Card (Portrait)",
                Description = "Instagram-optimized 4:5 vertical quote card with high legibility, typography scale, and author footer.",
                Width = 1080,
                Height = 1350,
                Schema = """
                {
                  "name": "Quote Card (Portrait)",
                  "width": 1080,
                  "height": 1350,
                  "slots": [
                    { "name": "background_image", "type": "image", "required": false, "description": "Optional background photo" },
                    { "name": "quote_text", "type": "text", "required": true, "description": "Main quote text" },
                    { "name": "author", "type": "text", "required": false, "description": "Author name or handle" },
                    { "name": "category", "type": "text", "required": false, "description": "Badge category label" }
                  ],
                  "layers": [
                    { "type": "rect", "fill": "#18181b", "x": 0, "y": 0, "w": "100%", "h": "100%" },
                    { "type": "image", "slot": "background_image", "fit": "cover", "x": 0, "y": 0, "w": "100%", "h": "100%" },
                    { "type": "rect", "fill": "rgba(0, 0, 0, 0.7)", "x": 0, "y": 0, "w": "100%", "h": "100%" },
                    { "type": "rect", "fill": "#27272a", "stroke": "#3f3f46", "strokeWidth": 2, "x": 100, "y": 140, "w": 200, "h": 44, "borderRadius": 22 },
                    { "type": "text", "slot": "category", "x": 130, "y": 150, "fontSize": 18, "fontFamily": "Inter, sans-serif", "fontWeight": "bold", "color": "#a1a1aa", "textTransform": "uppercase" },
                    { "type": "text", "slot": "quote_text", "x": 100, "y": 380, "maxWidth": 880, "fontSize": 52, "fontFamily": "Inter, sans-serif", "fontWeight": "bold", "lineHeight": 1.35, "color": "#ffffff", "align": "left" },
                    { "type": "text", "slot": "author", "x": 100, "y": 1150, "fontSize": 32, "fontFamily": "Inter, sans-serif", "color": "#e4e4e7", "prefix": "— " }
                  ]
                }
                """,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Template
            {
                Id = StatHighlightId,
                Name = "Stat Highlight",
                Description = "High-impact metric card with large KPI value, contextual label, trend indicator, and footnote.",
                Width = 1080,
                Height = 1080,
                Schema = """
                {
                  "name": "Stat Highlight",
                  "width": 1080,
                  "height": 1080,
                  "slots": [
                    { "name": "metric_value", "type": "text", "required": true, "description": "Big KPI number or statistic (e.g. 94%, $1.2M, 10x)" },
                    { "name": "metric_label", "type": "text", "required": true, "description": "Metric description or title" },
                    { "name": "trend_text", "type": "text", "required": false, "description": "Trend or growth badge (e.g. +42% YoY)" },
                    { "name": "footnote", "type": "text", "required": false, "description": "Context footnote or source attribution" }
                  ],
                  "layers": [
                    { "type": "rect", "fill": "#030712", "x": 0, "y": 0, "w": "100%", "h": "100%" },
                    { "type": "rect", "fill": "#111827", "stroke": "#1f2937", "strokeWidth": 2, "x": 80, "y": 80, "w": 920, "h": 920, "borderRadius": 32 },
                    { "type": "text", "slot": "trend_text", "x": 140, "y": 180, "fontSize": 24, "fontFamily": "Inter, sans-serif", "fontWeight": "bold", "color": "#10b981" },
                    { "type": "text", "slot": "metric_value", "x": 140, "y": 280, "maxWidth": 800, "fontSize": 120, "fontFamily": "Inter, sans-serif", "fontWeight": "bold", "color": "#60a5fa" },
                    { "type": "text", "slot": "metric_label", "x": 140, "y": 480, "maxWidth": 800, "fontSize": 38, "fontFamily": "Inter, sans-serif", "fontWeight": "bold", "lineHeight": 1.3, "color": "#f9fafb" },
                    { "type": "text", "slot": "footnote", "x": 140, "y": 860, "maxWidth": 800, "fontSize": 22, "fontFamily": "Inter, sans-serif", "color": "#6b7280" }
                  ]
                }
                """,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Template
            {
                Id = AnnouncementCardId,
                Name = "Announcement Card",
                Description = "Bold announcement card with category pill, headline, summary text, and brand handle.",
                Width = 1080,
                Height = 1080,
                Schema = """
                {
                  "name": "Announcement Card",
                  "width": 1080,
                  "height": 1080,
                  "slots": [
                    { "name": "badge_text", "type": "text", "required": false, "description": "Category badge (e.g. PRODUCT UPDATE)" },
                    { "name": "headline", "type": "text", "required": true, "description": "Main announcement title" },
                    { "name": "body_text", "type": "text", "required": false, "description": "Brief descriptive paragraph" },
                    { "name": "brand_handle", "type": "text", "required": false, "description": "Brand handle footer" }
                  ],
                  "layers": [
                    { "type": "rect", "fill": "#022c22", "x": 0, "y": 0, "w": "100%", "h": "100%" },
                    { "type": "rect", "fill": "#064e3b", "stroke": "#059669", "strokeWidth": 2, "x": 100, "y": 140, "w": 260, "h": 46, "borderRadius": 23 },
                    { "type": "text", "slot": "badge_text", "x": 130, "y": 152, "fontSize": 18, "fontFamily": "Inter, sans-serif", "fontWeight": "bold", "color": "#6ee7b7", "textTransform": "uppercase" },
                    { "type": "text", "slot": "headline", "x": 100, "y": 280, "maxWidth": 880, "fontSize": 56, "fontFamily": "Inter, sans-serif", "fontWeight": "bold", "lineHeight": 1.25, "color": "#ffffff" },
                    { "type": "text", "slot": "body_text", "x": 100, "y": 560, "maxWidth": 880, "fontSize": 32, "fontFamily": "Inter, sans-serif", "lineHeight": 1.4, "color": "#a7f3d0" },
                    { "type": "text", "slot": "brand_handle", "x": 100, "y": 920, "fontSize": 24, "fontFamily": "Inter, sans-serif", "color": "#34d399" }
                  ]
                }
                """,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Template
            {
                Id = CarouselSlideId,
                Name = "Carousel Slide",
                Description = "Standard 4:5 carousel slide with slide number, section title, takeaway body, and swipe indicator.",
                Width = 1080,
                Height = 1350,
                Schema = """
                {
                  "name": "Carousel Slide",
                  "width": 1080,
                  "height": 1350,
                  "slots": [
                    { "name": "slide_number", "type": "text", "required": false, "description": "Slide indicator (e.g. 01 / 05)" },
                    { "name": "slide_title", "type": "text", "required": true, "description": "Section or principle title" },
                    { "name": "slide_body", "type": "text", "required": true, "description": "Main explanation text" },
                    { "name": "footer_cta", "type": "text", "required": false, "description": "Bottom CTA or swipe prompt" }
                  ],
                  "layers": [
                    { "type": "rect", "fill": "#18181b", "x": 0, "y": 0, "w": "100%", "h": "100%" },
                    { "type": "rect", "fill": "#27272a", "x": 80, "y": 80, "w": 920, "h": 1190, "borderRadius": 24 },
                    { "type": "text", "slot": "slide_number", "x": 140, "y": 140, "fontSize": 24, "fontFamily": "Inter, sans-serif", "fontWeight": "bold", "color": "#a1a1aa" },
                    { "type": "text", "slot": "slide_title", "x": 140, "y": 240, "maxWidth": 800, "fontSize": 48, "fontFamily": "Inter, sans-serif", "fontWeight": "bold", "lineHeight": 1.3, "color": "#ffffff" },
                    { "type": "rect", "fill": "#3b82f6", "x": 140, "y": 380, "w": 80, "h": 4, "borderRadius": 2 },
                    { "type": "text", "slot": "slide_body", "x": 140, "y": 440, "maxWidth": 800, "fontSize": 32, "fontFamily": "Inter, sans-serif", "lineHeight": 1.5, "color": "#d4d4d8" },
                    { "type": "text", "slot": "footer_cta", "x": 140, "y": 1160, "maxWidth": 800, "fontSize": 22, "fontFamily": "Inter, sans-serif", "color": "#71717a" }
                  ]
                }
                """,
                CreatedAt = now,
                UpdatedAt = now
            },
            new Template
            {
                Id = CodeSnippetCardId,
                Name = "Code Snippet Card",
                Description = "Dark terminal style card with window controls, language badge, code text, and caption.",
                Width = 1080,
                Height = 1080,
                Schema = """
                {
                  "name": "Code Snippet Card",
                  "width": 1080,
                  "height": 1080,
                  "slots": [
                    { "name": "window_title", "type": "text", "required": false, "description": "File or snippet title (e.g. UserValidator.cs)" },
                    { "name": "code_text", "type": "text", "required": true, "description": "Formatted code block text" },
                    { "name": "caption", "type": "text", "required": false, "description": "Bottom takeaway note" }
                  ],
                  "layers": [
                    { "type": "rect", "fill": "#0a0a0c", "x": 0, "y": 0, "w": "100%", "h": "100%" },
                    { "type": "rect", "fill": "#141419", "stroke": "#272732", "strokeWidth": 2, "x": 80, "y": 80, "w": 920, "h": 920, "borderRadius": 20 },
                    { "type": "circle", "fill": "#ef4444", "cx": 130, "cy": 130, "radius": 10 },
                    { "type": "circle", "fill": "#f59e0b", "cx": 160, "cy": 130, "radius": 10 },
                    { "type": "circle", "fill": "#10b981", "cx": 190, "cy": 130, "radius": 10 },
                    { "type": "text", "slot": "window_title", "x": 230, "y": 120, "fontSize": 18, "fontFamily": "monospace", "color": "#9ca3af" },
                    { "type": "rect", "fill": "#1e1e24", "x": 80, "y": 170, "w": 920, "h": 2 },
                    { "type": "text", "slot": "code_text", "x": 120, "y": 220, "maxWidth": 840, "fontSize": 26, "fontFamily": "monospace", "lineHeight": 1.45, "color": "#e2e8f0" },
                    { "type": "text", "slot": "caption", "x": 120, "y": 920, "maxWidth": 840, "fontSize": 22, "fontFamily": "Inter, sans-serif", "color": "#64748b" }
                  ]
                }
                """,
                CreatedAt = now,
                UpdatedAt = now
            }
        };
    }
}
