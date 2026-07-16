# Threaded Replies Plan

Status: Proposed
Owner: Publishing and Composer UX
Theme: Bluesky publishing workflow

## Problem

Current behavior creates a brand-new draft for quick reply, but it appears as a top-level draft in the list. This makes ongoing reply chains look disconnected even though they belong to one conversation.

Desired behavior:

- Reply drafts remain distinct drafts in storage.
- UI shows reply drafts nested under the draft they were created from.
- Quick reply should preserve conversation context and avoid accidental self-reply loops.

## Investigation Summary

What exists today:

- Drafts already support one parent-child relationship field for platform variants via CanonicalDraftId.
- Platform variant logic depends on CanonicalDraftId semantics and should not be overloaded for reply-thread relationships.
- Quick reply currently calls a create-reply endpoint and switches to the new draft.
- Reply target metadata is stored separately in DraftBlueskyMetadata and already includes canonical parent URL and author/text/avatar preview fields.
- Backend now blocks setting a reply target on a sent draft.

Conclusion:

- Reusing CanonicalDraftId for replies would couple unrelated concepts and break variant assumptions.
- Best path is a dedicated reply-link relationship between drafts, then render a nested tree in DraftList.

## Goals

- Keep reply chains visually grouped in the DraftList.
- Preserve strict separation of drafts and chat history.
- Keep platform variants behavior unchanged.
- Minimize migration and API churn.

## Non-Goals

- Multi-level threaded message rendering inside a single editor document.
- Cross-platform unified conversation graph.
- Changing how Bluesky reply metadata is validated or resolved.

## Proposal

### 1. Data Model

Add dedicated reply linkage fields to Draft:

- ParentDraftId: nullable Guid
- ParentDraft: Draft? navigation
- ReplyChildren: collection navigation
- DraftLinkType: enum-like string with values:
  - None (default)
  - PlatformVariant
  - ThreadReply

Why this shape:

- One parent pointer is enough for current UX.
- Link type prevents ambiguity when future features add more link categories.
- Existing CanonicalDraftId can be migrated into this model or left as-is in phase 1 for compatibility.

Recommended phase 1 compatibility approach:

- Keep CanonicalDraftId for existing variant feature untouched.
- Introduce ParentDraftId + LinkType only for replies.
- Consider canonical-to-link-type consolidation only in a later refactor.

Database changes:

- New columns on Drafts table:
  - ParentDraftId uuid null
  - LinkType varchar(32) not null default 'None'
- FK ParentDraftId -> Drafts.Id (ON DELETE SET NULL)
- Indexes:
  - IX_Drafts_ParentDraftId
  - IX_Drafts_UserId_LinkType_ParentDraftId

### 2. API Contract

Extend DraftDto with linkage metadata:

- parentDraftId: string | null
- linkType: string
- replyChildCount: number (optional convenience field)

Reply creation endpoint behavior:

- POST /api/drafts/reply-from-url should accept optional parentDraftId.
- If parentDraftId not supplied, default to active draft id from caller context where available.
- Created reply draft should have:
  - ParentDraftId = selected parent draft
  - LinkType = ThreadReply
  - TargetPlatform = Bluesky
  - Bluesky reply metadata set from resolved URL

Validation rules:

- parentDraftId must belong to same user.
- parent draft cannot be Deleted.
- Keep existing guard: cannot mutate reply target on Sent draft.
- New rule: quick reply from Sent draft is allowed only via new child draft creation, never via mutation.

### 3. Quick Reply Target Resolution

On quick reply click in preview:

- Resolve source URL with this order:
  - activeDraft.blueskyReplyTarget.replyParentUrl
  - clicked post URL
- Create child reply draft linked to active draft.
- Switch UI focus to new child draft in edit mode.

Rationale:

- Preserves the original external thread context.
- Prevents accidental chain replies against our own published segment URL when original reply target exists.

### 4. Draft List UX

Render a draft tree with two child sections under each top-level draft:

- Variants (existing behavior)
- Replies (new)

Recommended visual treatment:

- Replies nested below parent with a reply arrow icon.
- Show compact badge: reply.
- Keep existing active-draft highlight behavior.
- Default ordering for replies: newest first.

Selection behavior:

- Selecting a parent draft does not auto-open child.
- Creating reply child auto-selects child.

### 5. Optional Editor UX (Phase 2)

If needed after phase 1:

- Add lightweight breadcrumb at top of editor:
  - Parent draft title -> current draft
- Add quick jump button to parent draft.

This improves orientation without forcing a single-document editing model.

## Implementation Plan

### Phase 1: Backend linkage foundation

- Add Draft fields ParentDraftId and LinkType.
- Add AppDbContext mapping and indexes.
- Add SQL migration script 0003_add_draft_linkage.sql.
- Extend DraftDto and mapping logic in DraftsService.
- Update create-reply flow to set ParentDraftId and LinkType=ThreadReply.
- Add tests for:
  - reply draft links to parent
  - user boundary validation
  - sent draft mutation guard remains enforced

### Phase 2: Frontend data plumbing

- Extend web DraftDto type with parentDraftId and linkType.
- Ensure store preserves linkage fields on load/create/switch.
- Keep existing behavior for variants unchanged.

### Phase 3: DraftList nested replies

- Build grouped reply children map by parentDraftId.
- Render replies under parent drafts.
- Preserve archive filtering and active highlight behavior.
- Add tests for:
  - nested rendering
  - selecting reply child
  - mixed variant + reply children under one parent

### Phase 4: Quick reply UX hardening

- Ensure quick reply sends parentDraftId and uses resolved source URL precedence.
- Add frontend test cases:
  - uses existing reply target URL when present
  - falls back to clicked post URL otherwise
  - creates child under active draft

### Phase 5: Verification

- Docker backend build and targeted tests.
- Docker web build/typecheck/tests.
- Manual flow:
  - Open reply draft with external target.
  - Publish.
  - Click quick reply.
  - Confirm new child appears nested under current draft, not top-level.
  - Confirm child still targets original external post URL.

## Risks and Mitigations

Risk: confusion between variants and replies.
Mitigation: distinct link type labels and separate UI sections.

Risk: old drafts without linkage fields.
Mitigation: nullable parent and default None link type make migration safe.

Risk: deep nesting complexity.
Mitigation: phase 1 supports one-level nesting in UI; recursive rendering can come later.

## Open Product Decisions

- Should replies be one-level only or fully recursive in list UI?
- Should parent draft show aggregated reply count badge?
- Should creating a reply from a child reply attach to child (current) or root draft?

Recommended default:

- Attach to current active draft (closest conversational context), while preserving source URL precedence described above.

## Acceptance Criteria

- Quick reply creates a new draft linked to the current draft.
- New reply draft appears nested under its parent in DraftList.
- Existing variant nesting still works.
- Sent draft cannot be patched to set/change reply target.
- Reply flow does not create disconnected top-level drafts for thread continuation.
