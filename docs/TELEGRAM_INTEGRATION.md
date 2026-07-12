# TELEGRAM_INTEGRATION.md -- Telegram Workspace Bridge for Mobile Access

## Overview

A Telegram bot that provides mobile access to the social-worker system. The bot is a **thin client** with no business logic -- every command translates to an HTTP call against the C# API. Messages are stored in Postgres, not Telegram, keeping the Web UI and Telegram UI always in sync.

## Prerequisites

- Phase 4 of [SELF_EVOLVING_ASSISTANT.md](SELF_EVOLVING_ASSISTANT.md) complete: `Workspace` and `WorkspaceMessage` entities exist, workspace chat tools (`list_workspaces`, `read_workspace`, `append_to_workspace`, `create_workspace`) are registered and tested
- A Telegram Bot Token from [@BotFather](https://t.me/botfather)
- `TELEGRAM_BOT_TOKEN` in `.env`

---

## Why Not Native Telegram Forum/Topics for v1

| Factor | Native Forum/Topics | DB-backed (virtual) |
|---|---|---|
| Rate limits | 20 topics/min, topic creation is heavy | None |
| Message persistence | Telegram servers (30-day cache for bots) | Postgres (forever) |
| Cross-platform | Telegram-only | Web UI + Telegram share backend |
| Topic-to-draft mapping | Must sync Telegram topic IDs to DB | Draft IS the workspace |
| Branching/versioning | Impossible | Trivial (entity version stamps) |
| UX for a 4.5B model | LLM must track topic IDs in context | LLM just calls `read_draft(id)` |

The Telegram bot is a **presentation layer** over the virtual workspace model, not the source of truth.

---

## Architecture

```
Telegram user
     │
     │ /chat write a thread about Docker
     ▼
┌──────────────────┐
│  telegram-bot     │  python-telegram-bot v21
│  (thin client)    │  no business logic
└──────┬───────────┘
       │ HTTP POST /api/telegram/command
       ▼
┌──────────────────┐
│  api (.NET)       │  processes command,
│                   │  routes to appropriate
│                   │  service/LLM
└──────┬───────────┘
       │ JSON response
       ▼
┌──────────────────┐
│  telegram-bot     │  formats response,
│                   │  sends to user
└──────────────────┘
```

---

## Bot Commands

| Command | Action | C# API Endpoint |
|---|---|---|
| `/start` | Welcome message, show user ID | - |
| `/drafts` | List workspaces with type=draft | `GET /api/workspaces?type=draft` |
| `/draft <id>` | Read workspace content | `GET /api/workspaces/{id}` |
| `/append <id> <text>` | Append message to workspace | `POST /api/workspaces/{id}/messages` |
| `/chat <text>` | Send message to main chat; returns LLM response | `POST /api/chat/telegram` |
| `/new_draft <name>` | Create new draft workspace | `POST /api/workspaces` |
| `/run <code>` | Execute Python code | `POST /api/sandbox/execute` (via api) |

---

## Telegram-Specific Chat Endpoint

`POST /api/chat/telegram` accepts a plain text message, creates a single-turn chat request against the user's main workspace, and returns the LLM response **synchronously** (no SSE -- Telegram's bot API does not support streaming well).

```csharp
// New endpoint in ChatEndpoint.cs
app.MapPost("/api/chat/telegram", async (
    ChatTelegramRequest req,
    ChatService chatService,
    HttpContext httpContext,
    CancellationToken ct) =>
{
    var userId = GetUserId(httpContext); // or from telegram-chat-id mapping

    // Build a single-turn ChatRequest
    var chatReq = new ChatModels.ChatRequest
    {
        Messages = new List<UiMessage>
        {
            new() { Role = "user", Content = new() { new() { Type = "text", Text = req.Text } } }
        },
        DraftId = null  // main workspace, no draft
    };

    // Consume the async stream synchronously
    var responseBuilder = new StringBuilder();
    await foreach (var line in chatService.StreamAsync(chatReq, userId, ct))
    {
        // Only collect text deltas (skip SSE framing)
        if (line.StartsWith("0:"))
            responseBuilder.Append(line[2..]);
    }

    return new { response = responseBuilder.ToString() };
});
```

---

## Directory Layout

```
telegram-bot/
├── Dockerfile              # python:3.12-slim, installs python-telegram-bot
├── requirements.txt        # python-telegram-bot, httpx
├── bot.py                  # Main entry point, async event loop
├── handlers.py             # Command handlers (/chat, /drafts, /draft, /append, /run, /new_draft)
└── api_client.py           # httpx AsyncClient wrapper for api.social-worker.localtest

docker-compose.yml          # Add telegram-bot service + TELEGRAM_BOT_TOKEN env var
```

---

## Docker Compose Additions

```yaml
telegram-bot:
  build:
    context: ./telegram-bot
    dockerfile: Dockerfile
  networks:
    - default
  environment:
    - TELEGRAM_BOT_TOKEN=${TELEGRAM_BOT_TOKEN}
    - API_BASE_URL=https://api.social-worker.localtest
    - POLLING_TIMEOUT=30
  restart: unless-stopped
  depends_on:
    api:
      condition: service_started
```

The bot uses **long polling** (not webhooks) for simplicity in a local dev setup. Production could switch to webhooks with a public URL.

---

## Implementation Phases

### Phase 1: API Endpoints + Bot Stub

**New API endpoints**:

```
POST /api/chat/telegram     → Single-turn LLM chat, returns text
POST /api/telegram/register → Link a Telegram chat ID to a user account
```

**`/register` flow**: The user sends `/start` to the bot, the bot generates a one-time code, the user enters the code in the Web UI (`Settings → Link Telegram`), and the bot confirms the link. Subsequent commands use the mapped `userId`.

**Bot stub**:

```python
# bot.py — minimal event loop
from telegram.ext import Application, CommandHandler

async def start(update, context):
    await update.message.reply_text(
        "Welcome to social-worker!\n\n"
        "Send /chat <text> to chat with your AI assistant.\n"
        "Send /help for all commands."
    )

async def chat(update, context):
    text = " ".join(context.args)
    if not text:
        await update.message.reply_text("Usage: /chat <your message>")
        return

    result = await api_client.post("/api/chat/telegram", {"text": text})
    # Split long responses into chunks (Telegram 4096 char limit)
    for i in range(0, len(result), 4096):
        await update.message.reply_text(result[i:i+4096])

async def help(update, context):
    await update.message.reply_text(
        "/chat <text> — Chat with AI\n"
        "/drafts — List your drafts\n"
        "/draft <id> — Read a draft\n"
        "/new_draft <name> — Create new draft\n"
        "/append <id> <text> — Append to draft\n"
        "/run <code> — Execute Python\n"
        "/help — This message"
    )

def main():
    app = Application.builder().token(os.environ["TELEGRAM_BOT_TOKEN"]).build()
    app.add_handler(CommandHandler("start", start))
    app.add_handler(CommandHandler("help", help))
    app.add_handler(CommandHandler("chat", chat))
    # ... other handlers
    app.run_polling(timeout=int(os.environ.get("POLLING_TIMEOUT", "30")))

if __name__ == "__main__":
    main()
```

### Phase 2: Workspace Commands

Implement `/drafts`, `/draft`, `/new_draft`, `/append` handlers. These call workspace API endpoints and format responses for Telegram.

**Key formatting considerations**:
- Draft content can be long → truncate at 2000 chars with `... (+N more chars)` footer
- Use MarkdownV2 for formatting (bold headers, code blocks for draft content)
- Lists use Telegram's native inline keyboard for selection (future)

### Phase 3: Rate Limiting + Error Handling

```python
# Rate limiter: 1 msg/sec per chat_id
import asyncio
_rate_limiters: dict[int, float] = {}

async def rate_limited(chat_id: int, coro):
    last = _rate_limiters.get(chat_id, 0)
    now = asyncio.get_event_loop().time()
    if now - last < 1.0:
        await asyncio.sleep(1.0 - (now - last))
    _rate_limiters[chat_id] = asyncio.get_event_loop().time()
    return await coro
```

**Error handling**:
- API unreachable → "Service unavailable, try again later"
- Authentication failed (unlinked account) → "Please link your Telegram account: Settings → Link Telegram in the Web UI"
- LLM timeout → "The assistant is taking longer than expected. Try again with a simpler request."

---

## Threat Model: Telegram-Specific

| Threat | Mitigation |
|---|---|
| Unauthorized Telegram user accesses drafts | `/register` flow requires user to enter one-time code in Web UI (authenticated session). Bot rejects commands from unlinked chat IDs. |
| Token leaked via Telegram message history | Bot messages are ephemeral where possible (Telegram `protect_content` flag). No credentials are ever sent to the bot. |
| Bot token stolen | `.env` never committed. Token is scoped to a single bot; revocable via @BotFather. |
| User abuses `/run` in Telegram | Rate limiter per user ID (5 executions/minute, configurable). |
| Long-running `/chat` blocks bot | Each handler runs in its own `asyncio` task. `POST /api/chat/telegram` has a 15s server-side timeout. |

---

## Links

- Virtual workspace model: `docs/SELF_EVOLVING_ASSISTANT.md` (Gap 3 section)
- Sandbox execution: `docs/PYTHON_SANDBOX.md`
- Main plan tracker: `docs/PLAN.md`