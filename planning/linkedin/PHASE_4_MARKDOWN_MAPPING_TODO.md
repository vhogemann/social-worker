# Phase 4 TODO - Markdown Mapping

## Goal
Define deterministic mapping from internal Markdown content into one LinkedIn publish unit.

## Status
- State: Not started
- Owner: TBD
- Last updated: TBD

## Checklist
- [ ] Confirm segment split strategy for LinkedIn input validation.
- [ ] Enforce exactly one non-empty segment for LinkedIn publishing.
- [ ] Define media classification order: Image, Article, None.
- [ ] Define YouTube URL mapping to ARTICLE originalUrl.
- [ ] Define mixed-content rejection behavior.
- [ ] Define alt text precedence and fallback rules.
- [ ] Add unit tests for mapping success and failure cases.

## Exit Criteria
- [ ] Same Markdown input always produces the same normalized model output.
- [ ] Mapping edge cases are covered by tests.
