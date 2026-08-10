# Phase 1 TODO - Scope And Capabilities

## Goal
Lock LinkedIn MVP scope and platform capability boundaries before implementation.

## Status
- State: Active
- Owner: Antigravity
- Last updated: 2026-08-10

## Checklist
- [x] Confirm MVP media categories: None, Article, Image.
- [x] Confirm non-MVP categories: Video, organization/page posting, threaded posts.
- [x] Confirm single-post-only rule: exactly one non-empty segment allowed.
- [x] Confirm YouTube handling: ARTICLE link-share preview behavior.
- [x] Document accepted and rejected payload examples (see below).
- [x] Record final scope decisions in planning/LINKED_IN.md.

## Payload / Input Examples

### Accepted Examples

1. **Text-Only Post (`NONE`)**
   ```markdown
   Hello LinkedIn! This is a simple, single-segment post without media.
   ```

2. **Article / URL Post (`ARTICLE`)**
   ```markdown
   Check out our latest update: https://example.com/announcement
   ```

3. **Image Post (`IMAGE`)**
   ```markdown
   Here is a preview of the new dashboard!
   ![Dashboard mockup](media://d3b07384-d113-4a11-a831-9f9391ab1c1b)
   ```

4. **YouTube link Post (treated as `ARTICLE` link-share, not native video)**
   ```markdown
   We just released a new video explaining our architecture: https://www.youtube.com/watch?v=dQw4w9WgXcQ
   ```

### Rejected / Invalid Examples

1. **Multi-segment Thread**
   ```markdown
   First segment of the post.
   ---
   Second segment of the post.
   ```
   *Failure reason:* LinkedIn MVP does not support multi-post threads. Exactly one non-empty segment is allowed.

2. **Mixed Media Types (Article URL + Image)**
   ```markdown
   Check out this link: https://example.com
   And here is an image:
   ![Mockup](media://d3b07384-d113-4a11-a831-9f9391ab1c1b)
   ```
   *Failure reason:* Mixed media or more than 1 media attachment is not supported in the MVP.

## Exit Criteria
- [x] Scope is explicitly documented and approved.
- [x] No ambiguous media behavior remains for MVP.
