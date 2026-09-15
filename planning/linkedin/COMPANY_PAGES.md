# LinkedIn Dual-App Support (Personal Profiles + Company Pages)

## Context & Objectives

LinkedIn enforces a strict policy where the **Community Management API** (which grants `w_organization_social` and `r_organization_admin` for company page management) cannot co-exist with member products (*Sign In with LinkedIn*, *Share on LinkedIn*) within the same developer application.

To support both publishing flows without compromising security or flexibility, `social-worker` supports **Dual-App LinkedIn Configuration**:
1. **Personal App**: Member sharing app using `w_member_social`, `openid`, `profile`.
2. **Company App**: Dedicated Community Management API app using `w_organization_social`, `r_organization_admin`.

Users can configure one or both in their environment variables and choose which type of account to connect in the UI.

---

## Architectural Design

### 1. Configuration & Options
- Standard keys for personal member app:
  `LINKEDIN__CLIENTID`, `LINKEDIN__CLIENTSECRET`, `LINKEDIN__REDIRECTURI`, `LINKEDIN__SCOPES`
- Company-specific keys:
  `LINKEDIN__COMPANY__CLIENTID`, `LINKEDIN__COMPANY__CLIENTSECRET`, `LINKEDIN__COMPANY__REDIRECTURI`, `LINKEDIN__COMPANY__SCOPES`
  (falls back to standard keys if company-specific ones are omitted).

### 2. Authorization & State Parameter
- When requesting authorization URL:
  `GET /api/accounts/linkedin/auth-url?type=personal|company`
  Appends `&state=personal` or `&state=company` to LinkedIn authorization redirect.
- When redirecting back to `/oauth/callback/linkedin?code=...&state=...`:
  Frontend passes `code` and `accountType: state` to `POST /api/accounts/linkedin/callback`.

### 3. Graceful Scope & Endpoint Handling
- Dedicated company apps do not have `openid` or `profile` scopes:
  `FetchUserInfoAsync` handles 403 Forbidden gracefully without aborting the flow.
- Organization discovery queries `/v2/organizationalEntityAcls?q=roleAssignee` and `/v2/organizations/{id}`, persisting administered company pages.

---

## Implementation Checklist

### Backend
- [x] Add `CompanyClientId`, `CompanyClientSecret`, `CompanyRedirectUri`, `CompanyScopes` to `LinkedInOptions.cs`.
- [x] Support `isCompany` in `LinkedInOAuthService.ExchangeCodeAsync`.
- [x] Make `LinkedInOAuthService.FetchUserInfoAsync` return `null` on 403 rather than throwing.
- [x] Update `AccountsEndpoint.cs` auth-url and callback handlers for `personal` vs `company`.
- [x] Add `LINKEDIN__COMPANY__*` to `docker-compose.yml` and `.env.example`.
- [x] Unit tests in `SocialWorker.Api.Tests`.

### Frontend
- [x] Update `web/src/api/accounts.ts` to accept `type` in `getLinkedInAuthUrl` and `accountType` in `submitLinkedInCallback`.
- [x] Update `ConnectionsTab.tsx` with Personal vs Company Page selector modal.
- [x] Update `LinkedInCallbackPage.tsx` to read `state` parameter and forward to callback.
- [x] Frontend unit tests in `web/src/`.
