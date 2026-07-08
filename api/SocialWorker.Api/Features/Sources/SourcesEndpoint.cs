using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using UglyToad.PdfPig;
using System.Text.Json;

namespace SocialWorker.Api.Features.Sources;

public static class SourcesEndpoint
{
    private static readonly Regex UrlRegex = new(@"https?://[^\s\)\""'<>]+", RegexOptions.Compiled);
    private static readonly Regex FileRegex = new(@"file://([0-9a-fA-F\-]{36})", RegexOptions.Compiled);

    public static void MapSourcesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/drafts/{draftId:guid}").RequireAuthorization();

        group.MapGet("/sources", async (ClaimsPrincipal principal, AppDbContext db, Guid draftId) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var draftExists = await db.Drafts.AnyAsync(d => d.Id == draftId && d.UserId == userId.Value && d.Status != DraftStatus.Deleted);
            if (!draftExists) return Results.NotFound();

            var sources = await db.Sources
                .Where(s => s.DraftId == draftId)
                .Select(s => new
                {
                    s.Id,
                    s.DraftId,
                    Kind = s.Kind.ToString(),
                    s.Reference,
                    s.Title,
                    s.AddedAt
                })
                .ToListAsync();

            return Results.Ok(sources);
        });

        group.MapPost("/files", async (ClaimsPrincipal principal, AppDbContext db, IServiceScopeFactory scopeFactory, Guid draftId, IFormFile file) =>
        {
            var userId = GetUserId(principal);
            if (userId is null) return Results.Unauthorized();

            var draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId && d.UserId == userId.Value && d.Status != DraftStatus.Deleted);
            if (draft is null) return Results.NotFound();

            if (file == null || file.Length == 0)
            {
                return Results.BadRequest("No file uploaded");
            }

            string extractedText;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            try
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                byte[] hashBytes;
                using (var tempStream = new MemoryStream())
                {
                    using var uploadStream = file.OpenReadStream();
                    await uploadStream.CopyToAsync(tempStream);
                    tempStream.Position = 0;
                    hashBytes = sha256.ComputeHash(tempStream);
                }
                var shaHashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                var existing = await db.Sources.FirstOrDefaultAsync(s => s.Sha256 == shaHashStr);
                if (existing != null)
                {
                    var source = new Source
                    {
                        DraftId = draftId,
                        Kind = SourceKind.File,
                        Reference = file.FileName,
                        Title = existing.Title ?? file.FileName,
                        Content = existing.Content,
                        Sha256 = shaHashStr,
                        AddedAt = DateTime.UtcNow
                    };

                    db.Sources.Add(source);
                    draft.Status = DraftStatus.Editing;
                    await db.SaveChangesAsync();

                    return Results.Ok(new
                    {
                        sourceId = source.Id,
                        markdownLink = $"[File: {source.Reference}](file://{source.Id})"
                    });
                }

                draft.Status = DraftStatus.Sourcing;
                await db.SaveChangesAsync();

                using var stream = file.OpenReadStream();
                if (ext == ".pdf")
                {
                    extractedText = ExtractPdfText(stream);
                }
                else if (ext == ".txt" || ext == ".md")
                {
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    extractedText = await reader.ReadToEndAsync();
                }
                else
                {
                    draft.Status = DraftStatus.Editing;
                    await db.SaveChangesAsync();
                    return Results.BadRequest("Unsupported file format. Supported: .pdf, .txt, .md");
                }

                var newSource = new Source
                {
                    DraftId = draftId,
                    Kind = SourceKind.File,
                    Reference = file.FileName,
                    Title = file.FileName,
                    Content = extractedText,
                    Sha256 = shaHashStr,
                    AddedAt = DateTime.UtcNow
                };

                db.Sources.Add(newSource);
                draft.Status = DraftStatus.Editing;
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    sourceId = newSource.Id,
                    markdownLink = $"[File: {newSource.Reference}](file://{newSource.Id})"
                });
            }
            catch (Exception ex)
            {
                draft.Status = DraftStatus.Editing;
                await db.SaveChangesAsync();
                return Results.BadRequest($"Failed to process file attachment: {ex.Message}");
            }
        });
    }

    public static async Task ReconcileSourcesAsync(AppDbContext db, IServiceScopeFactory scopeFactory, Draft draft, string content)
    {
        var urls = UrlRegex.Matches(content)
            .Select(m => m.Value)
            .Distinct()
            .ToList();

        var fileIds = FileRegex.Matches(content)
            .Select(m => Guid.TryParse(m.Groups[1].Value, out var id) ? (Guid?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var existing = await db.Sources
            .Where(s => s.DraftId == draft.Id)
            .ToListAsync();

        bool changed = false;
        foreach (var src in existing)
        {
            if ((src.Kind == SourceKind.Url || src.Kind == SourceKind.YouTube) && !urls.Contains(src.Reference))
            {
                db.Sources.Remove(src);
                changed = true;
            }
            else if (src.Kind == SourceKind.File && !fileIds.Contains(src.Id))
            {
                db.Sources.Remove(src);
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }

        var newUrls = urls.Where(url => !existing.Any(e => (e.Kind == SourceKind.Url || e.Kind == SourceKind.YouTube) && e.Reference == url)).ToList();
        if (newUrls.Count > 0)
        {
            draft.Status = DraftStatus.Sourcing;
            await db.SaveChangesAsync();

            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                foreach (var url in newUrls)
                {
                    try
                    {
                        if (IsYouTubeUrl(url))
                        {
                            var (title, contentText) = await FetchYouTubeMetadataAsync(url);
                            var source = new Source
                            {
                                DraftId = draft.Id,
                                Kind = SourceKind.YouTube,
                                Reference = url,
                                Title = title,
                                Content = contentText
                            };
                            scopedDb.Sources.Add(source);
                        }
                        else
                        {
                            using var client = new HttpClient();
                            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows" +
                                " NT 10.0; Win64; x64) AppleWebKit/537.36");
                            var html = await client.GetStringAsync(url);
                            var (title, text) = ExtractUrlContent(html, url);

                            var source = new Source
                            {
                                DraftId = draft.Id,
                                Kind = SourceKind.Url,
                                Reference = url,
                                Title = title,
                                Content = text
                            };
                            scopedDb.Sources.Add(source);
                        }
                    }
                    catch (Exception ex)
                    {
                        var source = new Source
                        {
                            DraftId = draft.Id,
                            Kind = IsYouTubeUrl(url) ? SourceKind.YouTube : SourceKind.Url,
                            Reference = url,
                            Title = url,
                            Content = $"Error fetching link: {ex.Message}"
                        };
                        scopedDb.Sources.Add(source);
                    }
                }

                var d = await scopedDb.Drafts.FindAsync(draft.Id);
                if (d != null)
                {
                    d.Status = DraftStatus.Editing;
                    d.UpdatedAt = DateTime.UtcNow;
                }
                await scopedDb.SaveChangesAsync();
            });
        }
    }

    private static (string Title, string Text) ExtractUrlContent(string html, string url)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim() ?? url;

        var noiseNodes = doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer|//iframe|//noscript");
        if (noiseNodes != null)
        {
            foreach (var node in noiseNodes)
            {
                node.Remove();
            }
        }

        var textNodes = doc.DocumentNode.SelectNodes("//p|//h1|//h2|//h3|//h4|//li");
        if (textNodes == null)
        {
            return (title, doc.DocumentNode.InnerText?.Trim() ?? "");
        }

        var sb = new StringBuilder();
        foreach (var node in textNodes)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText?.Trim() ?? "");
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (node.Name.StartsWith("h"))
                {
                    sb.AppendLine().AppendLine($"# {text}");
                }
                else if (node.Name == "li")
                {
                    sb.AppendLine($"- {text}");
                }
                else
                {
                    sb.AppendLine().AppendLine(text);
                }
            }
        }

        return (title, sb.ToString().Trim());
    }

    private static string ExtractPdfText(Stream stream)
    {
        using var pdf = PdfDocument.Open(stream);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
        {
            sb.AppendLine(page.Text);
        }
        return sb.ToString().Trim();
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var parsed) ? parsed : null;
    }

    private static bool IsYouTubeUrl(string url)
    {
        return url.Contains("youtube.com/watch", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(string Title, string Content)> FetchYouTubeMetadataAsync(string url)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        
        string title = url;
        string authorName = "";
        string authorUrl = "";
        string description = "";
        string views = "unknown";
        string publishedDate = "unknown";

        try
        {
            var oEmbedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(url)}&format=json";
            var oEmbedRes = await client.GetAsync(oEmbedUrl);
            if (oEmbedRes.IsSuccessStatusCode)
            {
                var json = await oEmbedRes.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("title", out var titleProp)) title = titleProp.GetString() ?? url;
                if (root.TryGetProperty("author_name", out var authorProp)) authorName = authorProp.GetString() ?? "";
                if (root.TryGetProperty("author_url", out var authorUrlProp)) authorUrl = authorUrlProp.GetString() ?? "";
            }
        }
        catch
        {
        }

        if (!string.IsNullOrEmpty(authorUrl))
        {
            try
            {
                string? channelId = null;
                if (authorUrl.Contains("/channel/UC"))
                {
                    var index = authorUrl.IndexOf("/channel/");
                    if (index >= 0)
                    {
                        channelId = authorUrl.Substring(index + 9);
                    }
                }
                else if (authorUrl.Contains("/@"))
                {
                    var channelHtml = await client.GetStringAsync(authorUrl);
                    var match = Regex.Match(channelHtml, @"youtube\.com/feeds/videos\.xml\?channel_id=(UC[\w-]+)");
                    if (match.Success)
                    {
                        channelId = match.Groups[1].Value;
                    }
                }

                if (!string.IsNullOrEmpty(channelId))
                {
                    var feedUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={channelId}";
                    var feedXml = await client.GetStringAsync(feedUrl);
                    
                    var xmlDoc = new System.Xml.XmlDocument();
                    xmlDoc.LoadXml(feedXml);
                    
                    var nsmgr = new System.Xml.XmlNamespaceManager(xmlDoc.NameTable);
                    nsmgr.AddNamespace("atom", "http://www.w3.org/2005/Atom");
                    nsmgr.AddNamespace("yt", "http://www.youtube.com/xml/schemas/2015");
                    nsmgr.AddNamespace("media", "http://search.yahoo.com/mrss/");

                    var videoId = ExtractVideoId(url);
                    if (!string.IsNullOrEmpty(videoId))
                    {
                        var entryNode = xmlDoc.SelectSingleNode($"//atom:entry[yt:videoId='{videoId}']", nsmgr);
                        if (entryNode != null)
                        {
                            description = entryNode.SelectSingleNode("media:group/media:description", nsmgr)?.InnerText ?? "";
                            views = entryNode.SelectSingleNode("media:group/media:community/media:statistics/@views", nsmgr)?.Value ?? "unknown";
                            publishedDate = entryNode.SelectSingleNode("atom:published", nsmgr)?.InnerText ?? "unknown";
                        }
                    }
                }
            }
            catch
            {
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# {title}");
        if (!string.IsNullOrEmpty(authorName))
        {
            sb.AppendLine($"By {authorName}");
        }
        if (views != "unknown" || publishedDate != "unknown")
        {
            sb.AppendLine($"Stats: {views} views | Published {publishedDate}");
        }
        sb.AppendLine();
        if (!string.IsNullOrEmpty(description))
        {
            sb.AppendLine(description);
        }
        else
        {
            sb.AppendLine("(No description available)");
        }

        return (title, sb.ToString().Trim());
    }

    private static string? ExtractVideoId(string url)
    {
        if (url.Contains("youtube.com/watch"))
        {
            var match = Regex.Match(url, @"[?&]v=([\w-]+)");
            if (match.Success) return match.Groups[1].Value;
        }
        else if (url.Contains("youtu.be/"))
        {
            var uri = new Uri(url);
            return uri.AbsolutePath.TrimStart('/');
        }
        return null;
    }
}
