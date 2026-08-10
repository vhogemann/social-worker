# Phase 5 TODO - Conversion Pipeline

## Goal
Implement and verify the conversion pipeline from platform thread content to LinkedIn publish payload.

## Status
- State: Not started
- Owner: TBD
- Last updated: TBD

## Checklist
- [ ] Build pipeline step sequence and boundaries.
- [ ] Add early cardinality gate for non-thread LinkedIn rule.
- [ ] Add normalization and policy validation stage.
- [ ] Add media asset lookup and readiness checks.
- [ ] Add image upload orchestration path.
- [ ] Add publish request build and send stage.
- [ ] Add published result mapping to existing PublishedPost output.
- [ ] Add tests for each stage where feasible.

## Exit Criteria
- [ ] Pipeline behavior is deterministic for supported inputs.
- [ ] Side effects are isolated to upload and publish stages.
