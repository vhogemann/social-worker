# Plan: Frontend Integration for Decoupled Orchestration Workflows (Approved Design)

This document outlines the approved UI changes and component reuse strategy for surfacing the decoupled `AutomationWorkflow` functionality in the frontend (`web/`) project.

---

## Goal Description
Surfacing the orchestration workflows requires shifting the default mode of the application from ad-hoc single-thread drafting to automated multi-source multi-destination workflow management. 

The default entry screen (`/`) will become the **Workflows Dashboard**, showing the active orchestrations. The run backlog history will live in a separate **Logs** page.

---

## Component Reuse vs. Rewrite Analysis

### 1. Fully Reusable Components (No Rewrite)
- **`ComposerView` (`ChatPanel` + `EditorPanel`)**: 
  - **Verdict**: Reused 100%.
  - **Details**: When a background workflow generates a primary/canonical draft or platform-specific drafts, these drafts will appear in the `DraftList`. The user can click any draft (e.g., a draft flagged as `Pending Review` or `Failed Validation`) to open the Composer and use the chat/editor to manually refine and approve it.
  - **Modification**: A "Back to Dashboard" button will be added to the composer header to allow quick return.
- **`SourcesLibrary`**:
  - **Verdict**: Reused 100%.
  - **Details**: Tracks the underlying scraped contents and transcripts.

### 2. Refactored / Replaced Components
- **`FeedsPanel.tsx`**:
  - **Verdict**: Refactored into **`WorkflowsPanel` / `AutomationDashboard`**.
  - **Details**: Instead of just managing single feed subscriptions, this panel will now:
    1. Manage **Workflows**: Create/Edit `AutomationWorkflow` (Instructions prompt, title).
    2. Manage **Feed Sources Checklist**: In the workflow creation form, users select which feed sources they want to map to the workflow using a checklist.
    3. Manage **Destinations**: Users can choose target platforms. The form will only display platform options where the user has a connected/authenticated account.
- **`Logs` Page (New Page)**:
  - **Verdict**: Built by extracting the backlog list from the original `FeedsPanel`.
  - **Details**: A dedicated page displaying the run history, queue state, status (Waiting, Failed, Succeeded), and error logs.

---

## Route Schema in `App.tsx`
We will shift the default route `/` to point to the new Workflows Dashboard:
- `GET /` -> `WorkflowsPanel` (Default Entry Point / Workflows list)
- `GET /composer` or `GET /draft/:draftId` -> `ComposerView` (Persistent Composer link in the global navigation header for creating and editing ad-hoc drafts)
- `GET /logs` -> `LogsPanel` (Separate backlog / run history page)
- `GET /sources` -> `SourcesLibrary`

---

## Verification Plan

### Manual Verification
- Verify navigating to `http://localhost:8100/` displays the dashboard and workflows list.
- Verify clicking the "Composer" link in the global header correctly opens a clean canvas for ad-hoc drafts.
- Verify that clicking "Open Edit" or "Fix Draft" on a recent run successfully redirects to the ComposerView with the correct draft ID.
- Verify that only authenticated/connected accounts are listed in the workflow destination selector.
- Verify navigating to the separate "Logs" page displays the backlog items list correctly.
