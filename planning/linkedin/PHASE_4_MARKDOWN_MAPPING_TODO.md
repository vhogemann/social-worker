# Phase 4 TODO - Markdown Mapping

## Goal
Define deterministic mapping from internal Markdown content into one LinkedIn publish unit.

## Status
- State: Complete
- Owner: Antigravity
- Last updated: 2026-08-10

## Checklist
- [x] Confirm segment split strategy for LinkedIn input validation.
- [x] Enforce exactly one non-empty segment for LinkedIn publishing.
- [x] Define media classification order: Image, Article, None.
- [x] Define YouTube URL mapping to ARTICLE originalUrl.
- [x] Define mixed-content rejection behavior.
- [x] Define alt text precedence and fallback rules.
- [x] Add unit tests for mapping success and failure cases.

## Exit Criteria
- [x] Same Markdown input always produces the same normalized model output.
- [x] Mapping edge cases are covered by tests.
