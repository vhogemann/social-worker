# Phase 5 TODO - Conversion Pipeline

## Goal
Implement and verify the conversion pipeline from platform thread content to LinkedIn publish payload.

## Status
- State: Complete
- Owner: Antigravity
- Last updated: 2026-08-10

## Checklist
- [x] Build pipeline step sequence and boundaries.
- [x] Add early cardinality gate for non-thread LinkedIn rule.
- [x] Add normalization and policy validation stage.
- [x] Add media asset lookup and readiness checks.
- [x] Add image upload orchestration path.
- [x] Add publish request build and send stage.
- [x] Add published result mapping to existing PublishedPost output.
- [x] Add tests for each stage where feasible.

## Exit Criteria
- [x] Pipeline behavior is deterministic for supported inputs.
- [x] Side effects are isolated to upload and publish stages.
