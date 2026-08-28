# Meta OAuth: Shared Facebook & Instagram OAuth Integration

## Context & Objectives

This document defines the shared Meta OAuth 2.0 infrastructure that underpins both **Facebook Pages** and **Instagram Professional** account connections in social-worker.

A single "Connect Meta" button triggers one OAuth consent screen. The backend discovers all Facebook Pages and linked Instagram Professional accounts from a single `GET /me/accounts` call and registers them as separate `Account` rows.

---

## Architecture Decision Record

| Decision | Resolution |
|---|---|
| OAuth flow | Unified Meta Login dialog (`facebook.com/v20.0/dialog/oauth`) with combined scopes |
| Token storage | Permanent Page Access Tokens stored directly per `Account` row (no user token retained) |
| Account discovery | Single `GET /me/accounts` with `instagram_business_account` expansion |
| Configuration | `Meta__AppId`, `Meta__AppSecret`, `Meta__RedirectUri` in `.env` |
| UI | One "Connect Meta" button in ConnectionsTab |
| HTTP client | Native typed `HttpClient` (matches `LinkedInOAuthService` pattern) |

---

## Technical Specifications

### 1. OAuth 2.0 Scopes (Combined)

```
pages_manage_posts
pages_read_engagement
pages_show_list
instagram_basic
instagram_content_publish
public_profile
```

### 2. Token Exchange Pipeline

```
User clicks "Connect Meta"
        |
        v
1. Frontend redirects to Meta OAuth dialog:
   https://www.facebook.com/v20.0/dialog/oauth
   ?client_id={appId}
   &redirect_uri={redirectUri}
   &scope={combined_scopes}
   &response_type=code
        |
        v
2. Backend exchanges code for short-lived User Access Token (~1-2h):
   GET https://graph.facebook.com/v20.0/oauth/access_token
   ?client_id={appId}&client_secret={appSecret}
   &redirect_uri={redirectUri}&code={code}
        |
        v
3. Backend elevates to long-lived User Access Token (~60d):
   GET https://graph.facebook.com/v20.0/oauth/access_token
   ?grant_type=fb_exchange_token
   &client_id={appId}&client_secret={appSecret}
   &fb_exchange_token={short_token}
        |
        v
4. Backend discovers accounts:
   GET https://graph.facebook.com/v20.0/me/accounts
   ?fields=id,name,access_token,instagram_business_account{id,username,name,profile_picture_url}
   &access_token={long_lived_token}
        |
        v
5. For each discovered Page:
   - Create Account(Platform="Facebook", Handle="{PageName} ({PageId})",
     CredentialsEncrypted=encrypt(page_access_token))
   - If page has instagram_business_account:
     Create Account(Platform="Instagram", Handle="@{ig_username}",
       CredentialsEncrypted=encrypt(page_access_token))
```

Page Access Tokens derived from long-lived user tokens are **permanent** (do not expire unless permissions are revoked or the user changes their password).

### 3. Configuration

```env
# .env
META__APPID=your_meta_app_id
META__APPSECRET=your_meta_app_secret
META__REDIRECTURI=https://social.plopsaurus.xyz/oauth/callback/meta
```

### 4. API Response Models

```csharp
public record MetaAccessTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int? ExpiresIn);

public record MetaPageResponse(
    [property: JsonPropertyName("data")] List<MetaPage> Data);

public record MetaPage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("instagram_business_account")] MetaInstagramAccount? InstagramBusinessAccount);

public record MetaInstagramAccount(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("profile_picture_url")] string? ProfilePictureUrl);
```

---

## Tasks

### Phase 1: Backend Services
- [ ] Create `MetaOptions.cs` (`AppId`, `AppSecret`, `RedirectUri`).
- [ ] Create `MetaOAuthService.cs` (code exchange, long-lived token elevation, `/me/accounts` discovery).
- [ ] Create response models (`MetaAccessTokenResponse`, `MetaPageResponse`, etc.).
- [ ] Register DI services in `PublishingExtensions.cs`.

### Phase 2: Backend Endpoints
- [ ] Add `GET /api/accounts/meta/auth-url` to `AccountsEndpoint.cs`.
- [ ] Add `POST /api/accounts/meta/callback` to `AccountsEndpoint.cs`.
- [ ] On callback: discover all Pages + Instagram accounts, upsert `Account` rows.

### Phase 3: Frontend Integration
- [ ] Add `getMetaAuthUrl()` and `submitMetaCallback(code)` to `web/src/api/accounts.ts`.
- [ ] Create `MetaCallback.tsx` to handle OAuth redirect.
- [ ] Update `ConnectionsTab.tsx` with "Connect Meta" button.
- [ ] Register `/oauth/callback/meta` route in `App.tsx`.

### Phase 4: Tests
- [ ] `MetaOAuthServiceTests.cs` — token exchange, elevation, discovery.
- [ ] Account endpoint integration tests for meta auth-url and callback.
- [ ] Frontend component tests.
