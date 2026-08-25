# Instagram Support: Business/Creator Publishing & OAuth Integration

## Context & Objectives

This document defines the architectural and implementation plan for adding full **Instagram (Professional/Creator/Business Accounts)** publishing and OAuth support to **social-worker**.

With this integration, users can:
1. Connect one or more Instagram Professional accounts (discovered via Meta OAuth or direct Instagram Login).
2. Validate Instagram-specific constraints (mandatory image attachment, 2,200 character caption limit, up to 10 carousel items).
3. Draft and adapt content into Instagram-optimized visual captions and hashtags.
4. Publish single-image and multi-image carousel posts using Meta's 2-step Container Publishing API (`media` -> `media_publish`).
5. Track published posts with direct Instagram URLs and remote media IDs.

---

## Technical Specifications & Instagram Graph API

### 1. Target Entity: Instagram Professional Accounts
- **Account Type**: Instagram Business or Creator account. (Personal accounts are not supported by Meta's Content Publishing API).
- **Multi-Account Integration**:
  - `Platform`: `"Instagram"`
  - `Handle`: `"@{username}"`
  - `CredentialsEncrypted`: Encrypted Page / Instagram User Access Token with `InstagramUserId` stored in metadata.

### 2. OAuth 2.0 & Discovery Pipeline
Meta provides two discovery paths:
1. **Unified Meta Login (Recommended & Shared with Facebook)**:
   - Scopes: `instagram_basic`, `instagram_content_publish`, `pages_show_list`, `pages_read_engagement`, `public_profile`.
   - Query: `GET https://graph.facebook.com/v20.0/me/accounts?fields=id,name,access_token,instagram_business_account{id,username,name,profile_picture_url}`.
   - Extracts all linked Instagram accounts along with their parent Page tokens.
2. **Direct Instagram Business Login**:
   - Dialog: `https://www.instagram.com/oauth/authorize?client_id={app_id}&redirect_uri={redirect_uri}&response_type=code&scope=instagram_business_basic,instagram_business_content_publish`.

### 3. Container Publishing Protocol

Instagram enforces a **2-step asynchronous container lifecycle**:

```
[Media Assets in Draft]
          │
          ▼
1. Create Item Container(s)
   POST https://graph.facebook.com/v20.0/{ig-user-id}/media
   - Single Image: image_url={url}, caption={text} -> returns { id: "container_id" }
   - Carousel (2-10 items):
       a) POST /{ig-user-id}/media with image_url={url}, is_carousel_item=true -> returns child { id }
       b) POST /{ig-user-id}/media with media_type=CAROUSEL, children=[id1, id2], caption={text} -> returns parent { id }
          │
          ▼
2. Verify Container Readiness (Optional/Recommended for video/large media)
   GET https://graph.facebook.com/v20.0/{container_id}?fields=status_code
   - Wait until status_code == "FINISHED"
          │
          ▼
3. Publish Container
   POST https://graph.facebook.com/v20.0/{ig-user-id}/media_publish
   - Body: creation_id={container_id}
   - Returns: { id: "{ig_media_id}" }
   - URL: https://www.instagram.com/p/{shortcode}/
```

### 4. Content & Platform Rules
- **Media Requirement**: Every Instagram post **must contain at least 1 image or video**. Pure text posts are rejected.
- **Caption Limits**: Maximum 2,200 characters, maximum 30 hashtags.
- **Carousel Limits**: Between 2 and 10 images/videos.
- **Aspect Ratio**: Between 4:5 (0.8 portrait) and 1.91:1 (landscape). Recommended: 1:1 square or 4:5 vertical.

---

## Tasks & Phases

### Phase 1: Backend Domain Models & Clients
- [ ] Create `InstagramOptions.cs` (`AppId`, `AppSecret`, `RedirectUri`).
- [ ] Create `InstagramOAuthService.cs` (handles Meta token exchange and Instagram account discovery).
- [ ] Create `InstagramApiClient.cs` (creates item containers, carousel containers, checks container status, and calls `media_publish`).
- [ ] Create `InstagramPublishService.cs` (validates media presence, builds image URLs, manages 2-step publish flow).
- [ ] Implement `InstagramPublisher.cs` (implements `IPublisher` and replaces stub).
- [ ] Update `InstagramPlatformContentPolicy.cs` with mandatory image check and 2,200 character validation.
- [ ] Register DI services in `PublishingExtensions.cs`.

### Phase 2: Backend Endpoints & Tests
- [ ] Add `GET /api/accounts/instagram/auth-url` and `POST /api/accounts/instagram/callback` in `AccountsEndpoint.cs`.
- [ ] Add unit tests in `SocialWorker.Api.Tests`:
  - `InstagramOAuthServiceTests.cs`
  - `InstagramApiClientTests.cs`
  - `InstagramPublisherTests.cs`
  - `InstagramContentPolicyTests.cs`

### Phase 3: Frontend Integration
- [ ] Add `getInstagramAuthUrl()` and `submitInstagramCallback(code)` in `web/src/api/accounts.ts`.
- [ ] Create `InstagramCallback.tsx` to handle OAuth callback redirects.
- [ ] Update `ConnectionsTab.tsx` with Instagram connection option and branded connect action.
- [ ] Register `/settings/connections/instagram/callback` route in `App.tsx`.
- [ ] Add Vitest tests for frontend components.

### Phase 4: Verification & Docker Validation
- [ ] Run full test suites in Docker (`dotnet test`, `npm test`, `npm run typecheck`, `npm run build`).
- [ ] Rebuild and verify containers in `docker compose`.
