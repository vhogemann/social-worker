# Image Uploads + YouTube Embeds

Add image attachment support to drafts and treat `![](youtube-url)` as embed cards. On Bluesky, a post can carry either images **or** one external embed (link card / YouTube), never both — so we need per-segment validation.

## Design Decisions (Resolved)

| Decision | Choice |
|----------|--------|
| **Storage** | Docker volume (`uploads:/app/uploads`) — fast file I/O, separate from DB |
| **Bluesky conflict** | Block at confirmation — refuse to publish until user resolves image+embed conflict |
| **Image size** | Auto-resize/compress at upload via SkiaSharp (always Bluesky-ready, max 1 MB) |
| **Alt text UX** | Editable in preview card (click image to edit) — non-blocking |
| **GIF support** | Yes — `.gif` allowed (counts toward 4-image limit) |
| **Clipboard paste** | Yes — `Cmd+V` / `Ctrl+V` triggers upload |
| **YouTube ATOM fallback** | Graceful — oEmbed-only data if ATOM feed fails |


## Proposed Changes

### Data Model (`api/`)

#### [NEW] MediaAsset.cs

New entity:

```csharp
public class MediaAsset
{
    public Guid Id { get; set; }
    public Guid DraftId { get; set; }
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public string? AltText { get; set; }
    public string FilePath { get; set; } = "";   // relative path under /app/uploads/
    public long SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Draft Draft { get; set; } = null!;
}
```

#### [MODIFY] Draft.cs

Add navigation: `ICollection<MediaAsset> MediaAssets`.

#### [MODIFY] Enums.cs

Add `YouTube` to `SourceKind`:

```csharp
public enum SourceKind
{
    Url,
    File,
    YouTube
}
```

#### [MODIFY] AppDbContext.cs

- Add `DbSet<MediaAsset> MediaAssets`.
- Configure entity: key, max lengths, cascade delete from Draft.

#### [NEW] EF Migration `AddMediaAssets`

Generated via `docker compose exec api dotnet ef migrations add AddMediaAssets`.

---

### Media Endpoints (`api/`)

#### [NEW] MediaEndpoint.cs

Two routes:

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/drafts/{draftId}/media` | Upload image (multipart). Validates extension + size, saves to disk, creates `MediaAsset` row, returns `{ id, markdownTag }` |
| `GET` | `/api/media/{id}` | Serves the file from disk with correct `Content-Type`. No auth required (images are local-only). |

**Upload logic**:
1. Validate MIME type against allowlist: `image/jpeg`, `image/png`, `image/webp`, `image/gif`.
2. Validate file size (max 1 MB per Bluesky limit, or resize server-side with `SkiaSharp` if larger).
3. Save to `/app/uploads/{draftId}/{assetId}{ext}`.
4. Create `MediaAsset` row.
5. Return `{ id: "<guid>", markdownTag: "![<filename>](media://<guid>)" }`.

**Serve logic**:
1. Look up `MediaAsset` by ID.
2. Stream file from `FilePath` with `Content-Type` from `MimeType`.

#### [MODIFY] Program.cs

- Import and map `MediaEndpoint`.
- Register `SkiaSharp` (if we add server-side resize).

---

### Markdown Convention

Images and YouTube embeds both use standard markdown image syntax `![]()`, distinguished by URL scheme:

| Pattern | Meaning | Example |
|---------|---------|---------|
| `![alt](media://<guid>)` | Uploaded image | `![sunset](media://a1b2c3d4-...)` |
| `![](https://youtube.com/watch?v=...)` | YouTube embed | `![](https://www.youtube.com/watch?v=Ij4oKVn1Qso)` |
| `![](https://youtu.be/...)` | YouTube embed (short URL) | `![](https://youtu.be/Ij4oKVn1Qso)` |

The segment splitter (DraftsEndpoint.cs) already splits on `---`. No changes needed there — the markdown tags travel with their segment text.

---

### Segment Validation

#### [MODIFY] DraftsEndpoint.cs

Add a helper `AnalyzeSegmentMedia(string segmentContent)` that returns:
- `imageIds: Guid[]` — media:// references
- `youtubeUrl: string?` — first YouTube embed URL detected
- `hasConflict: bool` — true if both are present

This is used at publish time to warn or block.

---

### YouTube Metadata Extraction

YouTube pages are JS-rendered SPAs, so our `HttpClient` scraper gets an empty shell. Instead we use a **two-step pipeline** that requires **no API key**:

#### Step 1 — oEmbed (fast, always works)

```
GET https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v={videoId}&format=json
```

Returns (no auth needed):

| Field | Example |
|-------|--------|
| `title` | "My Video Title" |
| `author_name` | "Channel Name" |
| `author_url` | `https://www.youtube.com/channel/UC...` |
| `thumbnail_url` | `https://i.ytimg.com/vi/.../hqdefault.jpg` |

This gives us the channel URL, from which we extract the `channel_id`.

#### Step 2 — Channel ATOM Feed (enrichment, best-effort)

Using the channel ID from oEmbed's `author_url`:

```
GET https://www.youtube.com/feeds/videos.xml?channel_id={channelId}
```

This is a standard Atom + Media RSS feed. We find the `<entry>` whose `<yt:videoId>` matches and extract:

| ATOM / MRSS Element | What we get |
|---------------------|------------|
| `<media:description>` | Full video description |
| `<media:statistics views="...">` | View count |
| `<published>` | Publish date |
| `<media:starRating>` | Rating stats |
| `<media:thumbnail>` | Higher-res thumbnail URL |

#### Implementation

#### [MODIFY] SourcesEndpoint.cs

Update `ReconcileSourcesAsync` to detect YouTube URLs in the markdown content. When a YouTube link is found:

1. **Detect**: Regex matches `https?://(www\.)?youtube\.com/watch\?v=([\w-]+)` and `https?://youtu\.be/([\w-]+)`.
2. **oEmbed fetch**: `GET https://www.youtube.com/oembed?url={url}&format=json` -> deserialize to get `title`, `author_name`, `author_url`, `thumbnail_url`.
3. **Extract channel ID**: Parse `author_url` — it's either `/channel/UC...` (direct ID) or `/@handle` (need one more redirect, or skip).
4. **ATOM feed fetch** (best-effort): `GET https://www.youtube.com/feeds/videos.xml?channel_id={channelId}` -> parse XML, find matching `<yt:videoId>`, extract `<media:description>` and `<media:statistics>`.
5. **Create Source row**: `Kind = SourceKind.YouTube`, `Reference = url`, `Title = oEmbed title`, `Content` = assembled markdown:

```markdown
# {title}
By {author_name} | {views} views | Published {date}

{description}
```

This cached content is what the LLM reads via `fetch_source` — so it gets the real video description instead of hallucinating.

**Fallback**: If the ATOM feed step fails (channel uses `/@handle` without a `/channel/` URL, or rate-limited), we still have the oEmbed data. The Source row is created with title + author only. This is strictly better than the current empty-shell HTML scrape.

#### Why not the YouTube Data API v3?

It requires a Google Cloud API key and has quota limits (10,000 units/day). The oEmbed + ATOM approach is:
- Zero configuration (no API key)
- Sufficient for our needs (title, description, thumbnail, view count)
- Consistent with the local-first, no-external-accounts philosophy

---

### Frontend — Editor

#### [MODIFY] MarkdownEditor.tsx

- Expand `handleDrop` to accept image files (`.jpg`, `.jpeg`, `.png`, `.webp`, `.gif`).
- On image drop: call `uploadMedia(draftId, file)` -> insert `![filename](media://guid)` at cursor.
- Handle `paste` event for clipboard images.

#### [MODIFY] drafts.ts

- Add `uploadMedia(draftId, file)` API function -> `POST /api/drafts/{draftId}/media`.
- Add `MediaAssetDto` type.

#### [MODIFY] draftStore.ts

- Add `uploadMedia` action wrapping the API call.

---

### Frontend — Thread Preview

#### [MODIFY] ThreadPreview.tsx

Each `PreviewCard` currently renders text only. Extend it to:

1. **Parse `![alt](media://guid)`** -> render `<img>` with `src="/api/media/{guid}"` and alt text. Click to edit alt text inline.
2. **Parse `![](youtube-url)`** -> render a YouTube thumbnail card (oEmbed-style) with:
   - Thumbnail from `https://img.youtube.com/vi/{videoId}/hqdefault.jpg`
   - Play button overlay
   - Click opens YouTube in new tab
3. **Conflict warning badge**: If a segment has both images and a YouTube embed, show an amber warning: *"Bluesky: only images OR embed, not both"*.
4. Strip the markdown image tags from the rendered text content (so they don't show as raw markdown).

---

### Docker Compose

#### [MODIFY] docker-compose.yml

Add a named volume for uploads:

```yaml
services:
  api:
    volumes:
      - uploads:/app/uploads

volumes:
  pgdata:
  uploads:
```

---

### LLM Vision Integration

Hybrid approach: text metadata always in context, actual image pixels on-demand via tool — conditionally enabled based on detected model capabilities.

#### [MODIFY] ChatService.cs

**System prompt updates:**
- Explain `![alt](media://guid)` and `![](youtube-url)` syntax.
- Bluesky conflict rule: images OR embed, not both per segment.
- If vision available: describe `view_image` tool and instruct *"When an image has no alt text, call view_image and suggest concise alt text."*
- If vision unavailable: *"Vision is not available with the current model. You can still reference images by their metadata (filename, dimensions)."*

**Image metadata in system prompt** — when building the prompt, query `MediaAssets` for the active draft and append:

```
--- ATTACHED IMAGES ---
1. sunset.jpg (media://a1b2...) - 1920x1080, 450 KB, alt: ""
2. chart.png (media://c3d4...) - 800x600, 120 KB, alt: "Q3 revenue"
```

This is always included (cheap, text-only) so the LLM knows what images exist without spending vision tokens.

**New tool: `view_image`** — registered alongside `list_sources` and `fetch_source` **only when `SupportsVision == true`**:

| Tool | Schema | Returns |
|------|--------|---------|
| `view_image` | `{ "id": "<guid>" }` | Multimodal tool response with base64 thumbnail |

**Tool execution flow:**
1. Look up `MediaAsset` by ID, verify it belongs to the active draft.
2. Read the file from disk.
3. Resize to max 512px on longest dimension using `SkiaSharp` (keeps vision cost to ~85-170 tokens).
4. Encode as JPEG, base64.
5. Return as an OpenAI-compatible multimodal content part:

```json
{
  "role": "tool",
  "content": [
    { "type": "text", "text": "Image: sunset.jpg (1920x1080). Current alt text: (none)" },
    { "type": "image_url", "image_url": { "url": "data:image/jpeg;base64,..." } }
  ]
}
```

**Use cases this unlocks (vision-capable models only):**
- *"Describe this image"* -> LLM calls `view_image`, writes a description.
- *"Write alt text for all images"* -> LLM iterates through the attached images list, calls `view_image` for each, suggests alt text.
- *"Write a thread about this photo"* -> LLM sees the image and drafts content that references what's actually in it.
- Drop image with empty alt -> system prompt nudges LLM to suggest alt text proactively.

---

### Model Capabilities Detection

Probe the provider API at startup to determine whether the active model supports vision. The `view_image` tool is only registered when vision is confirmed.

#### [NEW] ModelCapabilities.cs

```csharp
public record ModelCapabilities(bool SupportsVision, bool SupportsTools);
```

#### [NEW] ModelCapabilityProbe.cs

A service that resolves capabilities per provider type:

| Provider | Detection Method | Vision Check |
|----------|-----------------|--------------|
| **OpenRouter** | `GET {BaseUrl}/../models` (same base, models endpoint) | `architecture.input_modalities` contains `"image"` |
| **Ollama** | `POST {OllamaBaseUrl}/../api/show` with `{ "name": "{model}" }` | `capabilities` array contains `"vision"` |
| **OpenAI** | Hardcoded allowlist | Model ID matches `gpt-4o*`, `gpt-4-vision*`, `gpt-5*`, `o1*`, `o3*`, `o4*` |

**Caching**: Results are cached in-memory (`IMemoryCache`) keyed by `"{providerType}:{model}"` with a 1-hour TTL. No DB changes needed.

**Startup flow**:
1. `ChatService.StreamAsync` calls `ModelCapabilityProbe.GetCapabilitiesAsync(provider)` before building the tool list.
2. If `SupportsVision == true`, the `view_image` tool definition is included in the `tools` array.
3. If `SupportsVision == false`, the tool is omitted entirely — the LLM never sees it, so it can't attempt to call it.

#### [MODIFY] ChatService.cs

- Inject `ModelCapabilityProbe` via constructor.
- Before building the tools list, call `var caps = await _probe.GetCapabilitiesAsync(provider);`
- Conditionally add `view_image` tool definition only if `caps.SupportsVision`.
- Conditionally adjust system prompt vision instructions.

#### [MODIFY] Program.cs

- Register `ModelCapabilityProbe` as a singleton service.
- Add `builder.Services.AddMemoryCache()` for capability caching.


---

## Verification Plan

### Automated Tests
- `docker compose exec api dotnet test` — existing + new unit tests for `AnalyzeSegmentMedia`, YouTube oEmbed parsing, thumbnail generation.
- `docker run --rm -v ./web:/src -w /src node:22-alpine npm run test` — existing + new tests for image/YouTube rendering in ThreadPreview.

### Manual Verification
1. Drop a `.jpg` into the editor -> confirm `![filename](media://guid)` is inserted.
2. Switch to Preview -> confirm the image renders inline in the segment card.
3. Type `![](https://www.youtube.com/watch?v=Ij4oKVn1Qso)` -> confirm YouTube card renders in preview with thumbnail.
4. Put both an image and YouTube in same segment -> confirm amber conflict warning.
5. `GET /api/media/{id}` serves the image correctly in browser.
6. Paste a YouTube link in the draft -> confirm Source row created with `SourceKind.YouTube`, real description cached.
7. Ask the LLM *"describe the image I just dropped"* -> confirm it calls `view_image` and returns an accurate description.
8. Drop an image with no alt text -> confirm LLM suggests alt text.
9. Test with a text-only Ollama model -> confirm `view_image` gracefully falls back to text metadata.
10. Delete a draft -> confirm cascade removes MediaAsset rows (file cleanup is a stretch goal).
