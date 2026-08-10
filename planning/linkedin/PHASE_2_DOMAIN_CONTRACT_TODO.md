# Phase 2 TODO - Domain Contract

## Goal
Define the normalized internal LinkedIn post contract used before wire payload mapping.

## Status
- State: Not started
- Owner: TBD
- Last updated: TBD

## Checklist
- [ ] Define contract model for one LinkedIn publish unit.
- [ ] Add explicit cardinality gate error: ThreadNotSupportedForLinkedIn.
- [ ] Define media union model: None, Article, Image, VideoFuture.
- [ ] Define validation constraints: text length, media count, mixed-media rejection.
- [ ] Define mapping error taxonomy.
- [ ] Add unit test cases for model construction and validation behavior.

## Exit Criteria
- [ ] Contract types and invariants are documented.
- [ ] Failure modes are explicit and test cases are listed.
