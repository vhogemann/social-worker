# Getting Started with social-worker

social-worker is a local-first, Docker-only assistant for drafting and publishing social media threads.

## Prerequisites

- Docker with Compose v2

## Quick Start

```bash
git clone <repo>
cd social-worker
cp .env.example .env
docker compose up --build
```

Then open `http://localhost:8100` in your browser.

## Quick Tour

1. **Workspace Overview**: After signing in, you land in the main workspace with drafts, editor, preview controls, and chat side by side.
2. **Create Draft Modal**: Use the drafts sidebar to create a new draft and choose the target platform up front.
3. **Editor**: Write thread content in the markdown editor. Separate posts with `---` on its own line and keep refining in place.
4. **Chat Assistant**: Use chat to ask for rewrites, improvements, and idea generation based on your current draft.
5. **Source Preview**: Open Sources to attach reference material, then preview extracted contents without leaving the editor.
6. **Adapt Modal**: Click Adapt to generate platform-specific variants of your thread content.
7. **Preview Mode**: Switch to Preview mode to inspect how the thread will read as a sequence of social posts before publishing.
8. **Settings**: Open Settings to manage providers, connected accounts, and brand voice configuration from one place.

## Step-by-Step Walkthrough

### Step 1: Workspace Overview

After signing in, you land in the main workspace with drafts, editor, preview controls, and chat side by side.

![01-login](docs/getting-started/01-login.png)


### Step 2: Create Draft Modal

Use the drafts sidebar to create a new draft and choose the target platform up front.

![02-draft-list](docs/getting-started/02-draft-list.png)


### Step 3: Editor

Write thread content in the markdown editor. Separate posts with `---` on its own line and keep refining in place.

![03-editor](docs/getting-started/03-editor.png)


### Step 4: Chat Assistant

Use chat to ask for rewrites, improvements, and idea generation based on your current draft.

![04-chat](docs/getting-started/04-chat.png)


### Step 5: Source Preview

Open Sources to attach reference material, then preview extracted contents without leaving the editor.

![05-sources](docs/getting-started/05-sources.png)


### Step 6: Adapt Modal

Click Adapt to generate platform-specific variants of your thread content.

![06-adapt-modal](docs/getting-started/06-adapt-modal.png)


### Step 7: Preview Mode

Switch to Preview mode to inspect how the thread will read as a sequence of social posts before publishing.

![07-publish](docs/getting-started/07-publish.png)


### Step 8: Settings

Open Settings to manage providers, connected accounts, and brand voice configuration from one place.

![08-settings](docs/getting-started/08-settings.png)


## Running E2E Tests

```bash
# Run the full E2E suite
docker compose --profile e2e run --rm e2e npx playwright test

# Generate this guide
docker compose --profile e2e run --rm e2e npx playwright test tests/generate-getting-started.spec.ts
```

## Regenerating This Guide

Run the helper script to regenerate both this markdown file and the committed screenshots:

```bash
./scripts/regenerate-getting-started.sh
```
