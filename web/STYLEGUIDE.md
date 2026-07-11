# social-worker UI Styleguide

## API Calls

**Always import and use the typed functions in `src/api/` — never raw `fetch`.**

```tsx
// ✅ Correct
import { generateVariants } from "../../api/drafts";
const data = await generateVariants(draftId, selected);

// ❌ Wrong — no auth, wrong token key, no refresh handling
const token = localStorage.getItem("auth_token");
const res = await fetch(`/api/drafts/${draftId}/generate-variants`, {
  headers: { Authorization: `Bearer ${token}` },
});
```

All API functions in `src/api/*.ts` use `apiFetch` from `src/api/client.ts`, which:
- Reads the access token from Zustand store (persisted as `sw_access_token`)
- Injects `Authorization: Bearer` automatically
- Retries with refresh token on 401
- Sets `Content-Type: application/json` unless body is `FormData`

### Adding a new API call

1. Add the function to the appropriate file in `src/api/` (or create a new one)
2. Use `apiFetch` — never `fetch` directly
3. Export the types and function
4. Import the function in the component/store, not the raw `apiFetch`

```ts
// src/api/drafts.ts
export async function generateVariants(draftId: string, platforms: string[]): Promise<DraftFamilyDto> {
  const res = await apiFetch(`/api/drafts/${draftId}/generate-variants`, {
    method: "POST",
    body: JSON.stringify({ platforms }),
  });
  if (!res.ok) throw new Error(`generateVariants failed: ${res.status}`);
  return res.json();
}
```

Exception: login/register endpoints (`/api/auth/login`, `/api/auth/refresh`) use raw `fetch` because they run before an auth token exists.

## State Management (Zustand)

- All server state goes through the Zustand store, not local `useState`
- Store actions call API functions and update local state
- Import API functions at the top, call them inside store actions
- Update local state optimistically where it makes sense

```ts
// src/store/draftStore.ts
import { generateVariants } from "../api/drafts";

// Store action:
generateVariantsAction: async (draftId, platforms) => {
  const data = await generateVariants(draftId, platforms);
  set((s) => ({ drafts: refreshDrafts(s.drafts, data) }));
},
```

## Types

- Hand-sync frontend `interface` types in `src/api/*.ts` with backend C# records
- Types are exported from the API module where they're used
- Keep the field names, types, and nullability in sync with the backend
- When the API contract changes, update both TypeScript types and the API function

```ts
export interface DraftDto {
  id: string;
  title: string;
  targetPlatform: string | null;  // string — sent as JSON from backend
  canonicalDraftId: string | null;
  // ...
}
```

## Components

- One file per component, PascalCase
- Place in `src/components/<Area>/<Component>.tsx`
- Side-effect-free components get props; stateful components read from stores
- Use Tailwind utility classes, no CSS modules or styled-components
- No emojis in code or UI unless explicitly requested

## New files checklist

When adding a frontend feature:

1. Add types + API function in `src/api/<area>.ts` using `apiFetch`
2. Add store action in `src/store/<store>.ts` calling the API function
3. Create component in `src/components/<Area>/` reading from store
4. Never use raw `fetch` or `localStorage.getItem` for auth tokens