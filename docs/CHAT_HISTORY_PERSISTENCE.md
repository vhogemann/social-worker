# Chat History Persistence and Compaction

We implement a hybrid persistence and compaction strategy:
- The full chat history is stored in the database (`ChatHistory` column) to preserve the user's complete scrollback in the UI.
- An LLM-generated summary of older conversation context is stored in a `ChatSummary` column.
- We track `LastSummarizedMessageCount` (integer) in the database.
- A new summarization is only triggered when the difference between the current message count and `LastSummarizedMessageCount` is 20 or more.
- At request time, we inject the summary into the system prompt and only pass the last 10 messages, keeping the context window small while preserving earlier instructions.

## Implementation Details

### Backend
- **Draft Entity**: Added `ChatHistory`, `ChatSummary`, and `LastSummarizedMessageCount` to `Draft.cs`.
- **Drafts Service**: Maps the fields to `DraftDto`, updates them in `UpdateDraftAsync`, and runs `TriggerBackgroundSummarization` in the background when the 20-new-message threshold is reached.
- **Chat Service**: Slices message history sent to the LLM to the last 10 messages and appends the `ChatSummary` to the system prompt context.

### Frontend
- **API & Store**: Updated API mappings, `patchDraft`, and Zustand store `saveDraftChat`. Autoloads the chat history when switching or loading drafts.
- **Auto-Save**: Triggers auto-save of chat history to the database whenever `isRunning` transitions.
