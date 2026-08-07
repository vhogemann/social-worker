# SourcesService refactor plan

Status: `Completed`
Owner: `Copilot + user`
Started: `2026-07-19`
Last updated: `2026-07-19`

## Objective

Refactor `SourcesService` so source-type-specific behavior is extracted into focused services while preserving endpoint and chat-tool behavior.

## Scope

In scope:
- Extract `Url` + `YouTube` ingestion behavior from `SourcesService`
- Extract `File` ingestion behavior from `SourcesService`
- Extract transcript retry/lifecycle orchestration for YouTube into dedicated service
- Keep `SourcesService` as a thin orchestrator/facade
- Add/adjust tests to lock behavior and prevent regressions

Out of scope:
- Endpoint contract changes
- Chat tool output text contract changes (except bug-fix level corrections)
- Data model redesign beyond what is required for extraction

## Constraints

- Preserve current public method signatures on `SourcesService` during refactor phases.
- Preserve error behavior and key messages used by API endpoints and chat tools.
- Keep Docker-first validation flow for every phase.
- No cross-feature refactors unrelated to sources.

## Test-first protocol (mandatory)

- Before starting any phase, run the existing `SourcesServiceTests` class and confirm green.
- During extraction, prefer integration-style tests that use real source services and real orchestration wiring.
- Do not mock newly created internal source services.
- Mock/fake only edge dependencies:
	- outbound HTTP (`WebScraperService` client handler)
	- transcript extraction adapter (`ITranscriptExtractionService`)
	- summarization HTTP client behavior
	- database boundary via test database setup (Sqlite in-memory for integration tests)
- After each phase, re-run `SourcesServiceTests` first, then wider source/chat tests as needed.

## Architecture target

`SourcesService` keeps:
- Ownership/access checks
- Read/query and mapping paths (list/detail/status/link/delete/search/reconcile delegation)
- Delegation to source-type services

New focused services:
- `IFileSourceService` + implementation
- `IUrlSourceService` + implementation
- `IYouTubeSourceService` + implementation
- `ISourceUrlValidator` + implementation

Existing services kept and reused:
- `SourceSearchService`
- `SourceReconciliationService`
- `SourceTranscriptionService`
- `SummarizationService`

## Phase tracker

| Phase | Goal | Status | Exit criteria |
|---|---|---|---|
| 0 | Baseline + test locks | `Completed` | Build/tests green before edits; gaps identified and queued |
| 1 | Extract URL/YouTube ingestion | `Completed` | `AddUrlSourceAsync` delegates; behavior parity tests pass |
| 2 | Extract YouTube retry/lifecycle | `Completed` | `RetrySourceTranscriptAsync` delegates; queue/state tests pass |
| 3 | Extract File ingestion | `Completed` | `AddFileSourceAsync` delegates; dedupe/link tests pass |
| 4 | Slim SourcesService + DI cleanup | `Completed` | Lazy internal construction removed; constructor-injected deps only |
| 5 | Final verification + docs | `Completed` | Docker build/tests/up pass; plan and roadmap links updated |

## Detailed phase plan

### Phase 0: Baseline + test locks

Tasks:
- Run baseline API build and test in Docker.
- Run baseline `SourcesServiceTests` class before any code edits (phase gate).
- Convert/add tests toward integration style using real internal source services.
- Keep fakes/mocks only at external/edge boundaries (HTTP, transcript adapter, DB test harness).
- Confirm existing endpoint/chat tool contract tests are still representative.

Deliverables:
- Baseline command logs and pass/fail summary.
- Updated `SourcesServiceTests` strategy and first integration-style additions.
- Test additions (if needed) that fail before extraction and pass after.

### Phase 1: Extract URL/YouTube ingestion

Tasks:
- Introduce `IUrlSourceService` + implementation.
- Move URL validation/scrape/kind resolution/source creation into URL service.
- Trigger YouTube transcript queue through dedicated YouTube service API.
- Delegate `SourcesService.AddUrlSourceAsync` to URL service.
- Keep tests integration-oriented: instantiate real `SourcesService` + real extracted services together.

Deliverables:
- New URL and YouTube service files.
- `SourcesService` delegation path for URL add.
- Passing tests for URL and YouTube add flows.

### Phase 2: Extract YouTube retry/lifecycle

Tasks:
- Introduce `IYouTubeSourceService` + implementation.
- Move retry logic and draft link resolution from `SourcesService`.
- Keep `SourceTranscriptionService` as queue execution dependency.
- Delegate `SourcesService.RetrySourceTranscriptAsync`.
- Maintain queue/transcript tests through real service wiring; fake only transcript extraction edge.

Deliverables:
- YouTube lifecycle service files.
- `SourcesService` delegation path for retry.
- Passing retry/transcript queue tests.

### Phase 3: Extract File ingestion

Tasks:
- Introduce `IFileSourceService` + implementation.
- Move file hash/dedupe/extract/summarize/create/link logic.
- Delegate `SourcesService.AddFileSourceAsync`.
- Keep file-flow tests as integration-style with real service composition.

Deliverables:
- File source service files.
- `SourcesService` delegation path for file add.
- Passing file dedupe/link/extraction tests.

### Phase 4: Slim SourcesService + DI cleanup

Tasks:
- Remove lazy internal service creation from `SourcesService`.
- Wire all dependencies through constructor and DI registration.
- Optionally centralize URL validator usage in `AddSourceTool`.

Deliverables:
- Simplified `SourcesService` with orchestration-only responsibilities.
- Updated `SourcesExtensions` service registration.
- Test suite green.

### Phase 5: Final verification + docs

Tasks:
- Run final Docker build/test cycle.
- Run stack startup verification (`docker compose up --build`).
- Update this plan status and close open items.
- Keep planning index link and status current.

Deliverables:
- Final validation summary.
- `Status: Completed` and phase table updated.

## Working log

Use this section for concise progress updates while implementation is in flight.

- 2026-07-19: Plan created and linked from planning index.
- 2026-07-19: Baseline run complete: `SourcesServiceTests` green in Docker (17 passed, 0 failed).
- 2026-07-19: Test protocol updated to require `SourcesServiceTests` before each phase and integration-style evolution with edge-only fakes.
- 2026-07-19: Phase 1 implemented: extracted `ISourceUrlValidator`, `IYouTubeSourceService`, and `IUrlSourceService`; `SourcesService.AddUrlSourceAsync` now delegates to `UrlSourceService`.
- 2026-07-19: `SourcesServiceTests` evolved toward integration composition by wiring real extracted services in URL/YouTube add-flow tests.
- 2026-07-19: Post-change validation: `SourcesServiceTests` green in Docker (17 passed, 0 failed).
- 2026-07-19: Phase 2 implemented: moved YouTube retry transcription lifecycle into `IYouTubeSourceService`; `SourcesService.RetrySourceTranscriptAsync` now delegates.
- 2026-07-19: Phase 3 implemented: extracted file ingestion into `IFileSourceService`; `SourcesService.AddFileSourceAsync` now delegates.
- 2026-07-19: Added integration-style file ingestion tests (new file-add and dedupe/link scenarios) using real services and edge-only fakes.
- 2026-07-19: Phase 4 implemented: removed lazy internal service creation from `SourcesService`; constructor-time composition now wires all collaborators.
- 2026-07-19: Validation: `SourcesServiceTests` green after each phase gate (final count: 19 passed, 0 failed).
- 2026-07-19: Full validation green: `docker compose --profile tooling run --rm dotnet build`; `dotnet test /src/SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj` (241 passed, 0 failed).
- 2026-07-19: Runtime verification green: `docker compose up --build -d` started db/api/web/transcriber successfully.
- 2026-07-19: Follow-up API constructor-injection sweep completed for core services that still used fallback construction (`DraftsService`, `ChatService`, `DraftChatSummaryService`, `SourceReconciliationService`, `SourceTranscriptionService`, `SourcesService`, `BlueskyPublisher`) with DI/test wiring updated.
- 2026-07-19: Post-sweep validation green: `docker compose --profile tooling run --rm dotnet test /src/SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj` (241 passed, 0 failed).

## Open questions

- Should `AddSourceTool` consume shared `ISourceUrlValidator` in this refactor or be deferred to a follow-up cleanup?

## Validation commands

- `docker compose --profile tooling run --rm dotnet build`
- `docker compose --profile tooling run --rm dotnet test /src/SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj`
- `docker compose --profile tooling run --rm dotnet test /src/SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj --filter FullyQualifiedName~SourcesServiceTests`
- `docker compose up --build`
