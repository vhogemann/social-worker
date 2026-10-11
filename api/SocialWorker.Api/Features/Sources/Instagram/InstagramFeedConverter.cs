using System;
using System.Linq;
using System.Text;
using System.Xml;
using CodeHollow.FeedReader;

namespace SocialWorker.Api.Features.Sources.Instagram;

public static class InstagramFeedConverter
{
    public static Feed ToFeed(InstagramProfileDiscovery profile)
    {
        var sb = new StringBuilder();
        using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = false, Encoding = Encoding.UTF8 }))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("rss");
            writer.WriteAttributeString("version", "2.0");
            writer.WriteStartElement("channel");

            var channelTitle = !string.IsNullOrWhiteSpace(profile.Name)
                ? $"{profile.Name} (@{profile.Username}) on Instagram"
                : $"@{profile.Username} on Instagram";

            writer.WriteElementString("title", channelTitle);
            writer.WriteElementString("link", $"https://www.instagram.com/{profile.Username}");
            writer.WriteElementString("description", profile.Biography ?? $"Instagram posts from @{profile.Username}");

            if (profile.Media?.Data != null)
            {
                foreach (var item in profile.Media.Data)
                {
                    writer.WriteStartElement("item");
                    var itemTitle = ExtractTitle(item.Caption, profile.Username, item.Timestamp);
                    writer.WriteElementString("title", itemTitle);
                    writer.WriteElementString("link", item.Permalink ?? $"https://www.instagram.com/p/{item.Id}");
                    writer.WriteElementString("description", item.Caption ?? string.Empty);
                    if (item.Timestamp.HasValue)
                    {
                        writer.WriteElementString("pubDate", item.Timestamp.Value.ToUniversalTime().ToString("r"));
                    }
                    writer.WriteElementString("guid", item.Id);
                    if (!string.IsNullOrWhiteSpace(item.MediaUrl))
                    {
                        var isVideo = string.Equals(item.MediaType, "VIDEO", StringComparison.OrdinalIgnoreCase);
                        var mimeType = isVideo ? "video/mp4" : "image/jpeg";
                        writer.WriteStartElement("enclosure");
                        writer.WriteAttributeString("url", item.MediaUrl);
                        writer.WriteAttributeString("type", mimeType);
                        writer.WriteAttributeString("length", "0");
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Flush();
        }

        return FeedReader.ReadFromString(sb.ToString());
    }

    private static string ExtractTitle(string? caption, string username, DateTime? timestamp)
    {
        if (!string.IsNullOrWhiteSpace(caption))
        {
            var firstLine = caption
                .Split(new[] { '\r', '\n' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstLine))
            {
                return firstLine.Length > 100 ? firstLine[..97] + "..." : firstLine;
            }
        }

        return timestamp.HasValue
            ? $"@{username} post ({timestamp.Value:yyyy-MM-dd HH:mm})"
            : $"@{username} post";
    }
}
