# UI Component Discovery

Approach: assemble the social-worker GUI from ready-to-use open-source libraries where possible. Below is the component breakdown derived from the envisioned UX. Each section captures the functional requirements and integration points to guide library hunting.

## Vision

A multi-modal thread composer. The user brain-dumps into a chat, iterates with an LLM, then refines content in a markdown editor that serves as the canonical draft. Per-platform preview panes enforce each target platform's constraints (post length, image limits, media types). The user moves freely between chat and editor; the agent can also propose or apply edits to the editor when asked.

The whole app should feel like vim: modal, keyboard-driven, with shortcuts to change modes and switch focus between views. Mouse is a fallback, not the primary input.

## Components

### 1. Chat window

The brain-dump surface. Users paste links, references, notes, or just free-text while the LLM helps shape the thread.

- Streaming responses (SSE or WebSocket from the backend)
- Markdown rendering in assistant messages (code blocks, links, lists)
- Attach/paste: links, images, files
- Visible tool/function calls when the agent acts (e.g. "fetching source", "setting stage", "adapting to platform")
- Conversation history within a draft session
- Composer input with multi-line support, paste handling
- No Vercel AI SDK — native EventSource per AGENTS.md

### 2. Content editor (markdown source)

The canonical drafting surface. Markdown is the user-facing format; structured metadata lives behind it but is exposed/edited via markdown conventions.

- Markdown as source of truth
- `---` as the thread-segment separator (one segment = one post)
- Frontmatter or inline directives for structured fields (per-platform overrides, media references, alt text, etc.)
- Syntax highlighting for the markdown source (must-have)
- Editable directly by the user
- Programmatic edits: the agent can insert/replace text when asked
- Media attachments: insert images, manage per-segment media
- Undo/redo, find/replace expected baseline
- Vim-style modal editing: normal / insert / visual modes, with a visible mode indicator and shortcuts to switch (Esc, i, v, etc.)
- Ex-style command bar (`:`) for actions like `:w` (save), `:setstage formatting`, `:publish bluesky`, `:goto chat`
- Operator-pending motions (d, y, c) and counts work as expected

### 3. Rich-text preview pane

Live rendered preview alongside the markdown source. Split-pane layout (source | preview).

- Renders the markdown into platform-agnostic rich text
- Updates in real time as the source changes
- Shows segment breaks visually (where `---` splits posts)
- Renders media attachments inline
- Read-only

### 4. Per-platform preview panes

One preview per target platform, reflecting how the thread will appear when published on that platform.

- Applies platform constraints: max post length, image count limits, allowed media types, link/card behavior
- Shows the thread as a sequence of posts (one per segment) styled to resemble the platform
- Surfaces constraint violations (e.g. "post 2 exceeds 300 graphemes for Bluesky")
- Reflects per-platform overrides if the editor defines them
- Read-only; clicking a violation could navigate to the offending segment in the editor
- v1: Bluesky fully functional; Twitter/LinkedIn/Facebook/Instagram stubbed

### 5. View orchestration / layout

The shell that lets the user move between chat and editor, and shows platform previews.

- At least two primary views: chat | editor
- Per-platform previews either docked alongside the editor or as tabs
- State shared across views: the same draft, same thread segments
- Transitions between stages (RoughDraft → Sourcing → Refining → Formatting → Ready → Sent) surfaced in the shell
- Stage transitions require explicit user approval; the agent cannot self-advance

## Cross-cutting concerns

- **Vim feel across the app**: modal editing in the editor, but also keyboard-driven navigation between views (e.g. `<C-j>`/`<C-k>` to cycle chat | editor | previews, `<leader>cp` to focus a platform preview). A consistent keymap layer, not just a CodeMirror plugin scoped to the editor. Visible mode/state indicator. Ex-command bar available globally for actions and stage transitions.


- **Markdown data model**: a canonical markdown document with `---` segment separators and optional structured metadata (frontmatter or directives). This is what flows between editor, preview, and platform variants. Libraries should operate on a common markdown AST so source edits, preview renders, and programmatic agent edits all stay in sync.
- **Media handling**: images and files attached to segments need stable references that survive edits to the markdown. Likely an asset store keyed by ID, with markdown embedding the reference.
- **Agent edits**: the editor must expose an API for the chat/agent layer to apply structured edits (replace segment, insert segment, rewrite selection) without clobbering the user's cursor/undo history.
- **Stage state**: a single source of truth for the draft's current stage, reflected in the shell; transitions are user-approved.
- **Platform constraint definitions**: a pluggable spec per platform (max length, media limits, supported media types) consumed by the preview panes. New platform = new spec, not new UI.

## v1 gap (in PLAN.md but not covered above)

The components below are listed in `PLAN.md`'s v1 deliverable scope and must be in scope for v1. Calling them out explicitly so they aren't dropped.

### 6. Stage stepper

Top-of-shell indicator showing `RoughDraft → Sourcing → Refining → Formatting → Ready → Sent`. Each transition is proposed by the agent via `set_stage` but applied only after the user clicks "Approve". The stepper is the primary surface for stage state and the user-approval gate.

- Reads stage from the shared draft store (zustand)
- "Approve transition" button enabled only when the server signals a pending proposal
- Disabled forward when current stage can't advance (e.g. can't go `Ready` until platform variants are confirmed)
- Surfaced persistently in the shell so it's visible from both chat and editor views

### 7. Drafts list (sidebar)

Switch between drafts, create new, archive/delete.

- List of drafts with title, stage badge, last-updated
- "New draft" action
- Active draft highlighted; selecting loads its chat history + editor content + sources + variants
- Search/filter by title or stage (nice-to-have for v1)

### 8. Source list / manager

Browse sources attached to the active draft. The chat adds sources via tool calls; this surface lets the user inspect, open, and remove them.

- Per-source row: kind icon (URL / file / note), title or ref, "open cached readable text" expand
- Remove source action
- Filter by kind
- Read-only display of extracted text (no editing in v1)

### 9. Media dropzone / uploader

Drag-drop and paste handling for images and files across chat and editor.

- Drop anywhere on the chat or editor to upload
- Paste from clipboard (screenshots, copied images)
- Per-segment media attachment in the editor
- Progress + error states
- Reuses the `MediaAsset` backend; assets keyed by ID and embedded in the markdown by reference

### 10. Brand voice prompts manager

CRUD for `BrandVoicePrompt` entities (name, body, default flag).

- List + create + edit + delete
- "Set as default" toggle (one default at a time)
- Modal or side panel — not a full view, accessed from settings

### 11. Platform account connection UI

Connect/disconnect platform accounts. v1 ships Bluesky (identifier + app password); others show a "coming soon" state with a placeholder auth URL.

- Form per platform: fields per platform (Bluesky: identifier + app password; others: disabled with auth URL placeholder)
- Connection status badge (connected / disconnected / error)
- Delete account / revoke credentials
- Credentials never displayed back after save (write-only)

### 12. Publish confirmation modal

Explicit confirm flow before `publish` is dispatched. AGENTS.md: "publish is rejected unless Stage=Ready and the target platform's variant is confirmed."

- Triggered by `:publish <platform>` ex-command or a button
- Shows the final per-platform variant that will be sent
- Lists segment count, media per segment, and any remaining constraint violations
- Confirm / cancel — confirm calls the `publish` tool; cancel aborts
- Loading state while publishing, then success/failure per segment

## Cross-cutting gaps

- **Notifications / toasts** — feedback for save, errors, publish results, source fetch failures. Persistent enough to read, dismissible.
- **Forms** — for account credentials, brand prompts, settings. Needs validation.
- **Autosave / persistence** — editor content persists to the API on change (debounced). Stage state and segment edits flow through the same store. Don't lose work on reload.
- **Error boundaries / loading / empty states** — baseline resilience for every view.

## Vim ex-commands (enumerated)

The `:` command bar is available globally. v1 commands:

| Command | Action |
|---|---|
| `:w` | Save draft (force-sync editor content to API) |
| `:setstage <stage>` | Propose stage transition (still needs UI approval) |
| `:publish <platform>` | Open publish confirmation for the given platform |
| `:goto <view>` | Focus chat \| editor \| previews |
| `:sources` | Open the source list panel for the active draft |
| `:accounts` | Open the platform account connection UI |
| `:help` | Open the keymap cheatsheet |

## Data model reconciliation (markdown vs ThreadSegment)

`PLAN.md` models a draft as an ordered list of `ThreadSegment` rows in Postgres. The discovery pivoted the editor to a single markdown document with `---` segment splits. These must agree:

- The `ThreadSegment` table remains the source of truth in the DB (one row = one post).
- The markdown editor is a *view* over `ThreadSegment[]`: render segments joined by `\n\n---\n\n`, parse back into segments on save.
- Frontmatter holds draft-level metadata (title, default platform set, brand voice ref).
- Per-segment structured fields (media refs, alt text, per-platform overrides) live as inline directives or a parallel JSON payload keyed by segment position — not in the markdown body, to keep the user-facing text clean.
- Backend tools (`create_segment`, `update_segment`, `reorder_segments`) operate on rows; the editor reconciles by re-deriving the markdown view from the rows after any tool call.

Open question to resolve before build: do per-platform overrides live in the markdown (as directives) or in a separate `PlatformVariant` payload? PLAN.md has `PlatformVariant` with `SegmentsJson` — likely the right home, with the markdown editor only showing the canonical thread.

- Auth/multi-user (v1 is single-user, no auth)
- Scheduling/calendar
- Analytics/inbox
- Image generation
- Twitter/LinkedIn/Facebook/Instagram publishing (stubbed; only Bluesky ships)
