# v2 Plan: Sources Library + YouTube Transcripts

## Progress Update (2026-07-14)

### Completed

- `DraftSource` junction model implemented and wired through API/service queries.
- Source library search and cross-draft linking endpoints implemented.
- Python transcriber service integrated via Docker Compose (`transcriber` service).
- YouTube transcript extraction queued asynchronously from API background jobs.
- Transcript status polling endpoint implemented and used by web UI.
- Retry transcription endpoint implemented (`POST /api/sources/{sourceId}/retry-transcription`).
- Source UI now supports retrying failed YouTube transcriptions.
- Source preview UX improved for YouTube with tabbed interface (`Video` / `Transcript`) to avoid squeezed transcript view.
- Deterministic local Whisper integration test added in transcriber (`transcriber/tests/test_whisper_local_integration.py`).

### Notes

- External YouTube access remains intermittently network-dependent (connection refused / 429 can still occur).
- Library-first transcript retrieval + retry flow now provides better resilience and recovery path from UI.

## Overview

Build a global, searchable sources library using Postgres FTS (full-text search), enable source reuse across drafts via `DraftSource` junction table, and automatically extract YouTube transcripts via a separate Python microservice (yt-dlp + Whisper + configurable summarization).

## Architectural Decisions

### KQ Answers (User)
- **KQ1 - Reuse UX**: Option B - Allow linking (new junction table `DraftSource`)
- **KQ2 - Subtitle Language**: Whisper autodetection (mostly English at first)
- **KQ3 - Transcript Size**: Keep full transcript AND summary. Also implement summary for all text sources
- **KQ4 - Failure Handling**: Mark as unavailable if yt-dlp fails
- **KQ5 - Async Processing**: Async with progress status visible to user
- **KQ6 - Architecture**: Separate Python microservice (yt-dlp + whisper) in own container, REST API, saves transcripts to mounted volume

### Final Decisions

| Decision | Choice |
|---|---|
| Communication | HTTP REST API (.NET ↔ Python service via HttpClient) |
| Transcript Storage | Hybrid (volume for archive, summary in DB for search) |
| Summary Generation | Python service (simpler, self-contained) |
| DraftSource Migration | Automatic EF migration (create entries from existing Source.DraftId) |
| Status Polling | Poll `GET /api/sources/{id}/status` every 2s |
| Summary Scope | Implement for all text sources (YouTube + URL + PDF + Notes) |
| LLM Source Tools | `search_sources` tool (find existing) + UI linking (add existing manually) |
| Source Origin Display | Simpler MVP (no origin/linked draft tracking yet) |
| Python Health Check | Yes, check on startup + periodic pings |
| Summarization Engine | **Configurable** (Ollama or OpenRouter via env vars) |
| Python Service Startup | Always start with docker compose (required for YouTube features) |

---

## Phase 1: Data Model & Migrations

### New Entities

**New Table: `DraftSource`**
- Primary Key: (DraftId, SourceId)
- Foreign Keys: DraftId (→ Draft), SourceId (→ Source)
- Unique Index: (DraftId, SourceId)
- Purpose: Enables source reuse across multiple drafts

### Source Entity Changes

**Add Columns:**
- `Summary` (nvarchar(max), nullable) — for all text sources
- `TranscriptStatus` (enum: Pending / Processing / Complete / Failed, default Pending)
- `TranscriptPath` (nvarchar(500), nullable) — path relative to volume mount, e.g., `abc123.json`
- `YoutubeVideoId` (nvarchar(11), nullable) — extracted from URL, for future YouTube embeds

**Remove:**
- `DraftId` FK (data migrates to DraftSource junction table)

### EF Migration

1. Create `DraftSource` table
2. Create unique index: `IX_DraftSource_DraftId_SourceId`
3. Add new columns to `Source`
4. Data migration: Insert into `DraftSource` for all existing `Source` rows using `Source.DraftId`
5. Drop `Source.DraftId` column

**Migration Verification:**
- Verify all existing sources have corresponding `DraftSource` entries
- No orphaned sources (every Source has at least one DraftSource entry)

### FTS Index Setup (Postgres)

Add to migration:
```sql
-- Add tsvector column (generated, stored)
ALTER TABLE Sources 
ADD COLUMN ContentTsv tsvector 
GENERATED ALWAYS AS (to_tsvector('english', coalesce(Title, '') || ' ' || coalesce(Content, ''))) STORED;

-- Create GIN index for fast FTS queries
CREATE INDEX idx_sources_content_fts ON Sources USING GIN (ContentTsv);
```

---

## Phase 2: Python Microservice (`social-worker-transcriber`)

### Purpose
Handles video transcription and summarization asynchronously, decoupled from .NET API.

### Technology Stack
- Python 3.11
- FastAPI (lightweight REST API)
- `yt-dlp` (video download + subtitle extraction)
- `openai-whisper` (local transcription if needed)
- `requests` (HTTP calls to Ollama/OpenRouter for summarization)

### Dockerfile

```dockerfile
FROM python:3.11-slim

RUN pip install --no-cache-dir \
    fastapi \
    uvicorn \
    yt-dlp \
    openai-whisper \
    requests

COPY . /app
WORKDIR /app

EXPOSE 8000

CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
```

### REST API Endpoints

#### 1. Health Check
```
POST /health
Response: { "status": "ok" }
```

#### 2. Extract Transcript
```
POST /extract-transcript
Request: {
  "videoUrl": "https://youtube.com/watch?v=abc123",
  "outputPath": "/transcripts/abc123.json"
}
Response (success): {
  "status": "success",
  "transcriptPath": "abc123.json",
  "duration": 123,
  "language": "en"
}
Response (error): {
  "status": "failed",
  "error": "Video not found or region-locked"
}
```

Process:
1. Download video via yt-dlp
2. Extract subtitles (auto VTT or CC)
3. Parse VTT → plain text transcript
4. Save JSON: `{ "transcript": "...", "duration": 123, "language": "en" }`

#### 3. Summarize Text
```
POST /summarize
Request: {
  "text": "...",
  "maxLength": 500
}
Response (success): {
  "status": "success",
  "summary": "..."
}
Response (error): {
  "status": "failed",
  "error": "..."
}
```

Process:
1. Call Ollama or OpenRouter (configurable)
2. Return summary

### Environment Variables

```env
SUMMARY_ENGINE=ollama|openrouter
SUMMARY_URL=http://ollama:11434/api/generate
SUMMARY_API_KEY=...        # for OpenRouter only
SUMMARY_MODEL=llama2       # Ollama: local model; OpenRouter: remote model
```

### Volume Mount

- Host: `./transcripts/`
- Container: `/transcripts`
- Stores: `<uuid>.json` files with full transcript + metadata

### Error Handling

- Network timeout: Return 500
- Invalid URL: Return 400
- Missing subtitles: Return 404 with message "No subtitles available"
- Retry: Max 3 attempts with exponential backoff (backend responsibility)

---

## Phase 3: Backend (.NET)

### New Services

#### 1. YouTubeService
```csharp
public class YouTubeService
{
    public async Task<string?> ExtractVideoIdAsync(string url)
    public async Task<bool> IsYouTubeUrlAsync(string url)
}
```

Purpose: Validate and extract video IDs from various YouTube URL formats.

#### 2. TranscriptExtractionService
```csharp
public class TranscriptExtractionService
{
    public async Task<TranscriptExtractionResult> ExtractAsync(
        string videoUrl, 
        string outputPath, 
        CancellationToken ct)
}
```

Purpose:
- Call Python service `/extract-transcript` endpoint
- Retry logic: 3 attempts with exponential backoff
- Timeout: 30 minutes (configurable)
- Error handling: Return result with status + error message

#### 3. SummarizationService
```csharp
public class SummarizationService
{
    public async Task<string?> SummarizeAsync(string text, int maxLength, CancellationToken ct)
}
```

Purpose:
- Call Python service `/summarize` endpoint
- Works for all text sources (YouTube + URL + PDF + Notes)

### Updated SourcesService

**New Methods:**
```csharp
public async Task<List<SourceDto>> SearchSourcesAsync(
    Guid userId, 
    string query, 
    int page, 
    int pageSize, 
    CancellationToken ct)

public async Task LinkSourceAsync(
    Guid userId, 
    Guid sourceId, 
    Guid draftId, 
    CancellationToken ct)
```

**Refactored Methods:**
- `AddSourceAsync()` now:
  - Creates `Source` entity
  - Auto-creates `DraftSource` entry if draftId provided
  - Detects YouTube URLs → extracts video ID → queues `TranscriptExtractionJob`
  - Returns `SourceDto` with `transcriptStatus: "Pending"`

**Query Changes:**
- All queries now join through `DraftSource` table
- FTS queries use `ContentTsv` column

### New Endpoints

#### 1. Create Source
```
POST /api/sources
Request: {
  "kind": "Url|File|Note|Image",
  "reference": "...",
  "draftId": "<guid>",
  ...
}
Response: SourceDto {
  Id, Kind, Reference, Title, Summary, TranscriptStatus, AddedAt
}
```

#### 2. Search Sources (FTS)
```
GET /api/sources?query=...&page=1&pageSize=20
Response: {
  items: SourceDto[],
  total: int,
  page: int,
  pageSize: int
}
```

Query: `FROM Sources WHERE ContentTsv @@ plainto_tsquery('english', query)`

#### 3. Get Source Detail
```
GET /api/sources/{sourceId}
Response: SourceDetailDto {
  Id, Kind, Reference, Title, Content, Summary, TranscriptStatus, AddedAt
}
```

#### 4. Poll Transcript Status
```
GET /api/sources/{sourceId}/status
Response: {
  transcriptStatus: "Pending|Processing|Complete|Failed",
  summary: "...",
  error: "..."
}
```

#### 5. Link Source to Draft
```
POST /api/drafts/{draftId}/sources/{sourceId}/link
Response: DraftDto { updated with new source }
```

#### 6. Manually Trigger Summarization
```
POST /api/sources/{sourceId}/summarize
Response: {
  summaryStatus: "Pending|Complete|Failed"
}
```

### LLM Tools

#### New Tool: `search_sources`

Purpose: Allow LLM assistant to search the user's source library by keyword and suggest relevant sources.

```
Tool: search_sources
Input: {
  "query": "climate change policy",
  "limit": 5
}
Output: [
  {
    "id": "<uuid>",
    "title": "EPA Climate Action Initiative",
    "kind": "Url",
    "preview": "The EPA outlines federal climate policy...",
    "summary": "Federal climate policy framework and implementation",
    "isYoutube": false
  },
  ...
]
```

Use Case: User drafts: "I want to write about climate policy." LLM calls `search_sources("climate policy")` → finds 5 existing sources → suggests: "I found 5 sources about climate policy, including [title1], [title2]... Would you like me to add any of these to your draft?"

Behavior:
- User approves source suggestion → LLM calls `add_source` with existing sourceId (links, not duplicates)
- User rejects → LLM continues drafting
- LLM can proactively suggest sources during multi-turn conversation

### Existing LLM Tools (Updated)

#### Modified: `add_source`
Now supports two modes:

**Mode 1: Add New Source** (existing behavior)
```
Tool: add_source
Input: { "kind": "Url", "reference": "https://...", "draft_reference": "<draftId>" }
Output: { "sourceId": "...", "title": "...", "message": "Source added and linked to draft" }
```

**Mode 2: Link Existing Source** (new)
```
Tool: add_source
Input: { "sourceId": "<uuid>", "draft_reference": "<draftId>" }
Output: { "sourceId": "...", "title": "...", "message": "Existing source linked to draft" }
```

LLM can now call `search_sources` first, then use `add_source` with the resulting `sourceId` to link without duplicating.

### Background Jobs (TPL Queue)

#### TranscriptExtractionJob
- Triggered: When YouTube source is added
- Process:
  1. Call `TranscriptExtractionService`
  2. Update `Source.TranscriptStatus` to "Processing"
  3. On success: Save transcript to volume, update `Source.TranscriptPath`, call `SummarizationService`
  4. On failure: Set `Source.TranscriptStatus` to "Failed", log error
- Retry: 3 attempts with 5s, 15s, 45s delays

#### SummarizationJob (Future v2.1)
- Batch process sources without summaries
- Call `SummarizationService` for each
- Update `Source.Summary`

### HTTP Client Configuration

**appsettings.json:**
```json
{
  "Transcriber": {
    "BaseUrl": "http://transcriber:8000",
    "HealthCheckInterval": "60s",
    "TimeoutSeconds": 1800
  }
}
```

**Program.cs:**
```csharp
services.AddHttpClient<TranscriptExtractionService>(client =>
{
    var config = configuration.GetSection("Transcriber");
    client.BaseAddress = new Uri(config["BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(int.Parse(config["TimeoutSeconds"]!));
})
.AddPolicyHandler(GetRetryPolicy());
```

### Tests

- **YouTubeService**: Extract video ID from various URL formats
- **TranscriptExtractionService**: Mock HTTP calls, test retry logic + timeout
- **SummarizationService**: Mock HTTP calls
- **SourcesService.SearchSourcesAsync**: FTS queries, pagination, fuzzy matching
- **Endpoints**: POST /sources with YouTube → status polling → complete
- **Migration**: Verify DraftSource entries created correctly
- **Junction table**: Verify source queries work through DraftSource

**Target: 15+ new unit tests**

---

## Phase 4: Frontend

### New Route: `/sources`

URL: `https://social-worker.localtest/sources`

Layout:
```
┌─────────────────────────────────────────┐
│ 🔍 Search sources...    [FILTERS]       │
└─────────────────────────────────────────┘
┌─────────────────────────────────────────┐
│ SourceListItem #1                   │   │
│ SourceListItem #2   ⏳ Processing   │   │
│ SourceListItem #3                   │   │
├─────────────────────────────────────────┤
│ Page 1 / 5  [ < ]  [ > ]                │
└─────────────────────────────────────────┘
```

### Components

#### SourcesLibrary.tsx (Page)
- Query params: `?q=...&page=1&sort=...`
- Fetches: `GET /api/sources?query=...&page=...`
- Renders: Header + Search + List + Pagination
- State: `useSourceStore`

#### SourceListItem.tsx
Shows:
- Type badge: "URL" / "YouTube" / "PDF" / "Note"
- Title
- Preview: First 100 characters
- Status indicator: ✓ / ⏳ / ✗
- Date added
- Click → opens detail modal

#### SourceDetailModal.tsx
Shows:
- Full content preview (or "Transcribing..." spinner)
- Summary (if available)
- Buttons:
  - "Add to current draft" (calls `LinkSourceAsync`)
  - "Copy summary" (if available)
  - Close

#### SourceSearch.tsx
- Input: Text search with debounce (300ms)
- Triggers: `searchSources(query, page=1)`
- Filters (future):
  - Type: URL / YouTube / PDF / Note
  - Date range

### State Management (zustand)

**useSourceStore:**
```typescript
interface SourceStore {
  sources: SourceDto[]
  searchQuery: string
  currentPage: number
  isLoading: boolean
  fetchSources: (page: number) => Promise<void>
  searchSources: (query: string) => Promise<void>
  getSourceDetail: (sourceId: string) => Promise<SourceDetailDto>
  linkSourceToDraft: (sourceId: string, draftId: string) => Promise<void>
}
```

### Integration with ThreadPreview

When rendering code fence or displaying media:
- Show "Open in Sources Library" link
- Sources visible in draft: small badges with links to library entries

### Tests

- Search input → debounce → API call
- Pagination click → update URL query params + fetch
- Detail modal → fetch full content
- "Add to current draft" → call LinkSourceAsync → close modal → reload draft
- Transcript status polling (mock 2s interval)
- Error state: Show "Failed to load sources" message

---

## Phase 5: Docker & Deployment

### New Service: `transcriber`

**docker-compose.yml:**
```yaml
transcriber:
  build: ./transcriber
  container_name: social-worker-transcriber
  ports:
    - "8001:8000"
  volumes:
    - transcripts:/transcripts
  environment:
    - SUMMARY_ENGINE=${SUMMARY_ENGINE:-ollama}
    - SUMMARY_URL=${SUMMARY_URL:-http://ollama:11434/api/generate}
    - SUMMARY_MODEL=${SUMMARY_MODEL:-llama2}
    - SUMMARY_API_KEY=${SUMMARY_API_KEY:-}
  depends_on:
    - ollama
  networks:
    - default
  healthcheck:
    test: ["CMD", "curl", "-f", "http://localhost:8000/health"]
    interval: 30s
    timeout: 10s
    retries: 3
```

### Volume Mount

**docker-compose.yml:**
```yaml
volumes:
  transcripts:
    driver: local
    driver_opts:
      type: none
      o: bind
      device: ./transcripts
```

### Health Checks

- .NET periodically checks: `GET http://transcriber:8000/health`
- Ping interval: Configurable (default: 60s)
- If unhealthy: Log warning, retry transcript extraction later

### Environment Variables (.env)

```env
# Transcriber service
TRANSCRIBER_URL=http://transcriber:8000
TRANSCRIBER_HEALTH_CHECK_INTERVAL=60
TRANSCRIBER_TIMEOUT_SECONDS=1800

# Summarization (configurable)
SUMMARY_ENGINE=ollama|openrouter
SUMMARY_URL=http://ollama:11434/api/generate
SUMMARY_MODEL=llama2
SUMMARY_API_KEY=...  # for OpenRouter only
```

---

## Verification Checklist

### Backend
- [x] EF migration creates `DraftSource` table + indexes
- [x] Existing sources migrate to `DraftSource` automatically
- [x] `Source` entity has new columns: Summary, TranscriptStatus, TranscriptPath, YoutubeVideoId
- [x] Postgres FTS: `ContentTsv` column + GIN index working
- [x] YouTubeService: Extract video ID from URLs (various formats)
- [x] TranscriptExtractionService: Calls Python API, retry logic works
- [x] SummarizationService: Calls Python API
- [x] All 6 endpoints return correct DTOs
- [x] Background job: TranscriptExtractionJob queued + executed
- [x] Python health check: .NET can ping transcriber health endpoint
- [x] Source queries: Work through DraftSource junction table
- [x] **15+ new unit tests** (migration, services, endpoints, FTS, junction)

### Frontend
- [x] `/sources` route loads + renders list
- [x] Search input → debounce → API call → list updates
- [x] Pagination click → URL query params change → fetch new page
- [x] Detail modal → fetch full content + summary
- [x] "Add to current draft" → LinkSourceAsync → current draft reloads
- [x] ThreadPreview shows source badges (when applicable)
- [x] Status polling works (mock 2s interval)
- [x] No circular imports or TypeScript errors
- [x] **8+ new component tests** (search, pagination, modal, polling)

### Docker
- [x] `docker compose up` starts transcriber service
- [x] Transcriber health check passes: `curl http://localhost:8001/health`
- [x] Transcript files saved to `./transcripts/` on host
- [x] Python service can call Ollama (if configured)
- [x] No port conflicts (8001 for transcriber)
- [x] Volume mount works: Files persist across container restarts

### E2E Workflow
- [x] Add YouTube URL to draft
  - Transcript status shows "Processing" ⏳
  - Poll status endpoint → "Complete" ✓ when done
  - Transcript appears in Sources library
- [x] Add URL to draft
  - Fetched + cached
  - Appears in Sources library with preview
- [x] Search sources library
  - FTS finds content by keyword
  - Results paginate correctly
  - Filters work (type, date)
- [x] Link existing source to new draft
  - Source appears in new draft's sources
  - Deleting from one draft doesn't delete from other
  - Summary + transcript preserved

---

## Dependencies & Blockers

- Python packages: yt-dlp, whisper, fastapi, uvicorn, requests
- Database: Postgres 16+ (FTS support required)
- Docker: Multi-container compose (7+ services)
- No breaking changes: Old Source queries work via migration
- Backwards compatibility: Existing drafts auto-migrate

---

## Scope Boundaries

### In Scope (MVP v2)
- ✅ Sources library (search + browse)
- ✅ YouTube transcript extraction
- ✅ Source reuse via DraftSource junction
- ✅ Transcript + summary storage (hybrid)
- ✅ FTS for all sources
- ✅ Status polling for async jobs
- ✅ Python microservice (separate container)
- ✅ Configurable summarization engine
- ✅ LLM tool: `search_sources` (find existing sources by keyword)
- ✅ LLM tool: `add_source` updated (link existing sources without duplicating)

### Out of Scope (v2.1+)
- ❌ Source origin/linked draft display
- ❌ Batch summarization for existing sources
- ❌ Source versioning (history tracking)
- ❌ Scheduled transcript refresh
- ❌ Multi-language summarization
- ❌ YouTube comment scraping

---

## Implementation Order

1. **Database**: EF migration + FTS setup
2. **Python service**: Dockerfile + FastAPI app + endpoints
3. **Backend services**: YouTubeService, TranscriptExtractionService, SummarizationService
4. **API endpoints**: All 6 routes + tests
5. **Background jobs**: TranscriptExtractionJob + job queue
6. **Frontend**: `/sources` route + components + store
7. **Docker**: Add transcriber service to compose file
8. **E2E testing**: Manual workflow verification

---

## Notes

- **LLM source discovery**: `search_sources` tool empowers assistant to proactively suggest relevant existing sources, avoiding duplication and providing context-aware recommendations
- **Smart reuse**: `add_source` now supports linking existing sources without duplication (Mode 2)
- **No forced linking**: Users still have UI option to manually browse `/sources` and link
- **Async-first**: All transcript extraction is background + poll-based
- **Microservice rationale**: Python has better ecosystem for yt-dlp + whisper; separates concerns
- **Volume mount**: Allows users to archive transcripts outside DB for very long videos
- **Configurable summarization**: Users can choose local (free, slower) or remote (fast, costly)
- **FTS simplicity**: Using Postgres built-in rather than Elasticsearch keeps stack simpler
