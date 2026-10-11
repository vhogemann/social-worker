# Investigation & Architecture Plan: Instagram Profiles as Sources

## Executive Summary

**Verdict**: **Yes, it is entirely feasible to add Instagram profiles as automated content sources in social-worker.**

Unlike YouTube or traditional blogs, Instagram does not expose open, unauthenticated RSS/Atom feeds and aggressively blocks unauthenticated web scraping. However, two distinct, high-quality architectures can solve this cleanly:

1. **Option A (Recommended for Long-Term Production): Meta Graph API — Business Discovery**
   - **How it works**: Uses the official Meta Graph API `business_discovery` endpoint via an authenticated Instagram Creator/Business account.
   - **Reliability**: **100% stable**, official, zero scraping fragility, zero IP bans or CAPTCHAs.
   - **Fit for Politics & News**: **Exceptional**. 100% of major political figures (e.g. `@jairmessiasbolsonaro`, `@flaviobolsonaro`, `@lulaoficial`, `@erikahilton`), news outlets (`@cartacapital`, `@choquei`, `@midianinja`), and activists operate Instagram Creator or Business accounts, which are fully accessible via Business Discovery.
   - **Requirements**: User connects a free Instagram Professional/Creator account via Meta OAuth (`instagram_basic`).

2. **Option B (Zero-OAuth / Fast Ingestion Bridge): RSSHub Docker Sidecar**
   - **How it works**: Runs `diygod/rsshub` as a lightweight internal container in `docker-compose.yml`. RSSHub converts `https://instagram.com/username` into standard RSS 2.0 (`http://rsshub:1200/instagram/user/username`).
   - **Reliability**: High if provided with an Instagram session cookie in `.env`. Can break if Instagram challenges the session cookie.
   - **Compatibility**: Plugs directly into `social-worker`'s existing `FeedSubscription` engine with **zero backend architecture changes**.

---

## 1. Deep Dive: Option A — Official Meta Business Discovery API

### Overview
Meta provides the **Business Discovery API** as part of the Instagram Graph API. It allows any authenticated Instagram Professional (Creator or Business) account to query public media and profile data from other Instagram Professional accounts.

### Target Endpoint
```http
GET https://graph.facebook.com/v20.0/{ig-user-id}?fields=business_discovery.username({target_username}){id,username,name,profile_picture_url,biography,media.limit(10){id,caption,media_type,media_url,thumbnail_url,permalink,timestamp,like_count,comments_count}}&access_token={access_token}
```

### JSON Response Payload Shape
```json
{
  "business_discovery": {
    "username": "flaviobolsonaro",
    "name": "Flávio Bolsonaro",
    "id": "17841400...",
    "media": {
      "data": [
        {
          "id": "1804291823...",
          "caption": "URGENTE: Veja a verdade sobre a PEC que querem aprovar no Senado...",
          "media_type": "IMAGE",
          "media_url": "https://scontent.cdninstagram.com/v/t51...",
          "permalink": "https://www.instagram.com/p/DBxyz...",
          "timestamp": "2026-10-09T10:15:00+0000"
        }
      ]
    }
  },
  "id": "17841401..."
}
```

### Technical & Operational Assessment

| Dimension | Details | Evaluation |
| :--- | :--- | :--- |
| **Authentication** | Shared Meta OAuth token with `instagram_basic` scope. | Standardized with existing Meta login. |
| **Rate Limits** | Meta allows 200 calls/hour per app user. | Querying 15-20 Instagram sources every 30 minutes consumes ~30-40 calls/hour, well within limits. |
| **Media Types** | Returns `IMAGE`, `VIDEO`, `CAROUSEL_ALBUM`. | Complete caption text and CDN media URLs provided. |
| **Target Restrictions** | Target profile **must be a Business or Creator account**. | All public politicians, media portals, and influencers are Creator/Business accounts. |
| **Maintenance** | Official Graph API with SemVer versioning and deprecation notices. | **Near zero maintenance**. Never breaks on HTML changes. |

---

## 2. Deep Dive: Option B — RSSHub Sidecar Bridge

### Overview
[RSSHub](https://docs.rsshub.app/) is a popular, open-source RSS generator. It has a native Instagram route:
`http://rsshub:1200/instagram/user/:username`

### Docker Architecture
Added to `docker-compose.yml`:
```yaml
  rsshub:
    image: diygod/rsshub:latest
    container_name: social-worker-rsshub
    restart: unless-stopped
    environment:
      NODE_ENV: production
      CACHE_TYPE: memory
      IG_COOKIE: ${INSTAGRAM_SESSION_COOKIE:-}
    networks:
      - internal
```

### Technical & Operational Assessment

| Dimension | Details | Evaluation |
| :--- | :--- | :--- |
| **Integration Complexity** | None. Produces standard RSS 2.0 / Atom. | Fits existing `FeedSubscription` model immediately. |
| **Personal Accounts** | Can scrape non-professional personal accounts. | Broadest target profile support. |
| **Scraping Vulnerability** | Requires `IG_COOKIE` (session cookie) from a browser session. | Cookies expire every 60-90 days; risk of account challenge. |
| **Local-first Footprint** | Adds ~120MB Node.js image to Compose stack. | Lightweight, but adds an extra dependency. |

---

## 3. Architecture Comparison & Trade-Offs

| Criteria | Option A: Meta Business Discovery (Native) | Option B: RSSHub Sidecar (Bridge) | Option C: Headless Browser / Instaloader |
| :--- | :--- | :--- | :--- |
| **Stability** | **Highest** (Official Meta API) | Moderate (Cookie-dependent) | Low (Constant cat-and-mouse) |
| **IP Ban Risk** | **0%** (Authenticated official calls) | Medium if polled aggressively | High on cloud/datacenter IPs |
| **TOS Compliance** | **100% Compliant** | Gray (Web scraping) | Gray (Web scraping) |
| **Target Coverage** | 100% of Politicians, Media, Creators | All Public Accounts | All Public Accounts |
| **Implementation Effort** | ~2-3 days (.NET client + polling service) | ~1 day (Compose entry + feed URL) | ~4-5 days (Microservice + session handling) |

---

## 4. Recommended Implementation Strategy

A **phased hybrid roadmap** delivers immediate value while building the permanent official infrastructure:

### Phase 1: Native Ingestion Client (.NET) via Meta Business Discovery
1. **Extend OAuth Scopes**:
   - Ensure the Meta OAuth app requests `instagram_basic` and `instagram_manage_insights`.
2. **`InstagramBusinessDiscoveryService`**:
   - Queries `GET https://graph.facebook.com/v20.0/{ig_user_id}?fields=business_discovery.username({target})` using the stored Meta Account access token.
   - Extracts post caption, media URLs, permalink, and creation date.
3. **Ingestion & Keyword Filtering**:
   - Passes caption text through `FeedSourceIngestionService.PassesFilters`.
   - Download media if attachments are enabled; store as `MediaAsset`.
   - Creates draft linked to workflow with `SourceKind = SourceKind.Url` (pointing to Instagram permalink) and caption as content source.
4. **Source Registration via MCP & Web UI**:
   - Add MCP tool `add_instagram_source` / `discover_instagram_profile`.
   - Add Instagram option to Feed Subscription modal in UI.

### Phase 2: Optional RSSHub Fallback
- Provide an optional `docker-compose.rsshub.yml` override profile for users who wish to track non-professional personal Instagram accounts using session cookies.

---

## 5. Security, Rate Limiting & Edge Cases

1. **Token Refreshing**:
   - Meta long-lived user tokens last 60 days. The existing `TokenRefreshHostedService` handles periodic refresh.
2. **Rate Limit Throttling**:
   - Implement an in-memory cache with a 15-minute minimum interval per Instagram handle to prevent consuming the 200 calls/hour ceiling.
3. **Carousel & Reel Handling**:
   - Instagram posts can be single images, carousels (up to 10 photos), or video Reels.
   - The ingestion service will capture the caption as the primary text source and download the first 1-4 carousel images for draft attachments.
