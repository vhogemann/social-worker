using System;
using System.Collections.Generic;
using System.Linq;

namespace SocialWorker.Api.Infrastructure.Tags;

public static class TagHelper
{
    public static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags == null) return new List<string>();

        return tags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool HasTag(IEnumerable<string>? tags, string tag)
    {
        if (tags == null || string.IsNullOrWhiteSpace(tag)) return false;
        var normalized = tag.Trim().ToLowerInvariant();
        return tags.Any(t => string.Equals(t.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static List<string> AddTag(IEnumerable<string>? existingTags, string tag)
    {
        var list = NormalizeTags(existingTags);
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalized = tag.Trim().ToLowerInvariant();
            if (!list.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(normalized);
            }
        }
        return list;
    }

    public static List<string> AddTags(IEnumerable<string>? existingTags, IEnumerable<string>? tags)
    {
        var list = NormalizeTags(existingTags);
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    var normalized = tag.Trim().ToLowerInvariant();
                    if (!list.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        list.Add(normalized);
                    }
                }
            }
        }
        return list;
    }

    public static List<string> AddTag(IEnumerable<string>? existingTags, IEnumerable<string>? tags) => AddTags(existingTags, tags);

    public static List<string> RemoveTag(IEnumerable<string>? existingTags, string tag)
    {
        var list = NormalizeTags(existingTags);
        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalized = tag.Trim().ToLowerInvariant();
            list.RemoveAll(t => string.Equals(t, normalized, StringComparison.OrdinalIgnoreCase));
        }
        return list;
    }

    public static List<string> RemoveTags(IEnumerable<string>? existingTags, IEnumerable<string>? tags)
    {
        var list = NormalizeTags(existingTags);
        if (tags != null)
        {
            var toRemove = new HashSet<string>(NormalizeTags(tags), StringComparer.OrdinalIgnoreCase);
            list.RemoveAll(t => toRemove.Contains(t));
        }
        return list;
    }

    public static List<string> RemoveTag(IEnumerable<string>? existingTags, IEnumerable<string>? tags) => RemoveTags(existingTags, tags);

    public static List<string> ApplyUpdate(IEnumerable<string>? existingTags, UpdateTagsRequest req)
    {
        var current = NormalizeTags(existingTags);
        if (!string.IsNullOrWhiteSpace(req.Action))
        {
            return req.Action.ToLowerInvariant() switch
            {
                "add" => AddTags(current, req.Tags ?? req.Add),
                "remove" => RemoveTags(current, req.Tags ?? req.Remove),
                "set" or _ => NormalizeTags(req.Tags)
            };
        }

        if (req.Tags != null)
        {
            current = NormalizeTags(req.Tags);
        }
        if (req.Add != null && req.Add.Count > 0)
        {
            current = AddTags(current, req.Add);
        }
        if (req.Remove != null && req.Remove.Count > 0)
        {
            current = RemoveTags(current, req.Remove);
        }
        return current;
    }
}

public record UpdateTagsRequest(
    List<string>? Tags = null,
    string? Action = null,
    List<string>? Add = null,
    List<string>? Remove = null);
