# TO_REFACTOR

Refactoring opportunities identified from codebase scan on 2026-07-15.

## Scope and method

- Scanned API and web source files for size hotspots, repeated patterns, and risky error-handling patterns.
- Used `find + wc -l` for large-file hotspots and `grep` for exception and duplication markers.
- Excluded generated migration designer files from recommendations unless explicitly noted.

## Priority list

# TO_REFACTOR

Refactoring opportunities identified from the 2026-07-15 scan have now been executed and verified.

## Completed items

### 1) Consolidate Bluesky validation logic duplicated across endpoints/tools

Status: Completed

Implemented:

- Added shared `BlueskyContentValidator`.
- Replaced duplicated Bluesky validation logic in publish endpoints/tools/policy flows.
- Added validator-focused tests.

### 2) Split SourcesService into bounded sub-services

Status: Completed

Implemented:

- Extracted `SourceReconciliationService`.
- Extracted `SourceTranscriptionService`.
- Extracted `SourceSearchService`.
- Kept `SourcesService` as a thinner orchestrating facade.

### 3) Decompose ChatService into interaction pipeline components

Status: Completed

Implemented:

- Extracted `ChatSlashCommandService`.
- Extracted `ChatRequestPreparationService`.
- Extracted `ChatToolExecutor`.
- Extracted `ChatRoundProcessor`.
- Fixed the assistant-ui stream message id contract by honoring `unstable_assistantMessageId`.

### 4) Decompose DraftsService by responsibility

Status: Completed

Implemented:

- Extracted `DraftChatSummaryService`.
- Extracted `DraftSegmentService` for segment parsing, reconciliation, and media analysis.
- Kept `DraftsService` as the primary facade with compatibility forwarding where needed.

### 5) Remove catch-and-rethrow / non-logged exception catches

Status: Completed

Implemented:

- Removed redundant catch/rethrow behavior.
- Added structured logging for chat request parse failures and other affected paths.

### 6) Normalize media-markdown handling into one utility API

Status: Completed

Implemented:

- Expanded shared media parsing utilities in `SharedPatterns`.
- Migrated callers to shared strip/parse/count helpers.

### 7) Break up large frontend settings and sources panels

Status: Completed

Implemented:

- Extracted `useProvidersManager` from `ProvidersTab`.
- Extracted `useSourcesPanelManager` from `SourcesPanel`.
- Extracted `useDraftListManager` from `DraftList`.

### 8) Clarify publisher registration and dispatch strategy

Status: Completed

Implemented:

- Added `IPublisherResolver` and `PublisherResolver`.
- Removed duplicated publisher lookup logic from endpoint/tool call sites.

## Verification

- API Docker build passes.
- API test suite passes.
- Full Playwright e2e suite passes (`15/15`).

## Remaining work

- No open items remain from the 2026-07-15 refactor scan.

