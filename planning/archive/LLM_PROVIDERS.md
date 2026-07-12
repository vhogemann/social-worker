# Configurable LLM Providers

Admin-configurable LLM/AI providers through the Settings UI. Admin can add multiple providers (OpenRouter, Ollama), set one as default. Each user picks their preferred provider from the active list. Chat backend resolves the provider at request time instead of reading static env vars.

## Design decisions

- **Provider types**: OpenRouter and Ollama for v1 (both use OpenAI-compatible chat/completions API)
- **API keys**: write-only — stored server-side, never returned to the frontend; admin enters a new key to replace
- **One model per provider**: each provider stores a single model string (e.g. `anthropic/claude-3.5-sonnet`); multi-model is a follow-up
- **System default**: exactly one provider is marked `IsDefault`; new users and users without a preference use it
- **User preference**: `AppUser.PreferredProviderId` FK; `null` means "use system default"
- **Fallback**: if a user's selected provider is deleted or deactivated, chat silently falls back to the system default
- **Seed from env**: on first startup, if no providers exist in DB, one is created from `LLM__*` env vars and marked as default
- **Ollama convenience**: when provider type is Ollama, UI auto-fills base URL and hides the API key field
- **Connection Testing**: dry-run connection test endpoint to send verification request to the configured Base URL and credentials
- **Model Autocomplete**: UI suggested common models dropdown lists based on selected provider type

## API contract

```
GET    /api/providers              → LlmProviderDto[]       admin only
POST   /api/providers              → LlmProviderDto         admin only
PATCH  /api/providers/{id}         → LlmProviderDto         admin only
DELETE /api/providers/{id}         → 204                    admin only (cannot delete the default)
POST   /api/providers/test         → { success, error }     admin only (test LLM connectivity)

GET    /api/providers/available    → { id, name, providerType, model }[]   any authenticated user

PATCH  /api/account/provider       → 204   body: { providerId: guid | null }
GET    /api/auth/me                → adds preferredProviderId to response
```

## Data model

```
LlmProvider {
  Id            Guid        PK
  Name          string(100) unique
  ProviderType  string(50)  "OpenRouter" | "Ollama"
  BaseUrl       string(500)
  ApiKey         string(500) server-only, never sent to frontend
  Model         string(200)
  IsDefault     bool        exactly one true
  IsActive      bool
  CreatedAt     DateTime
  UpdatedAt     DateTime
}

AppUser (existing) {
  + PreferredProviderId  Guid?  FK → LlmProvider, nullable
}
```

---

## Implementation progress

### Backend — Data

- [x] `Data/Entities/LlmProvider.cs` — entity class
- [x] `Data/Entities/AppUser.cs` — add `PreferredProviderId (Guid?)` FK
- [x] `Data/AppDbContext.cs` — add `DbSet<LlmProvider>`, configure entity and FK
- [x] Migration `AddLlmProviders` — create `LlmProviders` table; add `PreferredProviderId` column to `AppUsers`

### Backend — Seed

- [x] `Program.cs` — after admin seed, if `LlmProviders` is empty, create one provider from `LLM__*` env vars with `IsDefault = true`

### Backend — Features/Providers (admin)

- [x] `Features/Providers/Models.cs` — DTOs and request records
- [x] `Features/Providers/ProvidersEndpoint.cs` — `GET/POST/PATCH/DELETE /api/providers` (admin), `GET /api/providers/available` (authenticated)

### Backend — User preference

- [x] `Features/Users/AccountEndpoint.cs` — `PATCH /api/account/provider` endpoint
- [x] `Features/Auth/AuthEndpoint.cs` — include `preferredProviderId` in `GET /api/auth/me`

### Backend — ChatService

- [x] `Features/Chat/ChatService.cs` — resolve provider from user preference or default at request time; pass `baseUrl`/`apiKey`/`model` to `CallOpenAiAsync` instead of reading `_opts`
- [x] Remove `IOptions<LlmOptions>` dependency from `ChatService` constructor (keep `LlmOptions` for seed only)

### Frontend — API

- [x] `api/providers.ts` — `listProviders`, `listAvailableProviders`, `createProvider`, `updateProvider`, `deleteProvider`
- [x] `api/auth.ts` — update `UserDto` with `preferredProviderId`; add `setPreferredProvider`

### Frontend — Settings UI

- [x] `components/Settings/ProvidersTab.tsx` — admin-only provider management table (CRUD, set default, toggle active)
- [x] `components/Settings/AccountTab.tsx` — add "Preferred AI Provider" dropdown
- [x] `components/Settings/SettingsModal.tsx` — add "providers" tab (admin only)

---

## Verification checklist

- [x] `docker compose up --build` — migration runs, seed creates one provider from `LLM__*` env vars
- [x] Login as admin → Settings → Providers tab → seeded provider visible, marked as default
- [x] Add a second provider (Ollama) → appears in list, base URL auto-filled
- [x] Set new provider as default → previous default cleared
- [x] Login as regular user → Settings → Account tab → "Preferred AI Provider" dropdown shows active providers
- [x] Select a non-default provider → chat uses that provider's config
- [x] Admin deactivates user's preferred provider → chat silently falls back to default
- [x] Admin deletes a non-default provider → user preferences cleared
- [x] `curl /api/providers` without admin token → 403
- [x] `curl /api/providers/available` with regular token → returns active providers (no API keys)
