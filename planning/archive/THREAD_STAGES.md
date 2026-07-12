# Thread Segments & Stages

Thread segment reconciliation, workflow stage stepper progression, draft lifecycle management (auto-naming, renaming, archiving, and deletion), tech debt resolution, Vitest testing configuration, decoupled platform stages, Thread Preview mode with clipboard utilities, and markdown-first sourcing auto-reconciliation.

## Design decisions

- **Segment Splitting**: The draft content markdown string in `Draft.Content` is split on lines containing exactly `---` to produce ordered segments.
- **Reconciliation**: Whenever content is updated (via API update or tool calls), `ThreadSegments` table rows are synchronized (inserts, updates, deletes) to match the parsed segments.
- **Markdown-First Sourcing**: Sources are parsed directly from markdown contents of `Draft.Content`. HTTP/HTTPS links (naked or markdown links) and custom `file://{sourceId}` references are scanned.
  - Adding a link in the text triggers background fetching and HTML cleaning (using `HtmlAgilityPack`).
  - Dropping/selecting a file uploads it, extracts text (Txt, Md, PDF via `UglyToad.PdfPig`), pre-creates a `Source` record, and returns `[File: name.pdf](file://sourceId)` to insert in the editor.
  - Removing a link/file tag deletes the corresponding cached `Source` row automatically.
  - While background link fetches are running, the draft status locks to `Sourcing`.
- **Thread Preview Mode**: An interactive "Edit" / "Preview" toggle inside the editor workspace. Preview mode splits the markdown text in real-time and renders it as simulated social media posts connected vertically by a thread line.
- **Segment Copying**: Each segment card in Preview Mode contains a copy-to-clipboard button. When clicked, it copies the raw text for that post to the clipboard, showing a temporary "Copied!" checkmark to ease manual copy-pasting.
- **Platform Threads (Sinks)**: A single draft can target multiple platforms (e.g. Bluesky, Twitter). A new `PlatformThread` entity holds the variant formatted text, platform type, and its independent stage.
- **Decoupled Stages**: Stages (`Draft`, `Ready`, `Sent`) belong to the platform-specific `PlatformThread` variant rather than the global `Draft`. The main draft remains interactively editable (except during temporary lock states).
- **Internal Locked Status**: `Sourcing` and `Formatting` are mapped to `DraftStatus` enums. While in these statuses, editing is locked (readonly editor and disabled composer).
- **Proposals vs. Mutation**: The LLM cannot directly set the stage. It can call a tool `propose_stage_transition(platform, stage, reasoning)` which displays a visual proposal card in the chat. The user clicks "Approve" to send the API request modifying the platform stage.
- **Manual Stepper**: An interactive stepper widget displays stage status of the active platform thread and allows users to manually click stages to transition them.
- **Draft Auto-Naming**: Sending the first prompt in a draft titled "Untitled" triggers an inline LLM summarization call on the backend to rename the draft.
- **Rename & Archive/Delete UI**: Sidebar actions to edit titles inline, toggle archive status (hiding them from the default view), and soft-delete drafts.
- **Autosave**: A debounced auto-save triggers 1.5 seconds after the user stops typing in the markdown editor to persist manual edits immediately.
- **Vitest Testing**: Integrated Vitest and JSDom to run frontend component and store unit tests.

## API contract

```
PATCH  /api/drafts/{id}                       → UpdateDraftResponse   body: { title?, content?, status? }
GET    /api/drafts/{id}/threads               → PlatformThreadDto[]
POST   /api/drafts/{id}/threads               → PlatformThreadDto     body: { platform }
PATCH  /api/drafts/{id}/threads/{threadId}   → PlatformThreadDto     body: { stage?, content? }
POST   /api/drafts/{id}/files                 → { sourceId, markdownLink }  (multipart/form-data)
```

## Data model

The relationships are updated:
```
Draft {
  Id        Guid
  Title     string
  Status    DraftStatus (string enum)
  Content   string
  UserId    Guid
  Segments  ICollection<ThreadSegment>
  Threads   ICollection<PlatformThread>
  Sources   ICollection<Source>
}

ThreadSegment {
  Id        Guid
  DraftId   Guid
  Position  int
  Content   string
}

PlatformThread {
  Id        Guid
  DraftId   Guid
  Platform  string
  Stage     PlatformThreadStage (string enum)
  Content   string
}

Source {
  Id        Guid
  DraftId   Guid
  Kind      SourceKind (string enum: Url, File)
  Reference string
  Content   string
  Title     string
}
```
