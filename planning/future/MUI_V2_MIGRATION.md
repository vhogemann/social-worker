# Implementation Plan: Material UI (MUI) V2 Frontend Modernization

## Goal Description

Modernize the **social-worker** web application (`web/`) into a visually cohesive, professionally designed **V2** frontend powered by [Material UI (MUI v6)](https://mui.com/material-ui/).

---

## Architectural Principles & Agreed Decisions (via /grill-me)

### 1. Rollout Sequencing
- **Foundation-First (Horizontal Slice)**: Establish the MUI theme, responsive collapsible sidebar shell, and common primitives (`Button`, `Input`, `Card`, `Modal`) first so every existing view immediately sits within the new design system without layout regressions.

### 2. Layout & Responsive Behavior
- **Mobile First (`< 900px`)**:
  - Top header with hamburger button opens a responsive slide-over overlay `Drawer`.
  - Composer uses a clean top Tab Switcher (`[Chat] | [Draft & Preview]`) so neither pane is cramped on small viewports.
- **Desktop (`>= 900px`)**:
  - Permanent sidebar with two states: Expanded (`240px`) and Collapsed Rail (`68px` with hover tooltips).
  - **Fluid Push**: Collapsing the sidebar smoothly resizes the main workspace via CSS transition, maximizing space for the dual-pane Composer and Calendar grid.
  - **Sidebar Footer**: Houses the Light/Dark mode toggle and the Collapse trigger.
  - **Top Bar**: Minimal, dedicated to Breadcrumbs and contextual page actions.

### 3. Theme & Color Mode
- Dynamic Light/Dark palette powered by MUI `ThemeProvider` + `CssBaseline`.
- **MUI-Only Light Mode during Transition**: Mode toggle is active immediately; un-migrated legacy screens remain dark until converted to MUI.

### 4. Built-in MUI Component Priority
- Strictly prioritize built-in MUI components (`Card`, `CardHeader`, `Avatar`, `CardContent`, `CardActions`, `Chip`, `Tabs`, `Tab`, `Drawer`, `Dialog`, `Alert`, `LinearProgress`, `TextField`, `Switch`, `Menu`) over custom HTML container divs and hand-rolled borders.
- Re-implement `PreviewCard` (Thread Preview) using standard `Card`, `CardHeader` (avatar + platform chip), `CardContent`, and `CardActions`.
- Replace `@fortawesome/*` icons progressively with `@mui/icons-material`.

### 5. Strict Modal Policy (Modals for Simple Stuff Only)
Modals are strictly reserved for simple binary confirmations (e.g. Delete Confirm, Discard Changes) or brief alerts. Complex interactions have dedicated screens, inline forms, or slide-over drawers:

| Interaction | Previous Pattern | Agreed V2 Pattern | Rationale |
| :--- | :--- | :--- | :--- |
| **New/Edit Workflow** | `WorkflowFormModal` | Dedicated routes `/workflows/new` & `/workflows/:id/edit` | Clean multi-card configuration with full viewport width and native mobile scroll. |
| **Workflow Run History** | `WorkflowExecutionHistoryModal` | Embedded "Execution History" tab on `/workflows/:id` | Full data table for run status, item counts, error logs, and retry triggers. |
| **Workflow Import** | `WorkflowImportModal` | Dedicated route `/workflows/import` | JSON editor/uploader, validation feedback, and schema preview. |
| **New Draft** | `CreateDraftModal` | Direct-to-Composer | Clicking "New Draft" navigates straight to `/composer` with a blank draft. |
| **Calendar Slot Rules** | `SlotRuleFormModal` & `ScheduleManagerModal` | Dedicated route/tab `/calendar/rules` | Ample space for recurrence pacing, time windows, and queue capacity. |
| **Calendar Draft Assign** | `DraftPickerModal` | Slide-over Drawer | Keeps visual context of the calendar day/week while picking drafts. |
| **Settings RSS Feeds** | `FeedSubscriptionModal` | Inline Collapsible Form Card | Clicking "Add Feed" opens a card at the top of the feed list with live discovery feedback. |
| **Adapt Variants** | `AdaptVariantsModal` | Inline Composer Drawer / Panel | Side-by-side comparison with character count budgets without leaving editor context. |
| **Delete / Discard** | `ConfirmModal` | MUI `Dialog` | Remains a modal: simple binary confirm/cancel action. |

---

## Detailed Migration Roadmap

### Phase 1: Core Setup, Theme System & Light/Dark Mode
1. Install dependencies in `web/package.json`:
   - `@mui/material@^6.4.0`
   - `@mui/icons-material@^6.4.0`
   - `@emotion/react@^11.14.0`
   - `@emotion/styled@^11.14.0`
2. Create `web/src/theme/`:
   - `theme.ts`: Custom MUI theme factory supporting `light` and `dark` palettes.
   - `ColorModeContext.tsx`: Context & Hook for mode (`light` | `dark`) with local storage persistence and `prefers-color-scheme`.
3. Wrap `web/src/main.tsx` with `StyledEngineProvider`, `ColorModeProvider`, `ThemeProvider`, and `CssBaseline`.
4. Validate baseline build and test suites inside Docker (`docker compose --profile tooling run --rm web-test`).

---

### Phase 2: Responsive Shell & Permanent Collapsible Sidebar
1. Implement in `web/src/components/Navigation/`:
   - `AppLayout.tsx`: Master responsive shell container.
   - `AppSidebar.tsx`:
     - Mobile: `Drawer variant="temporary"`.
     - Desktop: `Drawer variant="permanent"` (Expanded: 240px / Collapsed mini-variant: 68px with tooltips). Fluid width push transition.
     - Bottom footer: Theme toggle (`LightModeOutlined` / `DarkModeOutlined`) and collapse toggle.
   - `AppTopBar.tsx`:
     - Mobile hamburger trigger.
     - Breadcrumbs.
     - Contextual page actions.
2. Update navigation item list with `@mui/icons-material`:
   - Workflows: `<HubOutlined />`
   - Reply Agents: `<SmartToyOutlined />`
   - Outbox: `<OutboxOutlined />`
   - Calendar: `<CalendarMonthOutlined />`
   - Drafts / Composer: `<EditNoteOutlined />`
   - Templates: `<DashboardCustomizeOutlined />`
   - Sources: `<SourceOutlined />`
   - Logs: `<TerminalOutlined />`
   - Settings: `<SettingsOutlined />`
3. Refactor `App.tsx` to mount `AppLayout`.

---

### Phase 3: Shared UI Primitives (`web/src/components/common/`)
Update shared components to wrap standard MUI components:
- `Button.tsx`: MUI `Button` + `CircularProgress`.
- `Modal.tsx`: MUI `Dialog` strictly for simple confirmations and alerts.
- `Input.tsx`: MUI `TextField` (`size="small"`).
- `Badge.tsx`: MUI `Chip`.
- `AlertBanner.tsx`: MUI `Alert` & `AlertTitle`.
- `GlobalErrorToaster.tsx`: MUI `Snackbar` & `Alert`.

---

### Phase 4: Dedicated Configuration Screens & Card Conversions
1. **Workflow Management**:
   - Create dedicated `WorkflowEditorPage.tsx` at `/workflows/new` and `/workflows/:id/edit`.
   - Create dedicated `WorkflowImportPage.tsx` at `/workflows/import`.
   - Embed workflow execution history directly in `WorkflowDetailPage` as a full tab.
   - Update `WorkflowsPage.tsx` "New Workflow" button to navigate to `/workflows/new`.
2. **Built-in Cards & Surfaces**:
   - Re-implement `WorkflowCard` using MUI `Card`, `CardContent`, and `CardActions`.
   - Re-implement `PreviewCard` (Thread Preview) using standard MUI `Card`, `CardHeader`, `Avatar`, `CardContent`, and `CardActions`.
3. **Settings**:
   - Modern MUI `Tabs` interface with connection cards and account controls.
   - Convert feed addition to inline collapsible card form inside `FeedsTab`.
4. **Drafts Library**:
   - Direct "New Draft" navigation to `/composer` (bypassing popup modal).
   - Filter toolbar with MUI `Chip` filters and responsive draft list cards.
5. **Sources Library & Logs**:
   - Upload zone, media gallery, and monospace log viewer.

---

### Phase 5: Complex Interactive Views
- **Calendar & Outbox** (`CalendarPage.tsx`, `ReviewOutboxPage.tsx`):
   - Dedicated rule management view (`/calendar/rules`), responsive queue sidebar, outbox review actions.
   - Draft-to-slot assignment drawer.
- **Reply Agents** (`ReplyAgentsPage.tsx`):
   - Dedicated post monitor configuration inline panel.
- **Composer** (`ComposerPage.tsx`, `ChatPanel`, `EditorPanel`):
   - Dual-pane responsive layout that adapts to sidebar expanded/collapsed modes.
   - Mobile Tab Switcher (`[Chat] | [Draft & Preview]`).
   - Inline Drawer for variant adaptation.

---

## Verification Plan

### Automated Tests
```bash
# 1. Typecheck and Production Bundle Build
docker compose --profile tooling run --rm web-test "npm run build && npm run typecheck"

# 2. Run Entire Vitest Suite (525+ tests)
docker compose --profile tooling run --rm web-test

# 3. Targeted Navigation, Card & Preview Tests
docker compose --profile tooling run --rm web-test "npx vitest run src/components/EditorPanel/ThreadPreview/ThreadPreview.test.tsx"
docker compose --profile tooling run --rm web-test "npx vitest run src/components/Navigation/AppHeader.test.tsx"
docker compose --profile tooling run --rm web-test "npx vitest run src/components/WorkflowsPanel/WorkflowCard.test.tsx"
```

### Manual Verification
- In browser at `http://localhost:8100`:
  1. **Thread Preview Cards**: In Composer, check the thread preview. Verify it uses standard MUI `Card`, `CardHeader` with avatar, `CardContent`, and `CardActions`.
  2. **Modal Policy**: Confirm that "New Workflow" and "Edit Workflow" navigate to their own dedicated page (`/workflows/new`, `/workflows/:id/edit`) rather than opening a popup modal. Only simple actions (e.g. "Delete Workflow?") open a confirm dialog.
  3. **Mobile Responsiveness**: Test on mobile screen size (`< 900px`), verify top bar hamburger menu opens drawer, and forms scroll smoothly without modal distortion. In Composer, verify the top tab switcher between Chat and Draft.
  4. **Sidebar Collapse**: Toggle sidebar collapse on desktop (`>= 900px`), confirm smooth fluid push transition between 240px and 68px with tooltips.
  5. **Theme Toggle**: Verify instant Light/Dark mode switching on all views from the sidebar footer.
