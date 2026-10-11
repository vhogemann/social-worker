# MENTAL_MODEL.md — web (Frontend SPA)

## Purpose

This document provides the frontend architecture, component layout, state management model, and verification commands for the `web/` single-page application.

## Tech Stack

- **Runtime & Bundler**: Vite 8 with `@vitejs/plugin-react`.
- **Framework**: React 19 (`react`, `react-dom`, `react-router-dom` v6).
- **Language**: TypeScript 5.6 (strict mode enabled).
- **Styling**: Tailwind CSS 3 with `@tailwindcss/typography`, PostCSS, and Autoprefixer.
- **Icons**: FontAwesome SVG Core (`@fortawesome/react-fontawesome` v7/v3).
- **Rich Text & Code Editing**: CodeMirror 6 (`@uiw/react-codemirror`, `@codemirror/lang-markdown`).
- **Canvas & Graphics**: Konva (`konva` v9) for Template Studio graphical manipulation.
- **AI Chat Interface**: `@assistant-ui/react` and `@assistant-ui/react-data-stream` (native SSE streaming, no Vercel AI SDK).
- **State Management**: Zustand 4 for global stores; scoped React Context for page-level state.
- **Testing**: Vitest 4 with `@testing-library/react`, `@testing-library/jest-dom`, and `jsdom`.

## Application Topology & Navigation

```
web/
├── Dockerfile                         # Production Nginx reverse-proxy image
├── nginx.conf                         # Static asset hosting & API proxy to http://api:8080
├── package.json                       # Scripts, dependencies, and test tooling
├── src/
│   ├── App.tsx                        # Application router and shell wrapper
│   ├── main.tsx                       # React DOM root entrypoint
│   ├── index.css                      # Global Tailwind directives & dark theme variables
│   ├── api/                           # Hand-synced API clients (drafts, workflows, schedules, etc.)
│   ├── components/                    # View components and feature modules
│   │   ├── Calendar/                  # Content calendar (Month/Week/Day, SlotRules, QueueSidebar)
│   │   ├── Composer/                  # Full-width 2-pane thread drafting workspace
│   │   ├── Drafts/                    # Drafts library, search, and status filters
│   │   ├── EditorPanel/               # Markdown editor, platform preview, and sources drawer
│   │   ├── Navigation/                # AppHeader, breadcrumbs, and route links
│   │   ├── Outbox/                    # Scheduled post review outbox triage
│   │   ├── Settings/                  # Platform connections (Bluesky, LinkedIn, Threads, Twitter, Instagram)
│   │   ├── TemplateStudio/            # Visual WYSIWYG canvas editor
│   │   ├── WorkflowDetail/            # Execution history, triggers, and configuration
│   │   ├── WorkflowsPanel/            # Feed automation workflow monitor
│   │   └── common/                    # Shared UI primitives (Badge, Input, Modal, ErrorBoundary)
│   └── store/                         # Zustand state stores (draftStore, editorStore, authStore)
```

## Route Map (`App.tsx`)

| Path | Component | Purpose |
| :--- | :--- | :--- |
| `/` | `ComposerPage` | Primary 2-pane thread composition environment |
| `/drafts` | `DraftsPage` | Drafts library with status/stage filters |
| `/calendar` | `CalendarPage` | Visual schedule slots, queue sidebar, slot rule editor |
| `/outbox` | `ReviewOutboxPage` | Human approval triage for scheduled drafts |
| `/workflows` | `WorkflowsPanel` | Automated feed ingestion workflows list |
| `/workflows/:id` | `WorkflowDetail` | Detailed workflow metrics, trigger runs, logs |
| `/templates` | `TemplateStudio` | Canvas-based template designer and previewer |
| `/settings` | `SettingsPage` | User preferences, platform connections, brand voices, reply agents library, and feed subscriptions |
| `/oauth/callback/*` | Callback pages | OAuth redirect handlers (Twitter, Threads, Instagram, LinkedIn) |

## State Management Architecture

### 1. Scoped Composer State (`ComposerDraftContext`)

The Composer does not rely on a monolithic global store for active editing. Instead, `ComposerDraftContext` encapsulates:
- Current draft document and thread segments (`---` delimiters).
- Scoped source attachments and status polling.
- Auto-save debouncing with draft version conflict detection.
- Assistant chat runtime synchronization (`chatHistory` persisted to the draft record).

### 2. Global Stores (`src/store/`)

- **`draftStore.ts`**: Cached draft metadata list for quick navigation and badge counters.
- **`editorStore.ts`**: Transient editor layout preferences (e.g. active platform preview tab, drawer toggle).
- **`authStore.ts`**: Single-user local session and API authentication state.
- **`templateEditorStore.ts`**: Active canvas selection, layer hierarchy, and property inspection for Template Studio.

### 3. API Communication (`src/api/`)

API contracts are hand-synced with the .NET backend:
- `accounts.ts`: Platform OAuth initiation, connected account listings, credential validation.
- `drafts.ts`: CRUD operations, AST updates, validation checks, publishing triggers.
- `chat.tsx`: Native `EventSource` connection to the backend SSE streaming endpoint.
- `schedules.ts` & `outbox.ts`: Content calendar queries, slot rules, and queue dispatch.
- `workflows.ts`: Automated feed workflow triggers and log inspection.

## Styling & Styleguide

- Adheres to [`STYLEGUIDE.md`](file:///home/vhogemann/Projects/social-worker/web/STYLEGUIDE.md) conventions.
- Tailwind CSS dark-first color scheme: slate/neutral backgrounds (`bg-neutral-900`, `bg-neutral-800`), crisp borders (`border-neutral-700`), and subtle accent highlights.
- Accessible test anchors (`data-testid`, durable ARIA attributes) on interactive elements.

## Verification & Testing

All web build and test commands run inside Docker via the `tooling` profile:

```bash
# Run Vitest test suite (474 tests in 91 files)
docker compose --profile tooling run --rm web-test

# Run TypeScript type check and production build
docker compose --profile tooling run --rm web-test "npm run build && npm run typecheck"

# Run a specific Vitest test file
docker compose --profile tooling run --rm web-test "npx vitest run src/components/Settings/ConnectionsTab.test.tsx"
```
