# Architectural Evaluation: GraphQL in social-worker

This document analyzes the existing API surface in `SocialWorker.Api` and evaluates the architectural trade-offs of introducing a GraphQL endpoint.

---

## 1. Core Architectural Principle: Manually Curated Layering

Passing raw `IQueryable` / SQL projections straight from client GraphQL queries into the database creates severe architectural problems:
- **Domain Leakage**: It exposes raw table schemas directly to the client, violating domain encapsulation.
- **Unpredictable Query Plans**: Arbitrary nested selections can generate unintended Cartesian products and pathological SQL query plans.
- **Bypassed Domain Invariants**: Bypasses business validation and domain service boundaries.

**Rule for `social-worker`**: If GraphQL is ever implemented in the future, the translation between layers will be **manually curated**:
```
Client Query 
    ──▶ GraphQL Schema / Types 
    ──▶ Resolvers & DataLoaders 
    ──▶ Domain Services / Curated DTOs 
    ──▶ EF Core / Repositories 
    ──▶ Postgres
```

---

## 2. Trade-off Analysis with Curated Layers

When GraphQL is built properly with curated layers, the trade-offs shift as follows:

### What Improves:
1. **Strict Encapsulation**: Domain models and sensitive data (e.g., encrypted account tokens, internal queue attempt counters) remain completely protected behind curated GraphQL types.
2. **Explicit Data Fetching**: Resolvers and batch DataLoaders control exactly how and when queries are executed, avoiding surprise N+1 or Cartesian queries.
3. **Client Flexibility**: The frontend can still compose requests for dashboard views in a single query while the backend enforces safe retrieval patterns.

### The Cost / Friction:
1. **High Boilerplate & Maintenance**: Every domain entity requires:
   - GraphQL Type definition
   - Resolver & DataLoader classes for relations
   - Mapping logic from Domain/DTO to GraphQL Object
   - Input type definitions & custom validation
2. **Dual Maintenance**: Because Chat (SSE streaming token-by-token), Media (multipart uploads and image downloads), SkiaSharp code image rendering, and OAuth redirect flows **cannot** be cleanly handled by GraphQL, the team would maintain both a complete REST API and a complete GraphQL schema.

---

## 3. Comparison Matrix (Curated Minimal API vs. Curated GraphQL)

| Dimension | Curated Minimal API (Current) | Curated GraphQL (Future Consideration) |
|---|---|---|
| **Layering** | Endpoint ──▶ Service ──▶ EF Core ──▶ DB | Schema ──▶ Resolver/DataLoader ──▶ Service ──▶ EF Core ──▶ DB |
| **Domain Boundary** | Explicit (Request/Response DTOs) | Explicit (GraphQL Object Types + Input Types) |
| **Boilerplate** | Low (Minimal API endpoints + static DTOs) | High (Types, Resolvers, DataLoaders, Mutations) |
| **Streaming (SSE Chat)** | Native, clean `IAsyncEnumerable<string>` | Requires separate REST endpoint or WS subscriptions |
| **Binary/Media** | Native HTTP streaming (`image/png`, multipart) | Requires separate REST endpoints |
| **Frontend Fetching** | 2-3 focused REST calls for dashboard | Single compound GraphQL query |

---

## 4. Conclusion & Recommendation

1. **Do not implement GraphQL for v1**:
   - The current Minimal API pattern already gives us clean, manually curated layer boundaries with minimal boilerplate.
   - Adding a curated GraphQL layer alongside REST would substantially increase maintenance overhead without providing proportional value for a local-first application.
2. **Future Re-evaluation Trigger**:
   - Revisit if the frontend evolves to require complex, user-configurable dashboards with deeply nested relations (e.g. cross-platform analytics, calendar scheduling, or team workspaces) where waterfall REST calls become a bottleneck.
