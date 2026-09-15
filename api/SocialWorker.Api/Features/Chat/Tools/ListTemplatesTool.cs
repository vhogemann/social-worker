using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;

namespace SocialWorker.Api.Features.Chat.Tools;

public sealed record ListTemplatesArgs(string? Search = null);

public sealed record ListTemplatesSlotItem(
    string Name,
    string Type,
    bool Required,
    string? Description);

public sealed record ListTemplatesResultItem(
    Guid Id,
    string Name,
    string? Description,
    int Width,
    int Height,
    IReadOnlyList<ListTemplatesSlotItem> Slots);

public sealed record ListTemplatesResult(IReadOnlyList<ListTemplatesResultItem> Items) : IChatToolResult, IReadOnlyList<ListTemplatesResultItem>
{
    public int Count => Items.Count;

    public ListTemplatesResultItem this[int index] => Items[index];

    public IEnumerator<ListTemplatesResultItem> GetEnumerator() => Items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public string ToDisplayText()
    {
        if (Items.Count == 0)
        {
            return "No image templates found.";
        }

        var lines = Items.Select(t =>
        {
            var slotList = t.Slots.Count > 0
                ? string.Join(", ", t.Slots.Select(s => $"{s.Name} ({s.Type}{(s.Required ? ", required" : "")})"))
                : "none";
            return $"- {t.Name} (ID: {t.Id}, {t.Width}x{t.Height}): {t.Description ?? "No description"}\n  Slots: {slotList}";
        });

        return "Available image templates:\n" + string.Join("\n", lines);
    }
}

public sealed class ListTemplatesTool : ChatToolBase<ListTemplatesArgs, ListTemplatesResult>
{
    private readonly AppDbContext _db;

    public ListTemplatesTool(AppDbContext db)
    {
        _db = db;
    }

    public override string Name => "list_templates";
    public override string Description =>
        "Lists available image templates in the library along with their exact slot names (e.g. 'quote_text', 'metric_value', 'background_image'), slot types, and canvas dimensions. " +
        "Always call this first before render_template to discover exact slot parameter names and requirements.";

    public override JsonElement Parameters { get; } = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "search": {
              "type": "string",
              "description": "Optional keyword to filter templates by name or description (e.g. 'quote', 'metric', 'code', 'announcement')."
            }
          }
        }
        """).RootElement.Clone();

    public override async Task<ListTemplatesResult> ExecuteAsync(ListTemplatesArgs args, Models.ToolExecutionContext context)
    {
        var query = _db.Templates.AsNoTracking().Where(t => t.UserId == context.UserId);

        if (!string.IsNullOrWhiteSpace(args.Search))
        {
            var search = args.Search.Trim().ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(search) ||
                                     (t.Description != null && t.Description.ToLower().Contains(search)));
        }

        var templates = await query.OrderBy(t => t.Name).ToListAsync(context.CancellationToken);

        var resultItems = new List<ListTemplatesResultItem>();

        foreach (var t in templates)
        {
            var slots = new List<ListTemplatesSlotItem>();
            try
            {
                using var doc = JsonDocument.Parse(t.Schema);
                if (doc.RootElement.TryGetProperty("slots", out var slotsEl) && slotsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var slotEl in slotsEl.EnumerateArray())
                    {
                        var name = slotEl.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var type = slotEl.TryGetProperty("type", out var typ) ? typ.GetString() ?? "text" : "text";
                        var req = slotEl.TryGetProperty("required", out var r) && r.GetBoolean();
                        var desc = slotEl.TryGetProperty("description", out var d) ? d.GetString() : null;

                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            slots.Add(new ListTemplatesSlotItem(name, type, req, desc));
                        }
                    }
                }
            }
            catch
            {
                // If schema parsing fails, continue with empty slots
            }

            resultItems.Add(new ListTemplatesResultItem(
                t.Id,
                t.Name,
                t.Description,
                t.Width,
                t.Height,
                slots));
        }

        return new ListTemplatesResult(resultItems);
    }
}
