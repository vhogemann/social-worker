# Multi-Account Support per Platform

## Context & Objectives

Currently, users can only configure a single account per social platform (enforced by a composite unique index on `(UserId, Platform)` in the database). When creating a draft or adapting variants, the user selects from a list of abstract platforms (e.g. "Bluesky", "LinkedIn").

This feature enables **multi-account support per platform** (e.g. connecting both personal `@personal.bsky.social` and organization `@tech.bsky.social` accounts), and transitions draft creation, variant adaptation, and workflow automations to an **Account-first selection model**.

---

## Architectural & Domain Design

### 1. Database & Schema
- **Unique Constraint**:
  - Replace index `"IX_Accounts_UserId_Platform"` with composite unique index `"IX_Accounts_UserId_Platform_Handle"` on `("UserId", "Platform", "Handle")`.
  - Adding a different handle for an existing platform creates a new `Account` entity; re-saving an existing handle updates credentials and status.
- **Draft & Thread Linkage**:
  - Add optional `TargetAccountId` on `Draft` (referencing `Accounts(Id)` with `OnDelete(DeleteBehavior.SetNull)`).
  - Add optional `AccountId` on `PlatformThread` (referencing `Accounts(Id)` with `OnDelete(DeleteBehavior.SetNull)`).
  - Add optional `AccountId` on `AutomationWorkflowDestination` (referencing `Accounts(Id)` with `OnDelete(DeleteBehavior.SetNull)`).

### 2. Backend Endpoints & Domain Services
- **Accounts**:
  - `POST /api/accounts`: Match on `(UserId, Platform, Handle)` to either update or create a new `Account`.
  - `POST /api/accounts/linkedin/callback`: Match on `(UserId, Platform, authorUrn)` to update or create a new `Account`.
  - `GET /api/accounts`: Returns all connected accounts for the authenticated user.
- **Drafts & Variants**:
  - `POST /api/drafts`: Accepts `targetAccountId`.
  - `POST /api/drafts/{id}/variants`: Generates account-level variants for selected target accounts (allowing multiple variants for different handles on the same platform).
  - `DraftDto` and `PlatformThreadDto`: Expose `targetAccountId` and `accountHandle`.
- **Publishing**:
  - `PublishingEndpoint`: Look up `Account` by `thread.AccountId` (falling back to platform lookup if null).
  - `PublishPlatformTool`: Resolve target account by `AccountId` or `handle`.
  - `FeedAutoPublishService`: Publish to destination `AccountId`.

### 3. Frontend Workflow
- **`CreateDraftModal.tsx`**:
  - Renders the list of configured **Accounts** (`[🦋 Bluesky (@tech.bsky.social)]`, `[🦋 Bluesky (@personal.bsky.social)]`, `[💼 LinkedIn (John Doe)]`).
  - Sets both `targetPlatform` and `targetAccountId`.
  - If 0 accounts configured: renders empty state with shortcut link to `/settings/connections`.
- **`AdaptVariantsModal.tsx`**:
  - Renders selectable list of other configured accounts (excluding the source draft's account).
- **`DraftList.tsx` & `EditorPanel.tsx`**:
  - Displays platform icon + account handle (`🦋 @tech.bsky.social`) in sidebar rows and editor header.
- **`ConnectionsTab.tsx`**:
  - Groups connected accounts by platform card, allowing adding multiple handles per platform and managing them independently.
- **`WorkflowsPanel.tsx`**:
  - Configures automated workflow destinations by Account.

---

## Tasks & Phases

### Phase 1: Database Migration & Entities
- [x] Update `AppDbContext.cs` entity configurations for `Account`, `Draft`, `PlatformThread`, and `AutomationWorkflowDestination`.
- [x] Add migration script `0007_multi_account_support.sql`.
- [x] Update entity factory methods in `Draft.cs`, `PlatformThread.cs`, and `AutomationWorkflowDestination.cs`.

### Phase 2: API Endpoints & Publishing
- [x] Update `AccountsEndpoint.cs` upsert logic to key on `(UserId, Platform, Handle)`.
- [x] Update `DraftsEndpoint.cs` and `DraftsService.cs` (`CreateDraftAsync`, `GetDraftsForUserAsync`, `GetDraftByIdAsync`).
- [x] Update `PlatformVariantService.cs` to generate variants per target `AccountId`.
- [x] Update `PublishingEndpoint.cs`, `PublishPlatformTool.cs`, and `FeedAutoPublishService.cs` to resolve `Account` by `AccountId`.
- [x] Update `WorkflowsEndpoint.cs` to persist destination `AccountId`.
- [x] Add/update xUnit tests in `SocialWorker.Api.Tests` (Accounts, PlatformVariants, Drafts).

### Phase 3: Frontend UI & State
- [x] Update TypeScript DTOs in `web/src/api/` (`drafts.ts`, `workflows.ts`).
- [x] Update `CreateDraftModal.tsx` to list configured Accounts with handles and connect link when 0 accounts exist.
- [x] Update `AdaptVariantsModal.tsx` to select destination accounts.
- [x] Update `DraftList.tsx` to display `@handle` alongside platform badges.
- [x] Update `ConnectionsTab.tsx` and `WorkflowsPanel.tsx` for multi-account management and workflow destinations.
- [x] Update frontend tests in `web/src/`.

### Phase 4: Verification & Docker Validation
- [x] Rebuild and run unit tests in Docker (`dotnet test` - 310 tests pass, `vitest` - 259 tests pass).
- [x] Rebuild images and verify container startup (`docker compose build && docker compose up -d`).
- [x] Verify end-to-end multi-account publishing workflow.
