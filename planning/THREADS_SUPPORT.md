# Meta Threads Support Plan & Delivery

**Status**: Delivered (2026-10-08)  
**Tracking**: [PLAN.md](../PLAN.md)

---

## 1. Overview & Capabilities Delivered

Meta Threads support is now fully integrated end-to-end across social-worker:
- **Platform Foundation**: `SocialPlatform.MetaThreads` domain enum with alias-tolerant parsing (`threads`, `meta threads`, `metathreads`).
- **Resilient Multi-Post Publishing Engine**: Chained container replies (`reply_to_id`), carousel support (up to 10 images), status polling budget (~240s with exponential backoff), and idempotency prefix verification via `PublishResumePlanner`.
- **Database & Migration Layer**:
  - `0015_add_oauth_states.sql`: Single-use DB-backed CSRF nonces (`OAuthStates`).
  - `0016_resilient_publishing.sql`: `PublishedText` and `RemoteCid` on `Posts`, `LastPublishError` on `PlatformThreads`, and `PartiallySent` thread stage.
  - `0017_account_tokens_and_external_id.sql`: `ExternalId` and `TokenExpiresAt` on `Accounts`.
- **OAuth & Account Lifecycle**:
  - Official Threads API OAuth flow (`/oauth/authorize`, code exchange, 60-day long-lived token exchange).
  - Background automated token refresh (`TokenRefreshHostedService` running every 6h, refreshing tokens expiring within 7 days).
- **Validation**:
  - 500-character maximum per segment.
  - Carousel limit validation (max 10 media assets).
  - Placeholder link detection and unsupported markdown styling warnings.
- **Frontend Experience**:
  - Centralized `getPlatformLabel()` displaying "Threads" across badges, dropdowns, and calendar views.
  - Account connection with OAuth redirect in Connections settings tab.
  - Dedicated OAuth callback handler (`/oauth/callback/threads`).
  - Thread Preview (`MetaThreadsPostPreview.tsx`) with vertical thread line connector and 500-char / carousel warning limits.
  - Partial publication banner and one-click "Resume Publish" button in Editor header.

---

## 2. Test Verification

- **.NET API**: 586 unit tests passing (0 failures).
- **Web**: 462 Vitest tests in 90 test files passing (0 failures).
- **Typecheck & Build**: Clean production compilation via Vite and dotnet.
