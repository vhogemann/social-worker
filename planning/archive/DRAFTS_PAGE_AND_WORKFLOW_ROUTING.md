# Drafts Page Card Grid & Workflow-Grounded Routing Plan

## Overview
This implementation plan establishes **Workflows** as the primary organizational anchor across the entire app while providing a dedicated **Drafts Page (`/drafts`)** card grid to browse standalone and workflow-generated drafts.

The monolithic `DraftList` sidebar is **completely removed** from `ComposerPage`, transforming the composer into an expansive, full-width 2-pane workspace (`ChatPanel` ~45% + `EditorPanel` ~55%).

---

## Architectural Layout

```mermaid
graph TD
    subgraph Primary Grid Dashboards
        WF[WorkflowsPage (/)]
        TPL[TemplateStudioPage (/templates)]
        SRC[SourcesLibraryPage (/sources)]
        DRF[DraftsPage (/drafts)]
    end

    subgraph Detail & Workspace Views
        WF --> WFDetail[WorkflowDetailPage (/workflows/:id)]
        WFDetail -->|Click generated draft| WFDraftComp[ComposerPage (/workflows/:id/draft/:draftId)]
        
        DRF -->|Click draft card| AdhocComp[ComposerPage (/draft/:draftId)]
        
        subgraph Full-Width 2-Pane Composer Workspace
            WFDraftComp --> Chat1[ChatPanel (~45%)]
            WFDraftComp --> Edit1[EditorPanel (~55%)]
            AdhocComp --> Chat2[ChatPanel (~45%)]
            AdhocComp --> Edit2[EditorPanel (~55%)]
        end
    end
```

---

## Routing & Breadcrumbs Matrix

| Route | View Component | Breadcrumbs | Description |
|---|---|---|---|
| `/` or `/workflows` | `WorkflowsPage` | `Workflows` | Workflows list & management |
| `/workflows/:id` | `WorkflowDetailPage` | `Workflows` > `{WorkflowTitle}` | Workflow dashboard & generated drafts card grid |
| `/workflows/:workflowId/draft` | `ComposerPage` | `Workflows` > `{WorkflowTitle}` > `New Draft` | Creates/opens a draft tied to this workflow |
| `/workflows/:workflowId/draft/:draftId` | `ComposerPage` | `Workflows` > `{WorkflowTitle}` > `{DraftTitle}` | Full-screen 2-pane composer for workflow draft |
| `/drafts` | `DraftsPage` | `Drafts` | Card grid of all/standalone/archived drafts |
| `/draft/:draftId` | `ComposerPage` | `Drafts` > `{DraftTitle}` | Full-screen 2-pane composer for standalone draft |
| `/templates` | `TemplateStudioPage` | `Templates` | Template studio card grid |
| `/sources` | `SourcesLibraryPage` | `Sources` | Sources library card grid |
| `/settings/:tab` | `SettingsPage` | `Settings` > `{TabName}` | Settings dashboard |

---

## Key Changes by Area

### 1. Backend: Hierarchical Drafts with Workflow Metadata (`api/SocialWorker.Api/`)
- In `DraftDto`:
  - `Guid? AutomationWorkflowId = null`
  - `string? AutomationWorkflowTitle = null`
  - `List<DraftDto>? Variants = null`
- `GetDraftsForUserAsync` returns canonical drafts with their child platform variants pre-nested in `Variants`.
- `GetDraftByIdAsync` populates `Variants` and `AutomationWorkflow` metadata.

### 2. Frontend: `DraftsPage.tsx` (`web/src/components/Drafts/`)
- **`DraftCard.tsx`**: Card component displaying title, status, platform badges, variant count, timestamp, and actions (rename, archive, delete).
- **`DraftsPage.tsx`**: Full-screen grid view with search input, filter tabs (**All**, **Standalone**, **Workflow-Generated**, **Archived**), and `+ New Draft` button (opens `CreateDraftModal`).

### 3. Frontend: Full-Width 2-Pane `ComposerPage.tsx` (`web/src/components/Composer/`)
- Completely removes `DraftList` sidebar and sidebar toggle.
- `useComposerDraftSync` reads `{ workflowId, draftId }` from `useParams()`, auto-initializing the draft and editor store.

### 4. Frontend: Navigation & Breadcrumbs (`web/src/components/Navigation/`)
- `AppHeader.tsx`: `Workflows` (`/`), `Drafts` (`/drafts`), `Templates` (`/templates`), `Settings` (`/settings`).
- `Breadcrumbs.tsx`:
  - `/drafts` → `Drafts`
  - `/draft/:id` → `Drafts` > `{DraftTitle}`
  - `/workflows/:wfId/draft/:id` → `Workflows` > `{WorkflowTitle}` > `{DraftTitle}`

---

## Verification Plan

1. **API Tests**:
   ```bash
   docker compose --profile tooling run --rm dotnet test SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj
   ```
2. **Web Tests (Vitest)**:
   ```bash
   docker compose --profile tooling run --rm web-test
   ```
3. **Web Image Build**:
   ```bash
   docker compose build web
   ```
