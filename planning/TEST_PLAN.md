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
| `BlueskyPublisher.cs` | 201 | Mock HTTP handler, test auth flow, image upload, reply threading | [x] 5 tests covering: missing key, decryption failure, auth failure, single segment, multi-segment |
| `ChatService.cs` | 254 | Test SSE streaming pipeline, tool execution, multi-round, error handling | [x] 5 tests covering: message id, text deltas, tool execution, unknown tool, max rounds |
| `AuthService.cs` | 131 | Test login, refresh, logout, token expiry, password change | [x] 10 tests covering: valid login, wrong password, unknown user, inactive user, email login, valid refresh, invalid token, revoked token, valid logout, invalid logout |
| `DraftsService.cs` (UpdateDraftAsync) | ~90 | Test content update, segment reconciliation, source sync, status transitions, delete | [x] 9 tests covering: title update, content update, status change, chat history, chat summary, deleted draft rejection, wrong user rejection, segment reconciliation, media cleanup on delete |
| `MediaService.cs` | ~100 | Test upload, resize, dedup (SHA-256), shared asset protection | [x] 8 tests covering: missing draft, markdown tag format, dedup, missing asset, get file path, alt text update, delete, delete wrong user |
| `PublishingEndpoint.cs` | ~50 | Publish flow (happy path, already-sent, no account, no publisher) | [x] Covered by BlueskyPublisherTests + E2E publishing test. Core orchestration logic is thin; the HTTP-level tests (already-sent, no account, no publisher) are validated by the E2E suite. |

---

## P1 — Medium Risk, Partial or No Tests

| File | Lines | Notes | Status |
|---|---|---|---|---|
| `CodeImageService.cs` | ~60 | Only Renderer + Parser tested; service layer untested | [x] 1 test: render and store produces media asset |
| `WebScraperService.cs` | ~220 | URL scraping, YouTube metadata, readability extraction | [x] 5 tests: empty URL, https prepend, content extraction, noise removal, YouTube detection |
| `SourceExtractor.cs` | ~40 | PDF text extraction, txt/md parsing | [x] 3 tests: txt, md, unsupported extension |
| `ChatSessionLoader.cs` | ~110 | Session loading, draft creation, brand voice resolution | [x] 8 tests: inactive user, no provider, new draft, editor content, existing draft, deleted draft, content update, brand voice |
| `DraftTitleGenerator.cs` | ~50 | LLM-based title generation from chat | [x] 2 tests: no messages, empty message (no-throw) |
| `ChatStreamWriter.cs` | ~100 | SSE message formatting | [x] 8 tests: message id, text delta, tool call, empty args, tool result, step finish, isContinued, stream done |
| `ModelCapabilityProbe.cs` | ~180 | Capability probing for OpenAI, OpenRouter, Ollama | [x] 8 tests: OpenAI vision heuristics, non-vision, o1, caching, unknown provider, OpenRouter fallback, Ollama fallback |
| `CryptoHelper.cs` | ~50 | AES encryption/decryption round-trip | [x] 5 tests: round-trip, empty in/out, different output per call, wrong key throws |

---

## P2 — Chat Tools Without Tests

9 of 13 tools have no tests. 4 covered: `WebSearchTool`, `ImageSearchTool`, `AddSourceTool`, `ValidateDraftTool`.

| Tool | Status |
|---|---|---|
| `ReplaceEditorContentTool` | [ ] |
| `ProposeStageTransitionTool` | [x] |
| `ListSourcesTool` | [x] |
| `FetchSourceTool` | [x] |
| `ViewImageTool` | [ ] |
| `PublishPlatformTool` | [ ] |
| `AddImageSourceTool` | [x] |
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