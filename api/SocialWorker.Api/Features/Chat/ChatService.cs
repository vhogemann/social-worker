using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Llm;
using SkiaSharp;

namespace SocialWorker.Api.Features.Chat;

public sealed class ChatService
{
    private readonly HttpClient _http;
    private readonly ILogger<ChatService> _log;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModelCapabilityProbe _probe;

    public ChatService(HttpClient http, ILogger<ChatService> log, IServiceScopeFactory scopeFactory, ModelCapabilityProbe probe)
    {
        _http = http;
        _log = log;
        _scopeFactory = scopeFactory;
        _probe = probe;
    }

    public static readonly JsonElement ViewImageSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "id": {
              "type": "string",
              "description": "The unique Guid identifier of the image to view."
            }
          },
          "required": ["id"]
        }
        """).RootElement.Clone();

    public static readonly JsonElement ReplaceEditorContentSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "markdown": {
              "type": "string",
              "description": "The full markdown content to replace the editor with. Use --- on its own line to separate thread segments."
            }
          },
          "required": ["markdown"]
        }
        """).RootElement.Clone();

    public static readonly JsonElement ProposeStageTransitionSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "platform": {
              "type": "string",
              "description": "The target platform to adapt and transition (e.g. Bluesky, Twitter)."
            },
            "stage": {
              "type": "string",
              "description": "The target stage to propose: Draft, Ready, Sent."
            },
            "reasoning": {
              "type": "string",
              "description": "The rationale for proposing this stage transition."
            }
          },
          "required": ["platform", "stage", "reasoning"]
        }
        """).RootElement.Clone();

    public static readonly JsonElement ListSourcesSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {},
          "required": []
        }
        """).RootElement.Clone();

    public static readonly JsonElement FetchSourceSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "id": {
              "type": "string",
              "description": "The unique Guid identifier of the source to read."
            }
          },
          "required": ["id"]
        }
        """).RootElement.Clone();


    public async IAsyncEnumerable<string> StreamAsync(ChatModels.ChatRequest req, Guid userId, [EnumeratorCancellation] CancellationToken ct)
    {
        var convo = new List<OpenAiModels.OpenAiMessage>();

        string editorContent;
        string providerBaseUrl;
        string providerApiKey;
        string providerModel;
        bool supportsVision = false;
        string imagesMetadata = "";

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct)
                ?? throw new InvalidOperationException("User not found or inactive.");

            LlmProvider? provider = null;
            if (user.PreferredProviderId.HasValue)
            {
                provider = await db.LlmProviders.FirstOrDefaultAsync(p => p.Id == user.PreferredProviderId.Value && p.IsActive, ct);
            }

            if (provider == null)
            {
                provider = await db.LlmProviders.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, ct);
            }

            if (provider == null)
            {
                throw new InvalidOperationException("No active LLM provider found.");
            }

            providerBaseUrl = provider.BaseUrl;
            providerApiKey = provider.ApiKey;
            providerModel = provider.Model;

            var caps = await _probe.GetCapabilitiesAsync(provider);
            supportsVision = caps.SupportsVision;

            Draft draft;
            if (req.DraftId.HasValue)
            {
                draft = await db.Drafts.FirstOrDefaultAsync(d => d.Id == req.DraftId.Value && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
                    ?? throw new InvalidOperationException($"Draft {req.DraftId.Value} not found or access denied");
            }
            else if (!string.IsNullOrEmpty(req.Editor))
            {
                draft = new Draft { Content = req.Editor, UserId = userId };
                db.Drafts.Add(draft);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                draft = new Draft { Content = "", UserId = userId };
                db.Drafts.Add(draft);
                await db.SaveChangesAsync(ct);
            }

            editorContent = draft.Content ?? "";

            if (!string.IsNullOrEmpty(req.Editor) && req.Editor != editorContent)
            {
                draft.Content = req.Editor;
                draft.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                editorContent = req.Editor;
            }

            var mediaAssets = await db.MediaAssets.Where(m => m.DraftId == draft.Id).ToListAsync(ct);
            if (mediaAssets.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("\n\n--- ATTACHED IMAGES ---");
                int idx = 1;
                foreach (var asset in mediaAssets)
                {
                    sb.AppendLine($"{idx++}. {asset.FileName} (media://{asset.Id}) - {asset.Width}x{asset.Height}, {asset.SizeBytes / 1024} KB, alt: \"{asset.AltText}\"");
                }
                imagesMetadata = sb.ToString();
            }

            if (draft.Title == "Untitled" && req.Messages.Count > 0)
            {
                await TryGenerateDraftTitleAsync(db, draft, req.Messages, providerBaseUrl, providerApiKey, providerModel, ct);
            }
        }

        var systemPrompt = !string.IsNullOrWhiteSpace(req.System)
            ? req.System
            : "You are a helpful assistant that helps the user draft social media threads. "
              + "When the user asks you to write or update content, call replace_editor_content with the full markdown. "
              + "Use --- on its own line to separate thread segments (each segment is one post).\n"
              + "You have access to reference sources (attached files or URLs detected in the draft). "
              + "To view the list of available sources, call the 'list_sources' tool. "
              + "To read the actual cached text content of a source, call the 'fetch_source' tool with its Guid ID. "
              + "If the user asks you to summarize, explain, or draft posts based on a link or file attachment, you MUST first call 'list_sources' to locate it, and then call 'fetch_source' with the corresponding ID to read the source text before responding.";

        systemPrompt += "\n\n--- EDITOR CONTENT START ---\n"
            + (string.IsNullOrEmpty(editorContent) ? "(editor is currently empty)" : editorContent)
            + "\n--- EDITOR CONTENT END ---";

        if (!string.IsNullOrEmpty(imagesMetadata))
        {
            systemPrompt += imagesMetadata;
        }

        if (supportsVision)
        {
            systemPrompt += "\n\nYou have access to view the attached images using the 'view_image' tool. If an image alt text is empty (alt: \"\"), view the image using the tool and suggest a concise alt text description to the user.";
        }
        else
        {
            systemPrompt += "\n\nVision is not available with the current model. You can still reference images by their metadata (filename, dimensions).";
        }

        convo.Add(new OpenAiModels.OpenAiMessage { Role = "system", Content = systemPrompt });

        foreach (var m in req.Messages)
        {
            var text = string.Join("\n", m.Content.Where(p => p.Type == "text").Select(p => p.Text ?? ""));
            convo.Add(new OpenAiModels.OpenAiMessage { Role = m.Role, Content = text });
        }

        var tools = new List<OpenAiModels.OpenAiTool>
        {
            new()
            {
                Function = new()
                {
                    Name = "replace_editor_content",
                    Description = "Replace the entire content of the markdown editor with the provided text.",
                    Parameters = ReplaceEditorContentSchema,
                }
            },
            new()
            {
                Function = new()
                {
                    Name = "propose_stage_transition",
                    Description = "Propose transitioning the draft to a new stage (e.g. Sourcing, Refining, Formatting, Ready, Sent).",
                    Parameters = ProposeStageTransitionSchema,
                }
            },
            new()
            {
                Function = new()
                {
                    Name = "list_sources",
                    Description = "List all sources attached to the active draft (e.g. text notes or URLs parsed from the text).",
                    Parameters = ListSourcesSchema,
                }
            },
            new()
            {
                Function = new()
                {
                    Name = "fetch_source",
                    Description = "Fetch the cached text content of a specific source by its Guid ID.",
                    Parameters = FetchSourceSchema,
                }
            }
        };

        if (supportsVision)
        {
            tools.Add(new OpenAiModels.OpenAiTool
            {
                Function = new()
                {
                    Name = "view_image",
                    Description = "Fetch a specific image by its Guid ID. Returns the image so you can inspect its visual content.",
                    Parameters = ViewImageSchema,
                }
            });
        }

        var payload = new OpenAiModels.ChatCompletionRequest
        {
            Model = providerModel,
            Messages = convo,
            Stream = true,
            Tools = tools
        };

        yield return "f:{\"messageId\":\"m_" + Guid.NewGuid().ToString("N") + "\"}\n";

        const int maxRounds = 3;
        for (var round = 0; round < maxRounds; round++)
        {
            var toolCalls = new Dictionary<int, AccumulatedToolCall>();
            string? finishReason = null;

            await foreach (var chunk in CallOpenAiAsync(payload, providerBaseUrl, providerApiKey, ct))
            {
                foreach (var choice in chunk.Choices)
                {
                    if (choice.Delta.Content is { } c)
                    {
                        yield return "0:" + JsonSerializer.Serialize(c) + "\n";
                    }

                    if (choice.Delta.ToolCalls is { } tcs)
                    {
                        foreach (var tc in tcs)
                        {
                            if (!toolCalls.TryGetValue(tc.Index, out var acc))
                            {
                                acc = new AccumulatedToolCall();
                                toolCalls[tc.Index] = acc;
                            }
                            if (tc.Id is { } id) acc.Id = id;
                            if (tc.Function?.Name is { } name) acc.Name = name;
                            if (tc.Function?.Arguments is { } args) acc.Arguments += args;
                        }
                    }

                    if (choice.FinishReason is { } fr)
                    {
                        finishReason = fr;
                    }
                }
            }

            if (toolCalls.Count == 0)
            {
                yield return SerializeFinishStep(finishReason ?? "stop", false);
                break;
            }

            var assistantMsg = new OpenAiModels.OpenAiMessage
            {
                Role = "assistant",
                ToolCalls = toolCalls.Values.Select(tc => new OpenAiModels.OpenAiToolCall
                {
                    Id = tc.Id,
                    Function = new() { Name = tc.Name, Arguments = tc.Arguments }
                }).ToList(),
            };
            payload.Messages.Add(assistantMsg);

            foreach (var tc in toolCalls.Values)
            {
                yield return SerializeToolCall(tc.Id, tc.Name, tc.Arguments);

                var result = await ExecuteToolAsync(tc.Name, tc.Arguments, req.DraftId, userId, ct);
                yield return SerializeToolResult(tc.Id, result);

                if (tc.Name == "view_image")
                {
                    payload.Messages.Add(new OpenAiModels.OpenAiMessage
                    {
                        Role = "tool",
                        ToolCallId = tc.Id,
                        Content = "Image successfully retrieved and loaded.",
                    });

                    if (result is object[] parts)
                    {
                        payload.Messages.Add(new OpenAiModels.OpenAiMessage
                        {
                            Role = "user",
                            Content = parts
                        });
                    }
                }
                else
                {
                    string toolContentStr = result is string s ? s : JsonSerializer.Serialize(result);
                    payload.Messages.Add(new OpenAiModels.OpenAiMessage
                    {
                        Role = "tool",
                        ToolCallId = tc.Id,
                        Content = toolContentStr,
                    });
                }
            }

            yield return SerializeFinishStep("tool-calls", true);
        }

        yield return "d:{\"finishReason\":\"stop\",\"usage\":{\"promptTokens\":0,\"completionTokens\":0}}\n";
    }

    private async IAsyncEnumerable<OpenAiModels.StreamChunk> CallOpenAiAsync(
        OpenAiModels.ChatCompletionRequest payload, string baseUrl, string apiKey, [EnumeratorCancellation] CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrEmpty(apiKey))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            _log.LogError("OpenAI API error {Status}: {Body}", resp.StatusCode, err);
            throw new InvalidOperationException($"Upstream {resp.StatusCode}: {err}");
        }

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data:")) continue;
            var data = line.Substring("data:".Length).Trim();
            if (data == "[DONE]") yield break;

            OpenAiModels.StreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OpenAiModels.StreamChunk>(data);
            }
            catch (JsonException ex)
            {
                _log.LogWarning("Failed to parse chunk: {Data} ({Msg})", data, ex.Message);
                continue;
            }

            if (chunk is not null) yield return chunk;
        }
    }

    private async Task<object> ExecuteToolAsync(string name, string argumentsJson, Guid? draftId, Guid userId, CancellationToken ct)
    {
        if (name == "replace_editor_content")
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var markdown = doc.RootElement.GetProperty("markdown").GetString() ?? "";

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var draft = draftId.HasValue
                ? await db.Drafts.FirstOrDefaultAsync(d => d.Id == draftId.Value && d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
                    ?? throw new InvalidOperationException($"Draft {draftId.Value} not found or access denied")
                : await db.Drafts.OrderByDescending(d => d.UpdatedAt).FirstOrDefaultAsync(d => d.UserId == userId && d.Status != DraftStatus.Deleted, ct)
                    ?? throw new InvalidOperationException("No active draft found");

            draft.Content = markdown;
            draft.UpdatedAt = DateTime.UtcNow;

            await SocialWorker.Api.Features.Drafts.DraftsEndpoint.ReconcileSegmentsAsync(db, draft, markdown, ct);
            await db.SaveChangesAsync(ct);

            return new { success = true, length = markdown.Length, content = markdown };
        }

        if (name == "propose_stage_transition")
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var platform = doc.RootElement.GetProperty("platform").GetString() ?? "";
            var stage = doc.RootElement.GetProperty("stage").GetString() ?? "";
            var reasoning = doc.RootElement.GetProperty("reasoning").GetString() ?? "";
            return new { success = true, platform, proposedStage = stage, reasoning };
        }
        if (name == "list_sources")
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var activeDraftId = draftId.HasValue
                ? draftId.Value
                : (await db.Drafts.OrderByDescending(d => d.UpdatedAt).FirstOrDefaultAsync(d => d.UserId == userId && d.Status != DraftStatus.Deleted, ct))?.Id;

            if (activeDraftId == null)
            {
                return new { error = "No active draft found" };
            }

            var sources = await db.Sources
                .Where(s => s.DraftId == activeDraftId.Value)
                .Select(s => new { s.Id, Kind = s.Kind.ToString(), s.Reference, s.Title })
                .ToListAsync(ct);

            return sources;
        }

        if (name == "fetch_source")
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var sourceIdStr = doc.RootElement.GetProperty("id").GetString() ?? "";
            if (!Guid.TryParse(sourceIdStr, out var sourceId))
            {
                return new { error = "Invalid Guid ID format" };
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var source = await db.Sources.FirstOrDefaultAsync(s => s.Id == sourceId, ct);
            if (source == null)
            {
                return new { error = $"Source {sourceId} not found" };
            }

            var owned = await db.Drafts.AnyAsync(d => d.Id == source.DraftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
            if (!owned)
            {
                return new { error = "Access denied to target source" };
            }

            return new
            {
                source.Id,
                Kind = source.Kind.ToString(),
                source.Reference,
                source.Title,
                source.Content
            };
        }

        if (name == "view_image")
        {
            using var doc = JsonDocument.Parse(argumentsJson);
            var imageIdStr = doc.RootElement.GetProperty("id").GetString() ?? "";
            if (!Guid.TryParse(imageIdStr, out var imageId))
            {
                return new { error = "Invalid Guid ID format" };
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var asset = await db.MediaAssets.FirstOrDefaultAsync(m => m.Id == imageId, ct);
            if (asset == null)
            {
                return new { error = $"Image {imageId} not found" };
            }

            var owned = await db.Drafts.AnyAsync(d => d.Id == asset.DraftId && d.UserId == userId && d.Status != DraftStatus.Deleted, ct);
            if (!owned)
            {
                return new { error = "Access denied to target image" };
            }

            var fullPath = Path.Combine("/app/uploads", asset.FilePath);
            if (!File.Exists(fullPath))
            {
                return new { error = "Image file not found on disk" };
            }

            try
            {
                using var stream = File.OpenRead(fullPath);
                using var codec = SKCodec.Create(stream);
                if (codec == null)
                {
                    return new { error = "Failed to decode image" };
                }

                int maxDim = 512;
                int newWidth = codec.Info.Width;
                int newHeight = codec.Info.Height;
                
                if (newWidth > maxDim || newHeight > maxDim)
                {
                    double ratio = Math.Min((double)maxDim / newWidth, (double)maxDim / newHeight);
                    newWidth = (int)(newWidth * ratio);
                    newHeight = (int)(newHeight * ratio);
                }

                stream.Position = 0;
                using var original = SKBitmap.Decode(codec);
                using var resized = original.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.Medium);
                using var image = SKImage.FromBitmap(resized);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
                var bytes = data.ToArray();
                var base64 = Convert.ToBase64String(bytes);

                return new object[]
                {
                    new { type = "text", text = $"Image: {asset.FileName} ({asset.Width}x{asset.Height}). Current alt text: {asset.AltText ?? "(none)"}" },
                    new { type = "image_url", image_url = new { url = $"data:image/jpeg;base64,{base64}" } }
                };
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to process image: {ex.Message}" };
            }
        }

        return new { error = $"unknown tool: {name}" };
    }

    private async Task TryGenerateDraftTitleAsync(
        AppDbContext db, Draft draft, List<ChatModels.UiMessage> messages, string baseUrl, string apiKey, string model, CancellationToken ct)
    {
        var firstMsg = messages.FirstOrDefault();
        if (firstMsg == null) return;
        var text = string.Join("\n", firstMsg.Content.Where(p => p.Type == "text").Select(p => p.Text ?? ""));
        if (string.IsNullOrWhiteSpace(text)) return;

        try
        {
            var summarizePayload = new OpenAiModels.ChatCompletionRequest
            {
                Model = model,
                Messages = new()
                {
                    new()
                    {
                        Role = "system",
                        Content = "You are a helpful assistant. Summarize the user's first prompt into a clean 3-5 word title for their draft. Output ONLY the raw title without quotation marks, markdown formatting, or emojis."
                    },
                    new()
                    {
                        Role = "user",
                        Content = text
                    }
                },
                Stream = false
            };

            var json = JsonSerializer.Serialize(summarizePayload);
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl.TrimEnd('/')}/chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            if (!string.IsNullOrEmpty(apiKey))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                var choice = doc.RootElement.GetProperty("choices")[0];
                var content = choice.GetProperty("message").GetProperty("content").GetString();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var cleanTitle = content.Trim('"', '\'', ' ', '\n', '\r');
                    if (cleanTitle.Length > 100) cleanTitle = cleanTitle.Substring(0, 100);
                    draft.Title = cleanTitle;
                    await db.SaveChangesAsync(ct);
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to auto-generate title for draft {Id}", draft.Id);
        }
    }

    private static string SerializeToolCall(string id, string name, string argsJson)
    {
        var obj = new
        {
            toolCallId = id,
            toolName = name,
            args = string.IsNullOrEmpty(argsJson)
                ? (object)new { }
                : JsonDocument.Parse(argsJson).RootElement.Clone(),
        };
        return "9:" + JsonSerializer.Serialize(obj) + "\n";
    }

    private static string SerializeToolResult(string id, object result)
    {
        var obj = new
        {
            toolCallId = id,
            result = result
        };
        return "a:" + JsonSerializer.Serialize(obj) + "\n";
    }

    private static string SerializeFinishStep(string finishReason, bool isContinued)
    {
        var obj = new
        {
            finishReason,
            usage = new { promptTokens = 0, completionTokens = 0 },
            isContinued,
        };
        return "e:" + JsonSerializer.Serialize(obj) + "\n";
    }

    private sealed class AccumulatedToolCall
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "";
    }
}