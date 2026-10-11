# Instagram Profile Sources Setup Guide (Option A: Meta Business Discovery API)

This guide walks you through setting up and using Instagram profiles as automated content sources in **social-worker**.

---

## 1. Overview & Architecture

Instagram does not offer open, public RSS feeds and blocks web scraping. `social-worker` uses Meta's official **Instagram Graph API — Business Discovery** to query public posts, captions, permalinks, and media from any public Instagram Creator or Business account.

* **Cost**: **$0.00 (100% Free)**. Meta charges nothing for standard Graph API calls.
* **Rate Limits**: 200 calls/hour per app user (easily supports 15–30 tracked accounts on a 30-minute polling cycle).
* **Target Coverage**: 100% of politicians (e.g. `@jairmessiasbolsonaro`, `@flaviobolsonaro`, `@lulaoficial`), media outlets (`@cartacapital`, `@choquei`), and influencers operate Professional accounts and are fully accessible.
* **Format Compatibility**: Ingests into `social-worker` as standard feed subscriptions (`instagram://username`), applying keyword filters (`includeFilters` / `excludeFilters`) directly to post captions.

---

## 2. Prerequisites

### Step 2.1: Convert Your Instagram Account to a Creator Account
Meta requires the *caller* to have an Instagram Professional (Creator or Business) account:
1. Open the **Instagram app** on your phone.
2. Go to your profile → **Settings & Privacy**.
3. Scroll to **Account type and tools** → tap **Switch to professional account**.
4. Select **Creator** (or Business) and complete the prompt (free, takes ~10 seconds).
5. *(Optional)* Link your Instagram profile to a Facebook Page if prompted.

### Step 2.2: Meta Developer App Setup
You can use the **exact same Meta App** you already configured for Threads!
1. Go to the [Meta Developer Dashboard](https://developers.facebook.com/apps/).
2. Select your existing App (or create a new "Business" or "Consumer" app).
3. In the left sidebar under **Add Products**, add:
   * **Instagram Graph API**
   * **Facebook Login for Business** (or Facebook Login)
4. Under **Facebook Login → Settings**, ensure the **Valid OAuth Redirect URIs** includes:
   ```
   https://social.plopsaurus.xyz/oauth/callback/instagram
   ```
   *(or `http://localhost:8100/oauth/callback/instagram` for local development)*.
5. In App Settings → Basic, copy your **App ID** and **App Secret**.

---

## 3. Connecting Your Account in social-worker

You have three simple ways to connect:

### Method A: One-Click OAuth Connect (Recommended)
1. Open `social-worker` at `https://social.plopsaurus.xyz` (or `http://localhost:8100`).
2. Go to **Settings → Connections** (`/settings?tab=connections`).
3. Click **Add Connection** and select **Instagram** from the platform dropdown.
4. Click **Connect Instagram via Meta**.
5. Log into Facebook/Meta and approve permissions (`instagram_basic`, `pages_show_list`).
6. You are redirected back to `social-worker` with your account verified and saved!

### Method B: Direct Token Paste
If you generated an access token via Meta Graph API Explorer or a System User:
1. In **Settings → Connections → Add Connection**, select **Instagram**.
2. Enter your handle (e.g. `@vhogemann`).
3. Paste your **Access Token** into the token field.
4. Click **Save Instagram Token**.
5. `social-worker` will automatically contact Meta to resolve your Instagram Business Account ID (`externalId`) and store it securely encrypted in the database.

### Method C: Environment Variables (`.env`)
You can configure Instagram credentials directly in your `.env` file:
```env
# Meta / Instagram Graph API Configuration
INSTAGRAM__APPID=your_meta_app_id
INSTAGRAM__APPSECRET=your_meta_app_secret
INSTAGRAM__REDIRECTURI=https://social.plopsaurus.xyz/oauth/callback/instagram

# Direct Token & Caller Account ID (Optional override)
INSTAGRAM__ACCESSTOKEN=your_meta_access_token
INSTAGRAM__CALLERUSERID=your_instagram_business_account_id
```

> [!NOTE]
> If `INSTAGRAM__APPID` is left empty, `social-worker` automatically falls back to your existing `METATHREADS__APPID` and `METATHREADS__APPSECRET`.

---

## 4. Subscribing to Instagram Profiles

Once your account is connected, Instagram profiles can be added just like any RSS or YouTube feed.

### Supported URL Formats:
* `https://www.instagram.com/flaviobolsonaro`
* `https://instagram.com/flaviobolsonaro/`
* `instagram://flaviobolsonaro`
* `@flaviobolsonaro` (via `discover_feed`)

### Adding via the UI
1. Go to **Settings → Feeds**.
2. Click **Add Feed**.
3. In **Feed URL**, enter `https://www.instagram.com/flaviobolsonaro` (or `instagram://flaviobolsonaro`).
4. Set Title: `Flávio Bolsonaro (Instagram)`.
5. *(Optional)* Add **Include Filters** (e.g. `pec,senado,governo,lula`) to only process relevant posts.
6. Click **Save**.

### Adding via MCP Tools
You can use `discover_feed` and `add_feed`:

```json
// Tool: discover_feed
{
  "url": "https://www.instagram.com/flaviobolsonaro"
}
// Returns:
// feedUrl: "instagram://flaviobolsonaro"
// title: "Flávio Bolsonaro (@flaviobolsonaro) (Instagram)"
// websiteUrl: "https://www.instagram.com/flaviobolsonaro"
```

```json
// Tool: add_feed
{
  "title": "Flávio Bolsonaro",
  "feedUrl": "instagram://flaviobolsonaro",
  "websiteUrl": "https://www.instagram.com/flaviobolsonaro",
  "includeFilters": "pec,senado,segurança,eleição,stf",
  "excludeFilters": "aniversário,parabéns,família"
}
```

---

## 5. Connecting to Workflows

To automatically generate and publish thread counter-attacks whenever a monitored politician or creator posts:

1. In **Workflows**, open or create a workflow (e.g. *Fura-Bolha & Vira-Voto* or *Flame-Bait*).
2. Link the Instagram feed subscription as a **Source**.
3. Ensure the workflow prompt instructs the LLM on how to react (e.g. rebuttal, counter-narrative, Brazilian Portuguese tone).
4. When `social-worker` polls the Instagram profile:
   * It extracts the post caption, media, and permalink.
   * It checks your `includeFilters` and `excludeFilters`.
   * It creates a draft, attaches the Instagram source (without failing on web scraping blocks), and generates a multi-post thread response ready to publish to Meta Threads or Bluesky.

---

## 6. Troubleshooting

* **"Tried accessing nonexisting field (business_discovery) on node type (User)"**:
  * The target profile is a personal account rather than a Creator or Business account. Almost all public figures, politicians, and media are Creator/Business accounts.
* **"No Instagram Professional/Business account found linked to this Meta account"**:
  * Your caller account has not been switched to Professional/Creator yet. Follow Step 2.1 in the Instagram mobile app.
* **Rate Limits**:
  * Meta allows 200 calls per hour. `social-worker` caches calls and defaults to 10 posts per query to stay comfortably under limits.
