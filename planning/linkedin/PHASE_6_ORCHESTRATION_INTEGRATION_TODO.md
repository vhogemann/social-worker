# Phase 6 TODO - Orchestration Integration

## Goal
Integrate LinkedIn pipeline into existing publisher and validation orchestration paths.

## Status
- State: Not started
- Owner: TBD
- Last updated: TBD

## Checklist
- [ ] Replace LinkedIn stub publisher with real implementation path.
- [ ] Keep IPublisher contract stable.
- [ ] Route platform validation via existing platform policy mechanisms.
- [ ] Ensure Bluesky-only checks remain Bluesky-only.
- [ ] Preserve existing persistence flow for Post records.
- [ ] Add integration tests for publish endpoint and tool orchestration.

## Exit Criteria
- [ ] LinkedIn publish flow is integrated without regressions to existing platforms.
- [ ] Platform-specific validation behavior is enforced correctly.
