# Authentication

Email/username/password authentication for social-worker. A seeded admin account is created from `.env`. Two new screens: **Login** and **Settings** (Account + Users tabs). All API endpoints require a valid JWT.

## Design decisions

- **Access token**: signed JWT, 1-hour lifetime, HMAC-SHA256
- **Refresh token**: opaque random string, stored server-side in DB, **7-day sliding expiry** (reset on each use — 7 days of inactivity = automatic logout)
- **Password hashing**: BCrypt (`BCrypt.Net-Next`)
- **Login**: email OR username accepted
- **Account creation**: admin-only (no self-registration)
- **Draft ownership**: each draft is scoped to the user who created it
- **Admin seeding**: on first startup, if no users exist, create `admin` using `Auth__AdminPassword` from env
- **Deactivation**: admin can deactivate users; deactivation immediately revokes all their refresh tokens

## API contract

```
POST /api/auth/login    → { accessToken, refreshToken, expiresAt, user: { id, username, email, role } }
POST /api/auth/refresh  → { accessToken, expiresAt }          body: { refreshToken }
POST /api/auth/logout   → 204                                  body: { refreshToken }
GET  /api/auth/me       → { id, username, email, role }        requires access token

GET    /api/users                    → list all users           admin only
POST   /api/users                    → create user              admin only
PATCH  /api/users/{id}               → update user              admin only
POST   /api/users/{id}/password      → reset user password      admin only

PATCH  /api/account/password         → change own password      any authenticated user
```

## Environment variables

```
Auth__JwtSecret=<random 32-char hex>        # HMAC-SHA256 signing key
Auth__AdminPassword=changeme                 # initial admin password (seed only)
Auth__AccessTokenLifetimeMinutes=60
Auth__RefreshTokenLifetimeDays=7
```

---

## Implementation progress

### Backend — Infrastructure & Auth

- [x] `Infrastructure/Auth/AuthOptions.cs` — config binding (`JwtSecret`, `AccessTokenLifetimeMinutes`, `RefreshTokenLifetimeDays`, `AdminPassword`)
- [x] `SocialWorker.Api.csproj` — add `BCrypt.Net-Next`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `System.IdentityModel.Tokens.Jwt`
- [x] `appsettings.json` — add `Auth` section with placeholder defaults
- [x] `.env.example` — add `Auth__JwtSecret`, `Auth__AdminPassword`
- [x] `docker-compose.yml` — add `Auth__*` env vars to `api` service

### Backend — Data

- [x] `Data/Entities/AppUser.cs` — `Id, Username, Email, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt`
- [x] `Data/Entities/RefreshToken.cs` — `Id, UserId, Token, CreatedAt, LastUsedAt, ExpiresAt`
- [x] `Data/Entities/Draft.cs` — add `UserId (Guid)` FK → `AppUser`
- [x] `Data/AppDbContext.cs` — add `Users`, `RefreshTokens` DbSets; configure FKs and unique indexes
- [x] Migration `AddAppUsers` — create `AppUsers` + `RefreshTokens` tables; backfill `Drafts.UserId` to admin; add FK constraint

### Backend — Program.cs

- [x] Register JWT bearer auth + bind `AuthOptions`
- [x] Admin seed (runs after `MigrateAsync`)
- [x] `app.UseAuthentication()` + `app.UseAuthorization()`
- [x] Apply `.RequireAuthorization()` to all existing route groups
- [x] Register `"Admin"` policy

### Backend — Features/Auth

- [x] `Features/Auth/Models.cs` — request/response records
- [x] `Features/Auth/AuthService.cs` — `Login`, `Refresh`, `Logout`, `CreateAccessToken`, `CreateRefreshToken`
- [x] `Features/Auth/AuthEndpoint.cs` — `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `GET /api/auth/me`

### Backend — Features/Users

- [x] `Features/Users/Models.cs` — `CreateUserRequest`, `UpdateUserRequest`, `ChangePasswordRequest`, `ResetPasswordRequest`
- [x] `Features/Users/UsersEndpoint.cs` — admin CRUD (`GET /api/users`, `POST /api/users`, `PATCH /api/users/{id}`, `POST /api/users/{id}/password`)
- [x] `Features/Users/AccountEndpoint.cs` — `PATCH /api/account/password`

### Backend — Scoped drafts & chat

- [x] `Features/Drafts/DraftsEndpoint.cs` — filter all operations by `currentUserId` from JWT
- [x] `Features/Chat/ChatService.cs` — verify `draft.UserId == currentUserId`; receive `userId` from endpoint context

### Frontend — Auth state & API client

- [x] `store/authStore.ts` — zustand: `{ user, accessToken, refreshToken, isAuthenticated, login(), logout() }`; localStorage persistence; init-time token validation + silent refresh
- [x] `api/client.ts` — `apiFetch` helper with `Authorization` header; 401 → silent refresh → retry; second 401 → `logout()`
- [x] `api/auth.ts` — `login`, `refresh`, `logout`, `getMe`, `changePassword`, `listUsers`, `createUser`, `updateUser`
- [x] `api/drafts.ts` — switch to `apiFetch`
- [x] `api/chat.tsx` — attach `Authorization` header to SSE request body / headers

### Frontend — Login screen

- [x] `components/Login/LoginPage.tsx` — full-screen login form (email or username + password, show/hide toggle, loading state, error message)
- [x] `main.tsx` — wrap `<App>` with `<AuthGuard>`; render `<LoginPage>` when not authenticated

### Frontend — Settings screen

- [x] `components/Settings/SettingsModal.tsx` — full-screen modal; two tabs (Account, Users)
- [x] `components/Settings/AccountTab.tsx` — display username + email; change password form
- [x] `components/Settings/UsersTab.tsx` — user table; add user form; toggle active/inactive; reset password (admin only)
- [x] `components/DraftList/DraftList.tsx` — add gear icon button that opens `<SettingsModal>`

---

## Verification checklist

- [x] `docker compose up --build` — migration runs, admin seed creates one user
- [x] `http://localhost:8100` → login page shown when unauthenticated
- [x] Login with `admin` / `Auth__AdminPassword` → main app loads, both tokens in `localStorage`
- [x] Page reload → stays logged in (access token valid, `/api/auth/me` succeeds)
- [x] Simulate expired access token → silent refresh fires, no visible interruption
- [x] Settings → Account tab → change password → login with new password works
- [x] Settings → Users tab → create user → login as that user → Users tab hidden; only own drafts visible
- [x] Admin deactivates user → refresh token revoked → user logged out on next request
- [x] Logout → refresh token deleted server-side → login screen shown
- [x] `curl http://localhost:8101/api/drafts` without token → 401
