# MCP Server Capabilities Enhancement & Agent Guidance

**Status**: Completed  
**Tracking**: [PLAN.md](../PLAN.md)  
**Date**: 2026-10-08  

---

## 1. Problem Statement

While `social-worker` provides a rich internal API for drafting, source scraping, transcription, workflow automation, and multi-platform publishing, its Model Context Protocol (MCP) server implementation (`McpToolRegistry`) currently exposes an incomplete and under-documented toolset to AI coding assistants and external agents:

1. **Missing Account Introspection (`list_accounts`)**:
   Agents cannot discover which social media accounts (Bluesky, Threads, Twitter, LinkedIn) are currently connected or retrieve their `accountId` identifiers required for workflow destinations or publishing.
2. **Missing Feed Discovery & Management (`discover_feed`, `add_feed`)**:
   Agents cannot discover RSS/YouTube feeds from raw URLs/handles or subscribe to feeds through the MCP interface, despite the backend having robust discovery services (`FeedDiscoveryService`, `YouTubeFeedDiscoveryService`).
3. **Missing Workflow Creation (`create_workflow`)**:
   Agents can only list (`list_workflows`) and trigger (`trigger_workflow`) existing workflows, but cannot construct or update automated publishing pipelines programmatically.
4. **Lack of Agent Instructions (`instructions.md`)**:
   The MCP server provides no system-level guidance or operational best practices in `instructions.md`. Agents lack context on platform formatting rules (e.g., Bluesky YouTube embed syntax `![Title](url)` vs Twitter/Threads raw URLs, character limits per platform, and stage transitions).
5. **Shallow Tool Schemas**:
   Existing tool schemas offer single-line descriptions without examples, foreign key relationships, or parameter dependencies.

---

## 2. Goals & Deliverables

### Milestone 1: New Core MCP Tools (`McpToolRegistry.cs`)

1. **`list_accounts`**:
   - **Description**: Lists all active connected social accounts (Bluesky, Threads, Twitter, LinkedIn) for the authenticated user.
   - **Fields**: `id`, `platform`, `handle`, `status`, `createdAt` (strictly excludes encrypted tokens/credentials).
   - **Filter**: Optional `platform` filter string.

2. **`discover_feed`**:
   - **Description**: Discovers the RSS/Atom feed URL, channel title, and website URL from any website or YouTube channel/playlist URL.
   - **Dependencies**: Injects `FeedDiscoveryService` into `McpToolRegistry`.
   - **Parameters**: `url` (string, required).
   - **Returns**: `feedUrl`, `title`, `websiteUrl`, `isYouTube`.

3. **`add_feed`**:
   - **Description**: Subscribes the user to a new RSS or YouTube feed.
   - **Parameters**: `title` (required), `feedUrl` (required), `websiteUrl` (optional).
   - **Returns**: Created feed details with `id`.

4. **`create_workflow`**:
   - **Description**: Creates a new automated publishing workflow connecting feed sources, instruction prompts, and account destinations.
   - **Parameters**:
     - `title` (string, required)
     - `instructionPrompt` (string, required)
     - `feedSubscriptionIds` (array of UUIDs, optional)
     - `destinations` (array of objects with `platform`, `accountId`, `publishingMode` [`Manual` | `Automatic` | `Scheduled`], `autoPublish` boolean)
     - `scheduleId` (UUID, optional)
   - **Returns**: Created workflow details with `id`.

### Milestone 2: Schema & Parameter Enrichment

- Update all 20+ MCP tool schemas in `McpToolRegistry.cs` to include:
  - Clear explanations of required formats.
  - Foreign key sources (e.g. "Use `accountId` obtained from `list_accounts`").
  - Platform-specific validation expectations.

### Milestone 3: MCP Agent Instructions (`instructions.md`)

Create `/home/vhogemann/.gemini/antigravity-cli/mcp/socialworker/instructions.md` documenting:
- **Core Workflow Architecture**: Sources ➔ Feeds ➔ Automation Workflows ➔ Drafts / Threads ➔ Validation ➔ Publishing / Scheduling.
- **Platform Formatting & Limits**:
  - **Bluesky**: 300 graphemes per post; YouTube embeds MUST use `![Title](url)`; no mixing images and YouTube embeds.
  - **Meta Threads**: 500 characters per post; up to 10 carousel images; raw URLs.
  - **Twitter / X**: 280 weighted characters; URLs budgeted at 23 chars (`t.co`); text-only in v1; warn on markdown links.
  - **LinkedIn**: 3,000 characters; single post; markdown bold/italic converted to Unicode math characters.
- **Workflow Pipeline Lifecycle**:
  - How to create a feed ➔ link to a workflow ➔ trigger ingestion ➔ monitor generated drafts.

### Milestone 4: Unit Testing & Verification

- Add unit test coverage in `SocialWorker.Api.Tests/Features/Mcp/` covering:
  - `HandleListAccountsAsync`
  - `HandleDiscoverFeedAsync`
  - `HandleAddFeedAsync`
  - `HandleCreateWorkflowAsync`
- Verify JSON schema registration and end-to-end tool execution via the live MCP server.

---

## 3. Implementation Order

1. **Update `McpToolRegistry`**:
   - Inject `FeedDiscoveryService` into constructor.
   - Register the 4 new tool definitions and schemas in `ListTools()`.
   - Implement execution handlers in `ExecuteToolAsync`.
2. **Update Unit Tests**:
   - Update `McpToolRegistryTests.cs` (or create new test suite) to assert all tools execute cleanly.
3. **Generate & Update Schema Files**:
   - Export/update JSON schemas in `.gemini/antigravity-cli/mcp/socialworker/`.
   - Write comprehensive `instructions.md`.
4. **Rebuild & Verify**:
   - Rebuild API container and test tools directly via MCP calls.
