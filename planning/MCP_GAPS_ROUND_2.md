# MCP Server Gaps & Comprehensive Enhancements (Round 2)

**Status**: In Planning  
**Tracking**: [PLAN.md](../PLAN.md)  
**Date**: 2026-10-16  

---

## 1. Context & Problem Statement

Following the delivery of standard OAuth 2.1 authentication and RFC-compliant MCP endpoints (RFC 9728 / 8414 / 7591) for Claude integration, real-world testing and codebase audits revealed several functional and data-integrity gaps across the MCP layer (`McpToolRegistry.cs`, `PublishPlatformTool.cs`, and associated services):

1. **No Media or Image Support**: The core REST API and in-app chat support image uploads, imports from URL, template rendering, and code block rendering. MCP exposes none of these, preventing external agents from attaching media to drafts.
2. **Missing Account Selection**: `create_draft`, `validate_draft`, and `publish_thread` take a `platform` string with no `accountId`. `PublishPlatformTool` uses case-sensitive matching and non-deterministically picks `FirstOrDefaultAsync`, which fails multi-account scenarios.
3. **Manual Publishing Loses Critical Data**: `PublishPlatformTool` fails to persist `PublishedText` on `Post` records and leaves `PlatformThread.AccountId` null. This causes published text and account handles to appear null in `list_posts` and breaks reply-agent context.
4. **Misleading Comment Counts**: `commentsCount` in `list_posts` counts `WorkflowCommentAudits` (reply agent audit logs), not platform comments.
5. **Scheduling & Queue Gaps**: `enqueue_draft` hides `platformThreadId` in its schema and defaults to Bluesky. Queue management tools (remove, reorder, pause, retry) and schedule lifecycle operations are missing.
6. **Missing Lifecycle Tools**: No `delete_feed`, `delete_reply_agent`, or `get_workflow`. Workflow pausing and archiving are not exposed.
7. **Validation Discrepancies**: `validate_draft` defaults to Bluesky. `PublishPlatformTool.ValidateThreadContent` only validates Bluesky and skips Twitter, Threads, and LinkedIn.
8. **Documentation Drift & Safety**: Tool registry has 46 tools while documentation states 40. OAuth has a single coarse-grained `mcp:tools` scope. Irreversible publish actions lack `dryRun` options.

---

## 2. Phased Roadmap

### Phase 0: Data Integrity & Critical Account Selection (P0)
*Target: Immediate bug fixes with TDD verification*

1. **Fix Post Persistence & Account Association**:
   - In `PublishPlatformTool.cs`, populate `post.PublishedText = publishedPost.Text ?? thread.Content` on created `Post` records.
   - Assign `thread.AccountId = account.Id` when publishing.
   - Add unit/integration tests confirming `PublishedText` and `AccountHandle` are populated in `list_posts` after publishing.
2. **Account Selection & Deterministic Lookups**:
   - Add optional `accountId` parameter to `create_draft`, `validate_draft`, `publish_thread`, and `enqueue_draft`.
   - Update account lookups to be case-insensitive (`a.Platform.ToLower() == platform.ToLower()`).
   - When `accountId` is omitted: if the user has multiple accounts for that platform, return an error listing the available accounts with their IDs rather than picking an arbitrary one.
3. **Schema & Validation Alignment**:
   - Expose `platformThreadId` in the `enqueue_draft` MCP tool schema.
   - Make `validate_draft` default to the draft's designated platform (or first thread variant) instead of hardcoded `Bluesky`.
4. **Audit Metrics Clarification**:
   - In `list_posts`, provide `auditedCommentsCount` for reply agent audits, and prepare `commentsCount` for platform engagement data.

---

### Phase 1: Media & Expanded Capabilities (P1)
*Target: Enable rich multi-modal thread creation via MCP*

5. **MCP Media Tools**:
   - `import_media_from_url`: Import image from URL into a draft with alt text, returning `markdownTag`.
   - `upload_media`: Upload base64 encoded media to a draft with size validation.
   - `list_templates`: List available visual image templates from Image Engine.
   - `render_template`: Render Image Engine template with custom props and attach to draft.
   - `render_code_blocks`: Render code snippets into styled images and attach to draft.
   - `search_images`: Search Unsplash/web image sources via existing services.
   - `update_media` / `delete_media`: Update alt text or remove media from drafts.
   - Update `get_draft` response to include attached media assets.
6. **One-Off Scheduling (`publish_at` / `schedule_post`)**:
   - Add tool or parameter to schedule a draft for a specific UTC timestamp without requiring manual slot rules.
7. **Multi-Platform Publishing & Dry-Run**:
   - Add multi-destination publishing support with `dryRun` flag returning resolved accounts, validation results, and preview without posting.
8. **Engagement Sync Metrics**:
   - Connect platform engagement data (likes, reposts, comments) to `list_posts`.

---

### Phase 2: Lifecycle Completeness & Pipeline Validation (P2)
*Target: Full parity with REST API and in-app chat capabilities*

9. **Queue Management Tools**:
   - `remove_queue_item`, `reorder_queue`, `pause_queue_item`, `retry_queue_item`.
10. **Lifecycle CRUD Tools**:
    - `update_schedule`, `delete_schedule`, `delete_feed`, `delete_reply_agent`, `get_workflow`.
11. **Workflow Lifecycle Integration**:
    - Integrate `Status` (`Active`, `Paused`, `Archived`) into `list_workflows` and `update_workflow`.
12. **Chat & Content Tool Exposure**:
    - Expose platform variants generator (`generate_variants`), Bluesky reply target configuration, `transcribe_video`, and `web_search`.
13. **Comprehensive Publish Validation**:
    - Extend `PublishPlatformTool.ValidateThreadContent` to execute Twitter, Threads, and LinkedIn validators before publishing.

---

### Phase 3: Hygiene, Safety & Tool Count Drift (P3)
*Target: Developer ergonomics, granular security, and automated documentation*

14. **Automated Documentation & Drift Prevention**:
    - Add automated test verifying `docs/mcp_server.md` matches `McpToolRegistry.cs` tool names and counts.
15. **Structured Output for Write Tools**:
    - Standardize MCP write tool responses to return structured JSON payloads (`id`, `platform`, `accountId`, `status`, `url`).
16. **Granular OAuth Scopes**:
    - Split `mcp:tools` into `mcp:read` and `mcp:write` for least-privilege integrations.
17. **Confirm / Dry-Run Guards**:
    - Introduce explicit `confirm` or `dryRun` parameters on irreversible tools (`publish_thread`, `reply_to_comment`).

---

## 3. Verification & Acceptance Criteria

- All bug fixes follow test-driven development (failing test reproducing issue -> fix -> passing test).
- Tests run inside Docker: `docker compose --profile tooling run --rm dotnet test ...`.
- Documentation (`MENTAL_MODEL.md` and `docs/mcp_server.md`) kept in sync with every change.
