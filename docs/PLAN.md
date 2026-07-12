# PLAN.md

## Completed

- [x] Chat / SSE streaming
- [x] Editor (CodeMirror 6 + vim + markdown)
- [x] Draft CRUD
- [x] Thread stages (RoughDraft → Sourcing → Refining → Formatting → Ready → Sent)
- [x] Sources (URL fetch + cache, file upload, notes)
- [x] Media upload (images, SkiaSharp resize)
- [x] Platform variants (LLM-driven per-platform adaptation)
- [x] Platform publishing (Bluesky end-to-end; Twitter/LinkedIn/Facebook/Instagram stubbed)
- [x] Auth (single-user, JWT, refresh tokens, encrypted platform tokens)
- [x] Brand voice prompts
- [x] API refactoring (SourcesService, MediaService, DraftsService, ProvidersService)
- [x] UI library migration: all inline SVGs → Font Awesome icons

## Planned

- [ ] **Python execution sandbox** — see [PYTHON_SANDBOX.md](PYTHON_SANDBOX.md)
- [ ] **Self-evolving assistant** — see [SELF_EVOLVING_ASSISTANT.md](SELF_EVOLVING_ASSISTANT.md)
- [ ] **Telegram bridge** — see [TELEGRAM_INTEGRATION.md](TELEGRAM_INTEGRATION.md)
- [ ] **E2E tests** — see [E2E_TESTING.md](E2E_TESTING.md)
- [ ] Chat history persistence
- [ ] Multi-draft session management
- [ ] Thread reordering (drag-and-drop segments)
- [ ] Scheduled publishing
- [ ] Multi-user / team support