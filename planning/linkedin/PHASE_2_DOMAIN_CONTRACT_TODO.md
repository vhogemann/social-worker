# Phase 2 TODO - Domain Contract

## Goal
Define the normalized internal LinkedIn post contract used before wire payload mapping.

## Status
- State: Complete
- Owner: Antigravity
- Last updated: 2026-08-10

## Checklist
- [x] Define contract model for one LinkedIn publish unit.
- [x] Add explicit cardinality gate error: ThreadNotSupportedForLinkedIn.
- [x] Define media union model: None, Article, Image, VideoFuture.
- [x] Define validation constraints: text length, media count, mixed-media rejection.
- [x] Define mapping error taxonomy.
- [x] Add unit test cases for model construction and validation behavior.

## Exit Criteria
- [x] Contract types and invariants are documented.
- [x] Failure modes are explicit and test cases are listed.
