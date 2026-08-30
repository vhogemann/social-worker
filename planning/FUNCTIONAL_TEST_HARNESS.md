# Functional Test Harness — Live Stack & Bluesky Verification

## Overview

The Functional Test Harness (`scripts/functional-test.mjs` and `scripts/run-functional-test.sh`) allows automated, agent-controlled end-to-end testing and diagnostics of the live Social Worker stack against the production Bluesky network.

## Scenarios

| Scenario | Command | Purpose |
|---|---|---|
| `health` | `./scripts/run-functional-test.sh health` | Probes Social Worker API reachable and Bluesky ATProto PDS connectivity |
| `draft` | `./scripts/run-functional-test.sh draft` | Tests user authentication, draft creation, read-back, content update, and deletion |
| `feeds` | `./scripts/run-functional-test.sh feeds` | Tests listing feed subscriptions and automation workflows |
| `publish` | `./scripts/run-functional-test.sh publish` | Full live e2e: connects Bluesky credentials, creates multi-segment thread, publishes to Bluesky, queries ATProto XRPC out-of-band to verify root/reply trees, and cleans up posts |
| `full` | `./scripts/run-functional-test.sh full` | Runs `health`, `draft`, `feeds`, and `publish` (if credentials are configured) |

## Configuration

Credentials can be supplied in `.env.functional` in the project root (see `.env.functional.example`):

```bash
# Social Worker Instance
FUNCTIONAL_SW_URL=http://localhost:8101
FUNCTIONAL_SW_USERNAME=admin
FUNCTIONAL_SW_PASSWORD=changeme

# Bluesky Dedicated Test Account
FUNCTIONAL_BLUESKY_HANDLE=your-test-account.bsky.social
FUNCTIONAL_BLUESKY_APP_PASSWORD=xxxx-xxxx-xxxx-xxxx
FUNCTIONAL_BLUESKY_PDS_URL=https://bsky.social

# Test Options
FUNCTIONAL_CLEANUP_POSTS=true
FUNCTIONAL_TAG=[functional-test]
```

## Out-of-Band ATProto Verification

When the `publish` scenario runs:
1. Social Worker publishes the thread to Bluesky via ATProto.
2. The harness connects independently to `https://bsky.social/xrpc/app.bsky.feed.getPostThread?uri=<root_uri>`.
3. It asserts that the root post exists on Bluesky, matches the tag/content, and that the reply chain pointers (`reply.root`, `reply.parent`) are structurally intact.
4. If `FUNCTIONAL_CLEANUP_POSTS=true`, it calls `com.atproto.repo.deleteRecord` for each published segment to clean up the test account.
5. It marks the draft `Status="Deleted"` in Social Worker.
