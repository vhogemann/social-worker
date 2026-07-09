# REFACTORING_PLAN.md — API Refactoring and Decoupling Plan

This plan tracks the scan of the API features, identifies refactoring opportunities to decouple complex logic from Minimal API endpoints, and lists progress.

---

## 1. Namespace Tidiness Scan (Worst to Best)

| Priority | Feature Area | Primary Endpoint File | Lines | Coupling / Gnarliness Description |
|---|---|---|---|---|
| **1** | **Sources** | `SourcesEndpoint.cs` | 460 | **Worst.** Mixes HTTP routing with PDF text extraction (`PdfPig`), HTML scraping and cleaning (`HtmlAgilityPack`), YouTube oEmbed metadata fetching, RSS XML feed parsing, and background `Task.Run` workers. |
| **2** | **Media** | `MediaEndpoint.cs` | 255 | **Second Worst.** Mixes HTTP routing with SHA256 stream hashing, SkiaSharp image decoding/resizing/re-encoding, and direct local directory/file system reads and writes. |
| **3** | **Drafts** | `DraftsEndpoint.cs` | 431 | **Fair.** Moves some helpers into static methods, but still mixes draft and segment reconciliation, cascade deletion folder checks, and platform constraints validation (e.g. Bluesky media rules) within endpoint routing. |
| **4** | **Providers** | `ProvidersEndpoint.cs` | 280 | **Tidy.** Simple CRUD with minor deactivation and default status rules. |
| **5** | **Auth / Users** | `AuthEndpoint.cs` / `AuthService.cs` | - | **Best.** Properly separated into endpoint routes that delegate to a decoupled `AuthService`. |

---

## 2. Refactoring Targets

### Target A: Sources Refactoring (Priority 1)
Extract third-party integrations and parsing algorithms out of `SourcesEndpoint.cs`:
* **`SourceExtractor`**: Extract text parsing from `.pdf`, `.txt`, `.md` streams.
* **`WebScraperService`**: Extract oEmbed API scraping, YouTube XML RSS feed parsing, and `HtmlAgilityPack` logic.
* **`SourcesService`**: Orchestrate state reconciliation and manage background worker threads.

### Target B: Media Refactoring (Priority 2)
Extract graphic processing and local file storage logic out of `MediaEndpoint.cs`:
* **`ImageResizer`**: Decouple SkiaSharp-specific resizing, re-encoding, and quality setting.
* **`FileStorageProvider`**: Encapsulate file system checks, directory creation, file writes, and directory cleanup.
* **`MediaService`**: Orchestrate stream SHA256 hashing, deduplication checks, scaling execution, and DB updates.

### Target C: Drafts Refactoring (Priority 3)
Extract business rules and reconciliation out of `DraftsEndpoint.cs`:
* **`DraftsService`**: Encapsulate segment reconciliation, cascade deletes, and platform-specific validation rules (e.g., Bluesky post limits).

---

## 3. Progress Tracking

- [x] **ChatService Refactoring & Namespace Grouping** (Completed)
- [x] **Sources Feature Refactoring** (Completed)
- [x] **Media Feature Refactoring** (Completed)
- [/] **Drafts Feature Refactoring** (In Progress)
