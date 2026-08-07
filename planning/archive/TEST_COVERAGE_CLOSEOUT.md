# Test Coverage Closeout Plan

Status: Completed (Archived)
Owner: Copilot + user
Theme: Test coverage and release confidence

## Problem

The core backend test surface is largely covered, and the frontend is close to complete. What remains is a small set of composite web components plus final verification that the full stack and e2e flows still pass after the last coverage additions.

## Goals

- Finish the remaining frontend component tests in the coverage tracker.
- Keep the current test plan accurate while the final gaps are closed.
- Re-run the Docker build, API test suite, web test suite, and e2e checks after the last tests land.
- Archive the coverage tracker once the remaining gaps are closed.

## Non-Goals

- New product features.
- Refactoring production code purely to make tests easier unless a test exposes a real bug.
- Expanding test scope beyond the remaining gaps unless a regression forces it.

## Remaining Coverage Targets

Finish the composite frontend items that were open in [TEST_PLAN_2026-07-28.md](TEST_PLAN_2026-07-28.md):

- `DraftList` (`web/src/components/DraftList/DraftList.tsx`)
- `MarkdownEditor` (`web/src/components/EditorPanel/MarkdownEditor.tsx`)
- `SourceItem` (`web/src/components/EditorPanel/Sources/SourceItem.tsx`)
- `MediaAssetItem` (`web/src/components/EditorPanel/Sources/MediaAssetItem.tsx`)
- `ThreadPreview` (`web/src/components/EditorPanel/ThreadPreview/`)
- `AdaptVariantsModal` (`web/src/components/EditorPanel/AdaptVariantsModal.tsx`)

## Plan

### Phase 1: Close the composite frontend gaps

- Add focused store-mocked tests for each remaining component.
- Prefer behavior over snapshots: list actions, state transitions, preview rendering, and mutation affordances.
- Keep selectors stable and explicit so the tests are durable.

### Phase 2: Refresh coverage tracking

- Mark each finished row in [TEST_PLAN_2026-07-28.md](TEST_PLAN_2026-07-28.md).
- Tighten the remaining-gaps list until it is empty.
- If a component is shown to be already covered indirectly, update the tracker to reflect the actual tested surface instead of leaving stale rows behind.

### Phase 3: Final verification

- Run the API build in Docker.
- Run the API test suite in Docker.
- Run the web build, typecheck, and test suite in Docker.
- Run the e2e smoke suite with the dedicated compose stack.
- Fix any regressions surfaced by those runs before closing the plan.

### Validation note

- The web dependency refresh pass has already been applied and validated in Docker with `npm run build`, `npm run typecheck`, and `npm run test`.

### Phase 4: Closeout

- Archive [TEST_PLAN_2026-07-28.md](TEST_PLAN_2026-07-28.md) once the remaining gaps are closed and verification is green.
- Update [../PLAN.md](../PLAN.md) so quality and release confidence no longer depends on the backlog tracker.

## Acceptance Criteria

- Every row in the remaining-gaps section of [TEST_PLAN_2026-07-28.md](TEST_PLAN_2026-07-28.md) is closed or explicitly reclassified.
- The API build and test suite pass in Docker.
- The web build, typecheck, and test suite pass in Docker.
- E2E smoke passes in Docker.
- The coverage tracker is archived or clearly reduced to historical reference only.
