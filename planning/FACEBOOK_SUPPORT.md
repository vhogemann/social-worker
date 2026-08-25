# Facebook Support: Pages OAuth & Publishing Integration

## Context & Objectives

This document defines the architectural and implementation plan for adding full **Facebook (Pages)** support to **social-worker**.

With this integration, users can:
1. Connect one or more Facebook Pages via standard Meta OAuth 2.0.
2. Store Page credentials securely (permanent Page Access Tokens derived from 60-day User Access Tokens).
3. Draft, format, and adapt threads into Facebook-ready posts (single long-form or primary post + top-level comment segments).
4. Attach media (single photo, multi-photo carousels).
5. Publish directly to Facebook Pages with URL & remote ID tracking.

---

## .NET Library & Integration Landscape Research

### 1. Evaluated Options

| Option | Status & Compatibility | Fit for social-worker |
|---|---|---|
| **Meta Official C# SDK** | **Non-existent** (Meta only provides PHP, Python, Node, Android, iOS). | N/A |
| **`Facebook` (Facebook C# SDK)** | **Deprecated / Abandoned** (inactive since ~2015; incompatible with modern Graph API v20+ and .NET 10 `HttpClient`). | ❌ Reject (obsolete) |
| **`Microsoft.AspNetCore.Authentication.Facebook`** | **Active (Microsoft)**, but strictly designed for *app login/identity cookies*, not for managing background publishing tokens or Graph API calls. | ❌ Reject (wrong scope) |
| **Community wrappers (`CMS365.MetaSharp`, `Facebook.NET`)** | Niche, low maintenance, inflexible schema models. | ❌ Reject (external risk) |
| **Native Typed `HttpClient` + `System.Text.Json` (Recommended)** | **Modern .NET 10 standard**; aligns with `LinkedInApiClient` and `BlueskyApiClient` already in codebase. | ✅ **Recommended** |

### 2. Why Native Typed `HttpClient` is the Standard Choice
- **Architectural Parity**: Directly mirrors our production `LinkedInApiClient` and `BlueskyApiClient` implementations.
- **Zero Abandonment Risk**: No third-party dependency vulnerabilities or broken contracts on Graph API version upgrades (`v20.0` -> `v21.0`).
- **First-class Testability**: Uses standard `HttpMessageHandler` mock factories for lightning-fast xUnit testing in Docker.
- **Lightweight**: Zero additional NuGet dependencies required.

---

## Technical Specifications & Meta Graph API

### 1. Target Entity: Facebook Pages
Meta Graph API (v20.0+) **does not allow** automated posting to personal Facebook user profiles. Publishing is supported exclusively on **Facebook Pages** (and Groups with specific business app authorizations).

Each Facebook Page managed by the user is persisted as a distinct `Account` row in the database:
- `Platform`: `"Facebook"`
- `Handle`: `"{PageName} ({PageId})"` (or `"{PageName}"`)
- `CredentialsEncrypted`: Encrypted Page Access Token

### 2. OAuth 2.0 & Token Exchange Pipeline
- **Required Meta Scopes**:
  - `pages_manage_posts`
  - `pages_read_engagement`
  - `pages_show_list`
  - `public_profile`
- **Exchange Sequence**:
  1. Frontend redirects user to Meta OAuth dialog:
     `https://www.facebook.com/v20.0/dialog/oauth?client_id={app_id}&redirect_uri={redirect_uri}&scope=pages_manage_posts,pages_read_engagement,pages_show_list,public_profile&response_type=code`
  2. Backend exchanges `code` for short-lived User Access Token (~1-2 hrs):
     `GET https://graph.facebook.com/v20.0/oauth/access_token?client_id={id}&client_secret={secret}&redirect_uri={uri}&code={code}`
  3. Backend exchanges short-lived token for long-lived User Access Token (~60 days):
     `GET https://graph.facebook.com/v20.0/oauth/access_token?grant_type=fb_exchange_token&client_id={id}&client_secret={secret}&fb_exchange_token={short_token}`
  4. Backend fetches user's managed pages:
     `GET https://graph.facebook.com/v20.0/me/accounts?access_token={long_lived_user_token}`
  5. **Page Access Tokens derived from long-lived user tokens do not expire**, providing perpetual publishing access until permissions are revoked or the account password changes.

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

### Phase 1: Backend Domain Models & Configuration
- [ ] Create `FacebookOptions.cs` (`Facebook:AppId`, `Facebook:AppSecret`, `Facebook:RedirectUri`).
- [ ] Create `FacebookOAuthService.cs` (code exchange, long-lived token elevation, `me/accounts` fetch).
- [ ] Create `FacebookApiClient.cs` (Graph API feed post, photo upload, comment reply).
- [ ] Create `FacebookPublishService.cs` (orchestration of content formatting, media attachments, and post/comment publishing).
- [ ] Implement `FacebookPublisher.cs` (implementing `IPublisher` and replacing `StubPublishers.cs`).
- [ ] Register DI services in `PublishingExtensions.cs`.

### Phase 2: Backend Endpoints & Tests
- [ ] Add `GET /api/accounts/facebook/auth-url` and `POST /api/accounts/facebook/callback` to `AccountsEndpoint.cs`.
- [ ] Add unit tests in `SocialWorker.Api.Tests`:
  - `FacebookOAuthServiceTests.cs`
  - `FacebookApiClientTests.cs`
  - `FacebookPublisherTests.cs`

### Phase 3: Frontend Integration
- [ ] Add `getFacebookAuthUrl()` and `submitFacebookCallback(code)` to `web/src/api/accounts.ts`.
- [ ] Create `FacebookCallback.tsx` to process OAuth redirects.
- [ ] Update `ConnectionsTab.tsx` with Facebook connection option and branded connect action.
- [ ] Register `/settings/connections/facebook/callback` route in `App.tsx`.
- [ ] Add Vitest tests for frontend components.

### Phase 4: Verification & Docker Validation
- [ ] Run full test suites in Docker (`dotnet test`, `npm test`, `npm run typecheck`, `npm run build`).
- [ ] Rebuild and verify containers in `docker compose`.
