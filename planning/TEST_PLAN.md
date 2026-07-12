# Test Coverage Plan

Tracking progress on filling test gaps across the codebase.

---

## Priority tiers

| Tier | Criteria | Target |
|---|---|---|
| P0 | High risk, zero tests | First pass |
| P1 | Medium risk, partial coverage | Second pass |
| P2 | Chat tools without tests | Opportunistic |

---

## P0 — High Risk, Zero Tests

| File | Lines | Approach | Status |
|---|---|---|---|
| `BlueskyPublisher.cs` | 201 | Mock HTTP handler, test auth flow, image upload, reply threading | [ ] |
| `ChatService.cs` | 254 | Test SSE streaming pipeline, tool execution, multi-round, error handling | [ ] |
| `AuthService.cs` | 131 | Test login, refresh, logout, token expiry, password change | [ ] |
| `DraftsService.cs` (UpdateDraftAsync) | ~90 | Test content update, segment reconciliation, source sync, status transitions, delete | [ ] |
| `MediaService.cs` | ~100 | Test upload, resize, dedup (SHA-256), shared asset protection | [ ] |
| `PublishingEndpoint.cs` | ~50 | Test publish flow (happy path, already-sent, no account, no publisher) | [ ] |

---

## P1 — Medium Risk, Partial or No Tests

| File | Lines | Notes | Status |
|---|---|---|---|
| `CodeImageService.cs` | ~60 | Only Renderer + Parser tested; service layer untested | [ ] |
| `WebScraperService.cs` | ~220 | URL scraping, YouTube metadata, readability extraction | [ ] |
| `SourceExtractor.cs` | ~40 | PDF text extraction, txt/md parsing | [ ] |
| `ChatSessionLoader.cs` | ~110 | Session loading, draft creation, brand voice resolution | [ ] |
| `DraftTitleGenerator.cs` | ~50 | LLM-based title generation from chat | [ ] |
| `ChatStreamWriter.cs` | ~100 | SSE message formatting | [ ] |
| `ModelCapabilityProbe.cs` | ~180 | Capability probing for OpenAI, OpenRouter, Ollama | [ ] |
| `CryptoHelper.cs` | ~50 | AES encryption/decryption round-trip | [ ] |

---

## P2 — Chat Tools Without Tests

9 of 13 tools have no tests. 4 covered: `WebSearchTool`, `ImageSearchTool`, `AddSourceTool`, `ValidateDraftTool`.

| Tool | Status |
|---|---|
| `ReplaceEditorContentTool` | [ ] |
| `ProposeStageTransitionTool` | [ ] |
| `ListSourcesTool` | [ ] |
| `FetchSourceTool` | [ ] |
| `ViewImageTool` | [ ] |
| `PublishPlatformTool` | [ ] |
| `AddImageSourceTool` | [ ] |
| `RenderCodeBlocksTool` | [ ] |
| `GeneratePlatformVariantsTool` | [ ] |

---

## Frontend (web/)

Currently only 1 test file (`SourcesPanel.test.tsx`, 2 tests). No test framework is mandated yet — this section is tracked for when Vitest + Testing Library are introduced.

- Stores (zustand): authStore, chatStore, draftStore, editorStore
- API layer: accounts, auth, brandVoices, chat, client, drafts, providers
- Components: ChatPanel, DraftList, EditorPanel, MarkdownEditor, Thread preview tree (12 files), Settings tree (6 files), Login, AuthGuard

---

## Running tests

```bash
# API tests
docker compose --profile tooling run --rm dotnet test SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj

# E2E tests
docker compose --profile e2e run --rm e2e npx playwright test
```

## Coverage tracking

Update this file as tests are added. Mark items `[x]` when the test file exists and covers the happy path + edge cases.