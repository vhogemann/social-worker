# Getting Started with social-worker

social-worker is a local-first, Docker-only multi-modal assistant for composing and publishing social media threads.

## Prerequisites

- Docker with Compose v2
- mkcert (for local HTTPS)

## Quick Start

```bash
git clone <repo>
cd social-worker
cp .env.example .env
./scripts/bootstrap.sh
docker compose up --build
```

Then open `https://social-worker.localtest` in your browser.

## App Walkthrough

### 1. Login

### 1. Sign in with your admin credentials

### 2. The sidebar shows all your drafts

### 3. The markdown editor uses CodeMirror 6 with Vim keybindings

### 4. The chat panel lets you collaborate with an AI assistant

### 5. Add reference sources to your draft

### 6. Click 'Adapt' to generate platform-specific versions of your draft

### 7. When your draft is ready, click Publish

### 8. Open Settings to configure LLM providers (OpenAI, OpenRouter, Ollama), connect social media accounts, and manage brand voice prompts

---

### Step 1: Login

Sign in with your admin credentials. The app uses JWT authentication with refresh tokens.

![01-login](e2e/screenshots/01-login.png)


### Step 2: Draft List

The sidebar shows all your drafts. Click a draft to open it in the editor. Use the + button to create new drafts.

![02-draft-list](e2e/screenshots/02-draft-list.png)


### Step 3: Editor

The markdown editor uses CodeMirror 6 with Vim keybindings. Write your thread content here. Use `---` on its own line to separate thread segments.

![03-editor](e2e/screenshots/03-editor.png)


### Step 4: Chat

The chat panel lets you collaborate with an AI assistant. Ask for improvements, rewrites, or new ideas. The assistant can read your draft and suggest changes.

![04-chat](e2e/screenshots/04-chat.png)


### Step 5: Sources

Add reference sources to your draft. Paste URLs or upload files, and the assistant can reference them when generating content.

![05-sources](e2e/screenshots/05-sources.png)


### Step 6: Adapt Modal

Click 'Adapt' to generate platform-specific versions of your draft. The assistant tailors content for each platform's constraints (character limits, tone, format).

![06-adapt-modal](e2e/screenshots/06-adapt-modal.png)


### Step 7: Publish

When your draft is ready, click Publish. The app posts to your connected Bluesky account. Publishing to Twitter, LinkedIn, Facebook, and Instagram is coming soon.

![07-publish](e2e/screenshots/07-publish.png)


### Step 8: Settings

Open Settings to configure LLM providers (OpenAI, OpenRouter, Ollama), connect social media accounts, and manage brand voice prompts.

![08-settings](e2e/screenshots/08-settings.png)


## Running E2E Tests

```bash
# Run the full E2E suite
docker compose --profile e2e run --rm e2e npx playwright test

# Generate this guide
docker compose --profile e2e run --rm e2e npx playwright test tests/generate-getting-started.spec.ts
```

## Regenerating This Guide

Run the `generate-getting-started` test after any UI changes that affect the screenshots:

```bash
docker compose --profile e2e build e2e
docker compose --profile e2e run --rm e2e npx playwright test tests/generate-getting-started.spec.ts
cp e2e/output/GETTING_STARTED.md GETTING_STARTED.md
```
