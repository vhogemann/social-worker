# Tech Debt & Quick Wins

Inventory of low-effort improvements, code smells, missing tests, and minor bugs identified by scanning the full codebase.

---

## Quick Wins (Minutes, single-file changes)

### Bugs / Config

- [x] **`.env.example:12` — two settings concatenated on one line** (`Auth__RefreshTokenLifetimeDays=7Auth__DbEncryptionKey=...`). Missing newline — silently breaks config for anyone copying `.env.example` directly. *(Fixed: split onto separate lines)*

- [x] **`ProposeStageTransitionTool.cs:15` — stale stage names in LLM description.** References "Sourcing, Refining, Formatting, Sent" but `PlatformThreadStage` enum only has `Draft` and `Sent`. The model will emit invalid transitions. *(Fixed: description now reads "Draft, Sent")*

### Dead Code & Unused Imports

- [x] **`ModelCapabilityProbe.cs:145-156` — `IsKnownVisionModel` method defined but never called.** Remove. *(Removed)*

- [ ] **Unused `using` imports:** All flagged imports either are legitimately used or are covered by `<ImplicitUsings>enable</ImplicitUsings>` (redundant but harmless). Skipped as style-only.

### Return Type Hygiene

- [x] **`publishPlatformThread` in `web/src/api/drafts.ts:141` — returns `Promise<any>`.** Define a proper interface. *(Added `PublishResult` interface)*

- [x] **`updateUser` in `web/src/api/auth.ts:102` — uses `Record<string, any>`** for request body. *(Changed to `Partial<Pick<UserDto, ...>>`)*

### Antipatterns

- [ ] **`ListSourcesTool.cs:14` — empty args record** for a parameterless tool invocation. Removing would cascade through generic type parameter; not worth the churn.

- [x] **`ChatService.cs:63` — hardcoded "last 10 messages" limit** sent to LLM. *(Extracted to `MaxContextMessages` constant; full config extraction deferred)*

- [ ] **`ViewImageTool.cs:115` — uses deprecated `SKFilterQuality.Medium`** in SkiaSharp. *(Requires SkiaSharp 3.0 upgrade to use replacement `SKSamplingOptions`; deferred)*

---

## Empty Catch Blocks (at-minimum logging needed)

- [x] `WebScraperService.cs:80,132` — empty `catch { }` *(Added `Console.Error.WriteLine`)*
- [x] `SystemPromptBuilder.cs:65-68` — empty `catch { // Fallback }` *(Added `Console.Error.WriteLine`)*
- [x] `FileStorageProvider.cs:37,43` — empty `catch { }` *(Added `Console.Error.WriteLine`)*

---

## Tech Debt (Single-component changes)

### Extraction / Deduplication

- [x] **DraftDto construction duplicated 4x** in `DraftsService.cs:99,159,187,280`. Extract a `ToDto` helper. *(Done: extracted to `ToDto(Draft, List<PlatformThread>, List<Post>, List<MediaAsset>)` private method)*

- [x] **`GetUserId(ClaimsPrincipal)` duplicated across 7 endpoint files.** Extract to an extension method on `ClaimsPrincipal`. *(Done: `ClaimsPrincipalExtensions.GetUserId()` added to `Infrastructure/`, replaced 15 inline call sites in 7 files)*

- [x] **LlmProvider lookup logic duplicated 3x** (`ChatSessionLoader.cs:43`, `DraftsService.cs:450`, `GeneratePlatformVariantsTool.cs:72`). Extract to a shared service. *(Done: `LlmProviderService.GetProviderForUserAsync()` added to `Infrastructure/Llm/`, registered in DI, 3 call sites updated)*

- [x] **Media regex pattern duplicated 3x** (`BlueskyPublisher.cs:28`, `DraftsService.cs:57`, `ValidateDraftTool.cs:22`). Extract to a shared constant. *(Done: `SharedPatterns.MediaRegex` added to `Infrastructure/`, all 3 sites use it; note: DraftsService regex was slightly different (no alt-text capture group), unified to the capturing variant and group index adjusted from 1→2)*

- [x] **Sqlite test setup duplicated ~12 lines across every test class.** Extract to a test base class or fixture. *(Done: `SqliteTestBase` abstract class with `CreateDbContext()`, `CreateSeedUser()`, and `Cleanup()` virtual method; `DraftTests`, `PlatformVariantTests`, `SourcesServiceTests`, `LlmProviderTests` updated)*

### DI / Architecture Smells

**Phase 1 — Static Bridges (Items 1-2)**

- [x] **`DraftsEndpoint.cs:142` — instantiates `DraftsService` with `null!`** as a static bridge. Fragile DI workaround.  
  *Done: Removed `ReconcileSegmentsAsync` static bridge from `DraftsEndpoint`. `ReplaceEditorContentTool` and `RenderCodeBlocksTool` now resolve `DraftsService` from their existing DI scopes instead. (Pure static delegates `SplitMarkdownIntoSegments` and `AnalyzeSegmentMedia` retained.)*

- [x] **`SourcesEndpoint.ReconcileSourcesAsync` static bridge** (`SourcesEndpoint.cs:111-124`) — scope-factory workaround.  
  *Done: Removed static bridge. `DraftTests.cs` constructs `SourcesService` directly (same pattern already used in `SourcesServiceTests.cs`).*

**Phase 2 — Background Job Infrastructure (Items 3-4)**

- [x] **`DraftsService.TriggerBackgroundSummarization`** — fire-and-forget `Task.Run` with `CancellationToken.None`. No error recovery, no cancellation support. Extract to a background service.  
  *Done: Created `Infrastructure/Background/BackgroundJobQueue.cs` (bounded `Channel<Job>` wrapper) + `BackgroundJobHostedService.cs` (sequential `BackgroundService`). Registered in `Program.cs`. `TriggerBackgroundSummarization` now enqueues a job via the queue. The hosted service provides cancellation via `stoppingToken` and error logging via `ILogger`.*

- [x] **`SourcesService.ReconcileSourcesAsync:199`** — fire-and-forget `Task.Run` for background scraping. Silent failure if DB context is disposed.  
  *Done: Replaced `Task.Run` with the same shared `BackgroundJobQueue` enqueue. Uses the hosted service's cancellation token for graceful shutdown.*

### Functionality Gaps

- [x] **`BraveSearchEngine` doesn't implement `SearchImagesAsync`** — image search silently returns empty when using Brave provider. *(Fixed: extracted shared internal method, added `SearchImagesAsync` that queries Brave's images endpoint and extracts thumbnail URLs)*

- [x] **`web/src/api/chat.tsx` draft-switch race** — switching/creating drafts while a run is active could surface parent-message consistency errors in UI state. *(Fixed: persist previous draft chat, cancel in-flight run on draft switch, and bind save-on-stop to the originating draft id.)*

- [x] **`scripts/run-e2e.sh` omitted transcriber service** — e2e smoke failed with missing `social-worker-transcriber-e2e` image/container. *(Fixed: include `transcriber-e2e` in build and `up` service list.)*

### Future

- [ ] **DB-backed background job queue** — Replace the in-memory `Channel<Job>` with a `PendingJob` table. The `BackgroundJobQueue` would write to and read from the DB directly, giving durability across restarts and removing the bounded channel's drop behavior. Requires a `PendingJob` entity, migration, and rewriting the queue/hosted service to use DB polling. The current `Job` record (which embeds a `Func<CancellationToken, Task>`) would be replaced by a `JobType` discriminator + JSON payload, with the work function reconstructed from the payload on read.

---

## Missing Tests (by risk)

### Highest Risk — zero tests

| File | Lines | Why it matters |
|---|---|---|
| `BlueskyPublisher.cs` | 201 | **Only working publisher** — publishes real content to Bluesky |
| `ChatService.cs` | 254 | Core orchestrator for SSE streaming + tool execution |
| `AuthService.cs` | 131 | Login, refresh, logout — auth is the security boundary |
| `DraftsService.cs` (UpdateDraftAsync) | ~90 | Most complex service method (segment reconciliation, source sync) |
| `MediaService.cs` | ~100 | File uploads, resize, dedup — data-loss risk |
| `PublishingEndpoint.cs` | ~50 | Publish flow — can't verify without tests |
| `AuthService.cs` | 131 | Token lifecycle, password verification |

### Medium Risk — partial or no tests

- 9 of 13 Chat tools have **no tests** (only `WebSearchTool`, `ImageSearchTool`, `AddSourceTool`, `ValidateDraftTool` are covered)
- `CodeImageService.cs` — no service-layer tests (only Renderer + Parser tested)
- `WebScraperService.cs` — no tests
- `SourceExtractor.cs` — no tests (PDF, txt parsing)
- `ChatSessionLoader.cs`, `DraftTitleGenerator.cs`, `ChatStreamWriter.cs` — no tests
- `ModelCapabilityProbe.cs` — no tests
- `CryptoHelper.cs` — no tests

### Frontend

- **Only 1 test file** (`SourcesPanel.test.tsx`, 2 tests) for the entire React frontend
- **No tests** for: stores, API layer, chat panel, thread preview, editor, draft list, settings, login, auth guard
- `web/src/test/setup.ts` — only has a single import
- No e2e tests (`planning/future/E2E_TESTING.md` exists as reference strategy, but coverage work is still incomplete)
