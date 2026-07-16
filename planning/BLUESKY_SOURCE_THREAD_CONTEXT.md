# Bluesky Source Thread Context Plan

Status: Proposed
Owner: Sources and Chat Context
Theme: Source ingestion and knowledge reuse

## Problem

When a source is a Bluesky post URL from outside our own authored posts, the agent currently gets only the single scraped post snippet. This drops critical conversational context from earlier posts in that thread.

Goal: for external Bluesky sources, resolve and store prior thread context so the agent can reason over the full conversation.

## Scope

In scope:

- Source ingestion path for URL sources pointing to bsky.app post URLs.
- Resolve previous posts in thread and include them in stored source content.
- Preserve existing behavior for non-Bluesky URLs and YouTube sources.
- Avoid applying this to our own authored posts.

Out of scope:

- UI redesign for threaded source visualization.
- Replacing existing reply-target metadata flow.
- Re-ingesting all historical sources in this phase.

## Product Rules

- Trigger only when source URL is a strict Bluesky post URL.
- Only for sources that do not originate from our own posts.
- Store both:
  - original post URL content summary
  - prior thread context in normalized plain text
- Keep source Kind semantics stable unless we intentionally add a dedicated kind.

## Proposed Design

### 1. Detection

At AddUrlSourceAsync ingestion:

- Detect strict Bluesky post URL format.
- Determine whether it is one of our own posts:
  - compare post author handle/DID to connected Bluesky account handle/DID for current user.
- If own post: skip thread expansion and keep current path.
- If external post: resolve full parent chain.

### 2. Thread Resolution Service

Create a dedicated service for source context resolution, separate from reply-target resolver concerns.

Candidate:

- IBlueskyThreadContextResolver
- BlueskyThreadContextResolver

Output model:

- canonical post URL
- author handle
- focal post text
- parent chain entries (ordered oldest to newest)
- flattened text block for source content

Formatting guidance:

- each entry includes handle, canonical URL, and text
- deterministic ordering oldest to newest
- explicit separator markers between posts
- length cap with truncation marker

### 3. Persistence Strategy

For external Bluesky source URLs:

- Source.Reference remains canonical focal post URL.
- Source.Title stays concise (focal post author and first line).
- Source.Content stores:
  - focal post text
  - parent thread context block
- optional Source.Summary can be generated from combined context if summarizer is enabled.

No schema changes required in phase 1 if we can serialize combined context into Content.

Phase 2 optional schema extension:

- add structured JSON context field if we need richer UI display later.

### 4. Agent Consumption

No new tool contract needed if content is persisted in Source.Content.

Existing tools already support this path:

- list_sources finds the source
- fetch_source returns Source.Content

Result: agent naturally sees full thread context when it fetches source text.

## Implementation Plan

### Phase 1: Resolver and ingestion wiring

- Add Bluesky thread context resolver service.
- Integrate resolver into SourcesService.AddUrlSourceAsync for external Bluesky URLs.
- Keep fallback behavior: if resolver fails, continue with current scrape result.

### Phase 2: Ownership exclusion

- Add ownership check against user Bluesky account.
- Skip expansion for own posts.

### Phase 3: Tests

Add SourcesService tests for:

- external Bluesky post URL includes parent chain in Source.Content
- own Bluesky post URL does not expand thread
- non-Bluesky URL unaffected
- resolver failure falls back without failing ingestion

### Phase 4: Verification

- docker compose tooling API build
- targeted SourcesService tests
- manual check by adding external Bluesky source and fetching it in chat

## Risks and Mitigations

Risk: API throttling or transient failures from public Bluesky endpoints.
Mitigation: best-effort resolution with graceful fallback to current behavior.

Risk: oversized prompt/context from very long threads.
Mitigation: strict truncation cap and deterministic formatting.

Risk: false ownership classification.
Mitigation: compare normalized handle and DID when available; default to safe behavior and log decision path.

## Open Questions

- Should we include only parents, or parents plus sibling replies visible in thread payload?
- Should we mark injected context with explicit provenance labels for downstream UI?
- Should own-post exclusion be strict by DID only, or handle plus DID fallback?

## Acceptance Criteria

- Adding an external Bluesky post URL source stores prior thread context in Source.Content.
- Adding a Bluesky URL for our own post does not include extra parent-chain context.
- Non-Bluesky source ingestion behavior remains unchanged.
- Agent can fetch the source and read full context via existing tools without prompt contract changes.
