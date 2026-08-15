# Implementation Plan: LinkedIn OAuth Integration [COMPLETE]

Implement a standard 3-legged OAuth 2.0 authorization code flow for connecting LinkedIn accounts. This replaces the manual entry of Member URNs and access tokens with a secure, browser-based authorization redirection.

## Goals
1. Configure LinkedIn App Client ID, Client Secret, and Redirect URI in `.env` and `appsettings.json` backend configurations.
2. Implement a backend API endpoint `POST /api/accounts/linkedin/callback` that exchanges an authorization code for an access token, fetches the user's Member URN, and saves the connected account.
3. Add a frontend routing handler for `/oauth/callback/linkedin` that captures the code from the query parameters, communicates with the backend, and redirects back to Settings.
4. Update the Connections settings tab to render a "Connect LinkedIn" button that redirects the user to LinkedIn's authorization page.

## User Review Required
> [!IMPORTANT]
> **LinkedIn App Credentials**: To run this flow, the user must define the following variables in their local `.env` file:
> ```env
> LINKEDIN__CLIENTID=your_linkedin_client_id
> LINKEDIN__CLIENTSECRET=your_linkedin_client_secret
> LINKEDIN__REDIRECTURI=http://localhost:8100/oauth/callback/linkedin
> ```
> We will fallback to stub/manual mode or show a configuration error if these credentials are not defined in the environment.

> [!NOTE]
> **API Permissions**: The LinkedIn App must be configured on the LinkedIn Developer Portal with the `w_member_social` and `openid profile` scopes enabled, and `http://localhost:8100/oauth/callback/linkedin` added to the Authorized Redirect URIs.

## Proposed Changes

---

### Backend Components (`api/SocialWorker.Api/`)

#### [MODIFY] `api/SocialWorker.Api/appsettings.json` (or Program.cs config binding)
Bind the `LinkedIn` configuration section to typed options:
```csharp
public class LinkedInOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}
```

#### [NEW] `api/SocialWorker.Api/Features/Accounts/LinkedInOAuthService.cs`
Create a helper service to manage the OAuth exchange:
* `ExchangeCodeForTokenAsync(string code, CancellationToken ct)`: POSTs to `https://www.linkedin.com/oauth/v2/accessToken` to get the access token.
* `FetchMemberProfileAsync(string token, CancellationToken ct)`: GETs `https://api.linkedin.com/v2/userinfo` to retrieve the Member URN (`sub` or `id`) and display name.

#### [MODIFY] `api/SocialWorker.Api/Features/Accounts/AccountsEndpoint.cs`
Add a new endpoint:
* `POST /api/accounts/linkedin/callback`
  * Accepts `{ string code }`.
  * Invokes `LinkedInOAuthService` to exchange the code and fetch the profile.
  * Encrypts the resulting access token and saves the account with URN as the `Handle`.

---

### Frontend Components (`web/src/`)

#### [NEW] `web/src/components/Settings/LinkedInCallback.tsx`
Create a page component to handle the redirection callback:
* Mounted at route `/oauth/callback/linkedin`.
* Extracts the `code` and `state` parameters from the URL query string.
* Displays a loading spinner.
* Sends a POST request to `/api/accounts/linkedin/callback`.
* On success, updates the draft store/accounts list and redirects to `/settings` with a success notice.

#### [MODIFY] `web/src/App.tsx`
Add the route for the callback page:
```tsx
<Route path="/oauth/callback/linkedin" element={<LinkedInCallback />} />
```

#### [MODIFY] `web/src/components/Settings/ConnectionsTab.tsx`
* Replace the manual inputs for LinkedIn with a "Connect to LinkedIn" button.
* When clicked, redirects the browser to:
  `https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id={clientId}&redirect_uri={redirectUri}&scope=w_member_social%20openid%20profile`
* (We can fetch the authorization URL or client ID from a backend endpoint `GET /api/accounts/linkedin/auth-url`).

## Verification Plan

### Automated Tests
* Create unit tests under `api/SocialWorker.Api.Tests/Features/Accounts/LinkedInOAuthServiceTests.cs` to test code-exchange failure/success paths with mocked HTTP responses.
* Create unit tests for the `/api/accounts/linkedin/callback` endpoint in `api/SocialWorker.Api.Tests/Features/Accounts/AccountsEndpointTests.cs`.

Run:
```bash
docker compose --profile tooling run --rm dotnet test ../SocialWorker.Api.Tests/SocialWorker.Api.Tests.csproj
```

### Manual Verification
1. Add mock/real LinkedIn App credentials to `.env`.
2. Click **Connect LinkedIn** in Settings.
3. Complete the authorization flow on LinkedIn's sandbox or live page.
4. Verify the redirect lands on the callback page, registers the account successfully, and stores the correct Member URN and encrypted token.
