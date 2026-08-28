# Instagram Support: Business/Creator Publishing & OAuth Integration

## Context & Objectives

This document defines the implementation plan for adding full **Instagram (Professional/Creator/Business Accounts)** publishing to **social-worker**.

With this integration, users can:
1. Connect Instagram Professional accounts via shared Meta OAuth (see [META_OAUTH.md](META_OAUTH.md)).
2. Validate Instagram-specific constraints (mandatory image, 2,200 character caption, aspect ratio, carousel limits).
3. Draft and adapt content into Instagram-optimized visual captions and hashtags.
4. Publish single-image posts and multi-image carousels using Meta's 2-step Container Publishing API.
5. Track published posts with direct Instagram URLs and remote media IDs.

---

## Architecture Decision Record

| Decision | Resolution |
|---|---|
| OAuth | Shared Meta OAuth (`META_OAUTH.md`). One "Connect Meta" button discovers both Facebook Pages and Instagram accounts. |
| Image delivery | App is publicly reachable at `social.plopsaurus.xyz`. Media URLs constructed via `App__PublicBaseUrl` env var + `/api/media/{id}`. |
| Post mapping | 1 segment = single image post. 2-10 segments = 1 carousel post. >10 segments = validation error. |
| Carousel captions | Segment 0 text = carousel caption. Segments 1..N text is ignored/stripped. |
| Image requirement | Hard error: every Instagram post must have at least one image. |
| Aspect ratio | Warning (not error). Agent autonomously invokes image-engine tools to fix. |
| Container polling | Exponential backoff (1s, 2s, 4s… cap 30s, hard timeout 2min). |
| Validator | Full rule-based `InstagramDraftValidator` (like Bluesky), replacing `PlatformPolicyDraftValidator` stub. |
| Token storage | Permanent Page Access Token per `Account` row (via shared Meta OAuth). |

---

## Technical Specifications & Instagram Graph API

### 1. Target Entity: Instagram Professional Accounts
- **Account Type**: Instagram Business or Creator account. (Personal accounts are not supported by Meta's Content Publishing API).
- **Multi-Account Integration**:
  - `Platform`: `"Instagram"`
  - `Handle`: `"@{username}"`
  - `CredentialsEncrypted`: Encrypted Page Access Token (permanent, derived from long-lived user token).
  - Instagram User ID stored as metadata or derived at publish time.

### 2. OAuth 2.0 & Discovery

Handled by shared `MetaOAuthService` — see [META_OAUTH.md](META_OAUTH.md).

The `GET /me/accounts?fields=...,instagram_business_account{id,username,name,profile_picture_url}` call discovers Instagram accounts linked to Facebook Pages. Each discovered account is stored as a separate `Account` row with `Platform="Instagram"`.

### 3. Container Publishing Protocol

Instagram enforces a **2-step asynchronous container lifecycle**:

```
[Media Assets in Draft]
          |
          v
1. Create Item Container(s)
   POST https://graph.facebook.com/v20.0/{ig-user-id}/media
   - Single Image: image_url={public_url}, caption={text} -> returns { id: "container_id" }
   - Carousel (2-10 items):
       a) POST /{ig-user-id}/media with image_url={url}, is_carousel_item=true -> returns child { id }
       b) POST /{ig-user-id}/media with media_type=CAROUSEL, children=[id1, id2], caption={text} -> returns parent { id }
          |
          v
2. Poll Container Readiness (exponential backoff: 1s, 2s, 4s... cap 30s, timeout 2min)
   GET https://graph.facebook.com/v20.0/{container_id}?fields=status_code
   - Wait until status_code == "FINISHED"
   - If status_code == "ERROR", fail with error message from status field
          |
          v
3. Publish Container
   POST https://graph.facebook.com/v20.0/{ig-user-id}/media_publish
   - Body: creation_id={container_id}
   - Returns: { id: "{ig_media_id}" }
   - URL: https://www.instagram.com/p/{shortcode}/
```

### 4. Thread-to-Post Mapping

```
Draft Segments          Instagram Result
--------------          ----------------
1 segment               Single image post (image + caption)
2-10 segments           1 carousel post:
                          - Segment 0 text = caption
                          - Segments 1..N text = IGNORED
                          - Each segment contributes 1 image
                          - Alt text from MediaAsset used for carousel items
11+ segments            Validation ERROR (max 10 carousel items)
```

### 5. Content & Platform Rules
- **Media Requirement**: Every Instagram post **must contain at least 1 image**. Pure text posts are rejected (hard error).
- **Caption Limits**: Maximum 2,200 characters, maximum 30 hashtags.
- **Carousel Limits**: Between 2 and 10 images.
- **Aspect Ratio**: Between 4:5 (0.8 portrait) and 1.91:1 (landscape). Recommended: 1:1 square or 4:5 vertical. **Warning** at validation time; agent auto-fixes via image-engine resize tools.
- **Segments 1+ text**: Warning if non-trivial text exists (will be stripped by publisher).

### 6. Public Media URL Construction

Instagram's container API requires publicly reachable `image_url` values. The app constructs these from:

```
{App__PublicBaseUrl}/api/media/{mediaAssetId}
```

Example: `https://social.plopsaurus.xyz/api/media/a1b2c3d4-...`

This requires a new `App__PublicBaseUrl` environment variable.

---

## Validation Rules (`InstagramDraftValidator`)

Full rule-based validator following the Bluesky pattern (`BlueskyDraftValidator` + `IValidateDraftRule<T>`):

| Rule | Severity | Description |
|---|---|---|
| `InstagramRequiredImageRule` | Error | Every draft must have at least 1 image attachment |
| `InstagramMaxCaptionLengthRule` | Error | Segment 0 caption must not exceed 2,200 characters |
| `InstagramMaxHashtagsRule` | Warning | Caption should not exceed 30 hashtags |
| `InstagramMaxCarouselItemsRule` | Error | Draft must not exceed 10 segments (carousel limit) |
| `InstagramAspectRatioRule` | Warning | Images should be between 4:5 and 1.91:1 aspect ratio |
| `InstagramNonImageSegmentTextRule` | Warning | Segments 1+ with non-trivial text will have text stripped |
| `InstagramSegmentMissingImageRule` | Error | Each segment (carousel item) must have exactly 1 image |
| `InstagramMissingAltTextRule` | Warning | Images should have ALT text configured |

---

## Tasks & Phases

### Phase 1: Infrastructure & Configuration
- [ ] Add `App__PublicBaseUrl` env var and `AppOptions.cs` to read it.
- [ ] Create `MetaOptions.cs`, `MetaOAuthService.cs`, response models (see [META_OAUTH.md](META_OAUTH.md)).
- [ ] Update `.env.example` with `APP__PUBLICBASEURL`, `META__APPID`, `META__APPSECRET`, `META__REDIRECTURI`.

### Phase 2: Instagram Backend Services
- [ ] Create `InstagramContentValidator.cs` (segment analysis, produces `InstagramSegmentValidation` records).
- [ ] Create `InstagramSegmentValidation.cs` record.
- [ ] Create validation rules in `Publishing/Instagram/Validation/Rules/`:
  - `InstagramRequiredImageRule.cs`
  - `InstagramMaxCaptionLengthRule.cs`
  - `InstagramMaxHashtagsRule.cs`
  - `InstagramMaxCarouselItemsRule.cs`
  - `InstagramAspectRatioRule.cs`
  - `InstagramNonImageSegmentTextRule.cs`
  - `InstagramSegmentMissingImageRule.cs`
  - `InstagramMissingAltTextRule.cs`
- [ ] Create `InstagramDraftValidator.cs` (implements `IDraftValidator`, mirrors `BlueskyDraftValidator`).
- [ ] Create `InstagramApiClient.cs` (container create, status poll, media_publish, typed `HttpClient`).
- [ ] Create `InstagramPublishService.cs` (orchestrates: build URLs, create containers, poll, publish, assemble results).
- [ ] Implement `InstagramPublisher.cs` (implements `IPublisher`, replaces stub in `StubPublishers.cs`).

### Phase 3: Facebook Backend Services
- [ ] Create `FacebookApiClient.cs` (feed post, photo upload, comment reply, typed `HttpClient`).
- [ ] Create `FacebookPublishService.cs` (orchestrates text/photo/thread publishing).
- [ ] Implement `FacebookPublisher.cs` (implements `IPublisher`, replaces stub in `StubPublishers.cs`).
- [ ] Create `FacebookDraftValidator.cs` if needed (or keep `PlatformPolicyDraftValidator`).

### Phase 4: DI Registration & Wiring
- [ ] Update `PublishingExtensions.cs`:
  - Register `MetaOptions`, `MetaOAuthService`, `AppOptions`.
  - Register Instagram validation rules, `InstagramContentValidator`, `InstagramDraftValidator`.
  - Register `InstagramApiClient`, `InstagramPublishService`, `InstagramPublisher` (replace stub).
  - Register `FacebookApiClient`, `FacebookPublishService`, `FacebookPublisher` (replace stub).
- [ ] Update `DraftValidatorResolver.cs` to inject `InstagramDraftValidator` instead of `PlatformPolicyDraftValidator`.

### Phase 5: Backend Endpoints
- [ ] Add `GET /api/accounts/meta/auth-url` and `POST /api/accounts/meta/callback` to `AccountsEndpoint.cs`.
- [ ] Remove Facebook/Instagram stubs from `StubPublishers.cs` (only Twitter stub remains).

### Phase 6: Frontend Integration
- [ ] Add `getMetaAuthUrl()` and `submitMetaCallback(code)` to `web/src/api/accounts.ts`.
- [ ] Create `MetaCallback.tsx` to handle OAuth redirect.
- [ ] Update `ConnectionsTab.tsx` with "Connect Meta" button.
- [ ] Register `/oauth/callback/meta` route in `App.tsx`.

### Phase 7: Tests
- [ ] `MetaOAuthServiceTests.cs`
- [ ] `InstagramContentValidatorTests.cs`
- [ ] `InstagramDraftValidatorTests.cs` (per-rule tests)
- [ ] `InstagramApiClientTests.cs`
- [ ] `InstagramPublisherTests.cs`
- [ ] `FacebookApiClientTests.cs`
- [ ] `FacebookPublisherTests.cs`
- [ ] Frontend component tests (Vitest)

### Phase 8: Verification & Docker Validation
- [ ] Run full test suites in Docker (`dotnet test`, `npm test`, `npm run typecheck`, `npm run build`).
- [ ] Rebuild and verify containers in `docker compose`.
