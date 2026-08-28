# Facebook Support: Pages OAuth & Publishing Integration

## Context & Objectives

This document defines the implementation plan for adding full **Facebook (Pages)** publishing to **social-worker**.

With this integration, users can:
1. Connect one or more Facebook Pages via shared Meta OAuth (see [META_OAUTH.md](META_OAUTH.md)).
2. Store Page credentials securely (permanent Page Access Tokens).
3. Draft, format, and adapt threads into Facebook-ready posts (single long-form or primary post + top-level comment segments).
4. Attach media (single photo, multi-photo).
5. Publish directly to Facebook Pages with URL & remote ID tracking.

---

## Architecture Decision Record

| Decision | Resolution |
|---|---|
| OAuth | Shared Meta OAuth (`META_OAUTH.md`). One "Connect Meta" button discovers both Facebook Pages and Instagram accounts. |
| Token storage | Permanent Page Access Token per `Account` row (derived from long-lived user token via `/me/accounts`). |
| Thread mapping | Segment 0 = primary feed post. Segments 1..N = top-level comments under the post. |
| HTTP client | Native typed `HttpClient` (matches `LinkedInApiClient` pattern). |
| Validator | TBD — may use `PlatformPolicyDraftValidator` or a dedicated `FacebookDraftValidator`. |

---

## Technical Specifications & Meta Graph API

### 1. Target Entity: Facebook Pages

Meta Graph API (v20.0+) **does not allow** automated posting to personal Facebook user profiles. Publishing is supported exclusively on **Facebook Pages**.

Each Facebook Page managed by the user is persisted as a distinct `Account` row:
- `Platform`: `"Facebook"`
- `Handle`: `"{PageName} ({PageId})"`
- `CredentialsEncrypted`: Encrypted permanent Page Access Token

### 2. OAuth 2.0 & Discovery

Handled by shared `MetaOAuthService` — see [META_OAUTH.md](META_OAUTH.md).

### 3. Publishing Protocol

- **Text & Link Post**:
  - `POST https://graph.facebook.com/v20.0/{page-id}/feed`
  - Body params: `message={text}`, `link={url}` (optional).
  - Returns: `{ "id": "{page-id}_{post-id}" }`.
- **Photo Attachments (Single / Multi-photo)**:
  - Step 1: `POST https://graph.facebook.com/v20.0/{page-id}/photos` with `published=false`, `source` (multipart byte payload) -> returns `{ "id": "{photo-id}" }`.
  - Step 2: `POST https://graph.facebook.com/v20.0/{page-id}/feed` with `message={text}` and `attached_media=[{"media_fbid":"{photo-id-1}"}, {"media_fbid":"{photo-id-2}"}]`.
- **Multi-Segment Thread Handling**:
  - Segment 0 is published as the main Page post.
  - Segments 1..N are posted as top-level comments under the main post via `POST https://graph.facebook.com/v20.0/{post-id}/comments` with `message={segment_content}`.

---

## Tasks & Phases

Facebook publishing is implemented as part of the combined Meta integration batch (alongside Instagram). See [INSTAGRAM_SUPPORT.md](INSTAGRAM_SUPPORT.md) for the shared phasing.

### Backend Services
- [ ] Create `FacebookApiClient.cs` (Graph API feed post, photo upload, comment reply).
- [ ] Create `FacebookPublishService.cs` (orchestration of content formatting, media attachments, and post/comment publishing).
- [ ] Implement `FacebookPublisher.cs` (implementing `IPublisher`, replacing stub in `StubPublishers.cs`).
- [ ] Register DI services in `PublishingExtensions.cs`.

### Tests
- [ ] `FacebookApiClientTests.cs`
- [ ] `FacebookPublisherTests.cs`
- [ ] `FacebookPublishServiceTests.cs`
