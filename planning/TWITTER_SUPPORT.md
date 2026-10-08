# Twitter / X Support Plan & Delivery

**Status**: Delivered (2026-10-08)  
**Tracking**: [PLAN.md](../PLAN.md)

---

## 1. Overview & Capabilities Delivered

Twitter / X support is now fully integrated end-to-end across social-worker:
- **Platform Foundation**:
  - `SocialPlatform.Twitter` domain enum with alias-tolerant parsing (`twitter`, `x`).
  - Configuration options `TwitterOptions` supporting `ClientId`, optional `ClientSecret`, `RedirectUri`, `Scopes`, `BaseUrl`, `OAuthBaseUrl`, and `InterTweetDelayMs`.
- **Text & Counting Rules Engine**:
  - `TwitterTextWeightCalculator`: Implements official 280 weighted-character counting.
  - Automatic `t.co` URL budgeting: any URL (`https://...`, `http://...`) counts as exactly 23 weighted units.
  - Unicode-aware character weighting: ASCII/Latin count as 1 unit; CJK, symbols, emojis, and surrogate pairs count as 2 units.
- **Draft Validation**:
  - `TwitterDraftValidator`: Strictly enforces the 280 weighted-character limit per segment.
  - Enforces text-first scope for v1 (rejects media attachments with an informative error message).
  - Flags placeholder links (`[insert link]`) as errors.
  - Flags markdown links `[label](url)` and markdown styling (bold, italic, headings) as warnings since Twitter clients display plain text.
  - Registered in `DraftValidatorResolver`.
- **OAuth 2.0 PKCE Lifecycle & Token Rotation**:
  - `TwitterOAuthService`: Generates cryptographic PKCE `code_verifier` (Base64Url random bytes) and `code_challenge` (SHA-256 S256).
  - Nonce and verifier stored in `OAuthState.PayloadJson`, consuming securely on callback.
  - Dual confidential/public client support: uses HTTP Basic Auth if `ClientSecret` is present, or PKCE-only with `client_id` in body if omitted.
  - `TwitterTokenRefresher`: Implements `ITokenRefresher` for `TokenRefreshHostedService`, atomically rotating `access_token` and `refresh_token` and updating encrypted credentials. Marks `Account.Status = "Invalid"` on unrecoverable refresh failure.
  - Endpoints: `GET /api/accounts/twitter/auth-url` and `POST /api/accounts/twitter/callback`.
- **Resilient Publishing Engine**:
  - `TwitterApiClient`: Chained tweet creation via `POST /2/tweets` with `text` and optional `reply: { in_reply_to_tweet_id: ... }`.
  - `TwitterPublisher`: Resilient thread publisher using `PublishResumePlanner` for prefix verification and idempotent resume.
  - Configurable inter-tweet delay (default 1000ms) between chained tweets to ensure distributed reply graph indexing.
  - Creates post URLs formatted as `https://x.com/{handle}/status/{tweet_id}`.
- **Frontend Experience**:
  - `TwitterThreadPreview.tsx`: Live preview with vertical thread connector line, live weighted character counter, 280-char limit warning alerts, and media restriction banners.
  - Connections tab: "X (Twitter)" connection card, "Connect with X" button with `faXTwitter` icon, handle `@handle`, status badge, and token expiry.
  - Dedicated OAuth callback handler (`/oauth/callback/twitter` via `TwitterCallbackPage.tsx`).
  - Draft creation modal: "X (Twitter)" platform option.

---

## 2. Test Verification

- **.NET API**: 619 unit tests passing (0 failures).
- **Web**: 474 Vitest tests in 91 test files passing (0 failures).
- **Typecheck & Build**: Clean production compilation via Vite and dotnet.
