# MVP Goal

A focused first milestone to prove the concept end-to-end before building out the full v1. Ship two surfaces talking to each other through a real LLM, and stop.

## Scope

Three things, nothing else:

1. **Chat** — streaming LLM conversation via OpenRouter, rendered in the browser
2. **Editor** — CodeMirror 6 with vim keybindings and markdown syntax highlight
3. **Integration** — the agent can insert/replace text in the editor when asked, via a single tool call

That's it. No stages, no sources, no media, no platform previews, no publishing, no accounts, no drafts list. One session, one document.

## What we're proving

- The .NET backend can stream an OpenRouter chat completion over SSE that assistant-ui consumes
- The agent can call a tool that mutates the editor's markdown content without clobbering the user's cursor/undo history
- CodeMirror 6 + `@replit/codemirror-vim` + `@codemirror/lang-markdown` feel right together
- The split-pane chat | editor layout works with `react-resizable-panels`
- TanStack Hotkeys (or react-hotkeys-hook) can drive focus between the two panes

If these five things hold, the full v1 plan is viable. If any of them are painful, we find out cheaply.

## Backend (minimum)

```
api/SocialWorker.Api/
├─ Program.cs
├─ appsettings.json
├─ Features/
│  ├─ Chat/
│  │  ├─ ChatEndpoint.cs        # POST /api/chat → SSE stream
│  │  ├─ ChatService.cs         # Microsoft.Extensions.AI + OpenAI client → OpenRouter
│  │  └─ Tools.cs               # single tool: replace_editor_content(markdown: string)
│  └─ Editor/
│     └─ EditorState.cs         # in-memory singleton holding the current markdown doc
└─ Infrastructure/
   └─ Llm/ { LlmClientFactory.cs }
```

- No database. No EF Core. No migrations. No migrator service. Editor state is an in-memory singleton — restarting the API loses it. Fine for MVP.
- No auth. Single user. CORS open to the web origin.
- One endpoint: `POST /api/chat` accepting `{ messages: [{role, content}] }`, returning a `text/event-stream` that assistant-ui's data-stream runtime can consume.
- One tool exposed to the model: `replace_editor_content(markdown: string)` — overwrites the editor document. The frontend polls or subscribes to apply the change. For MVP, a simple approach: the SSE stream emits a `tool-call` event with the new content; the frontend applies it via the CodeMirror imperative handle.

## Frontend (minimum)

```
web/src/
├─ main.tsx
├─ App.tsx                       # two-panel split: ChatPanel | EditorPanel
├─ api/
│  └─ chat.ts                    # useDataStreamRuntime against /api/chat
├─ components/
│  ├─ ChatPanel/                 # assistant-ui Thread + composer, tool-call renderer for replace_editor_content
│  └─ EditorPanel/
│     ├─ MarkdownEditor.tsx       # CodeMirror 6 + vim + lang-markdown + syntax highlight
│     └─ editorExtensions.ts     # vim(), markdown(), highlightStyle, basicSetup
└─ store/
   └─ editorStore.ts             # zustand: { doc, setDoc }
```

- `react-resizable-panels` for the split. Two panels, horizontal, draggable handle.
- `@assistant-ui/react` + `@assistant-ui/react-data-stream` for chat. Custom `tool-call` renderer for `replace_editor_content` that shows "Editor updated" inline.
- `@uiw/react-codemirror` (or a hand-rolled hook) wrapping CM6 with `vim()` first in extensions, `markdown({ base: markdownLanguage })`, and a Lezer `HighlightStyle`.
- Zustand holds the doc string; the editor is uncontrolled, the store is the bridge for tool-call application.
- One global hotkey for MVP: `<C-j>` / `<C-k>` (or `<leader>cc` / `<leader>ee`) to focus chat vs editor. Skip ex-commands, skip modes indicator polish.

## Out of scope for MVP (explicitly)

- Database, migrations, migrator service (Postgres container still runs for parity, but the API doesn't use it yet — or skip Postgres entirely and add it when the DB lands)
- Drafts, drafts list, stage stepper
- Sources (URL/file/note)
- Media upload, dropzone
- Platform previews, platform constraints, PlatformVariant
- Bluesky publishing, accounts, IPublisher
- Brand voice prompts
- Toaster / notifications (just `console.error` for now)
- Forms beyond the chat composer
- Multi-draft — one session, one doc
- Persistence across API restarts
- Local HTTPS / Caddy / mkcert (use plain HTTP on localhost for MVP; Caddy lands with the full v1)
- Running outside Docker (no `dotnet run` / `npm run dev` directly on host — everything runs in containers so the dev environment matches prod)

## Definition of done

1. `docker compose up --build` brings up `api` and `web` containers with no errors
2. `docker compose logs api` shows the API listening on its internal port
3. Open `http://localhost:3000` (or whatever port `web` exposes), type "write a short thread about cats" in the chat
4. The assistant streams a response and calls `replace_editor_content` with the generated markdown
5. The editor updates to show the markdown with syntax highlighting
6. Press `i` to enter insert mode in the editor, edit the text, press `Esc` to return to normal mode
7. Ask the assistant in chat to "rewrite the second post to be funnier" — it calls the tool again, editor updates
8. Tab between chat and editor via the hotkey

If all eight work, MVP is done. Then we layer on the rest of v1.

## Docker setup

Each component in its own container, multi-stage Dockerfile (build stage + runtime stage), orchestrated via `docker-compose.yml`. No host-side Node or .NET required.

### `docker-compose.yml` (MVP)

```yaml
services:
  api:
    build:
      context: ./api
      dockerfile: Dockerfile
    ports:
      - "5000:8080"
    environment:
      LLM__Provider: OpenRouter
      LLM__BaseUrl: https://openrouter.ai/api/v1
      LLM__ApiKey: ${LLM__ApiKey}
      LLM__Model: ${LLM__Model:-anthropic/claude-3.5-sonnet}
      ASPNETCORE_URLS: http://+:8080
    restart: unless-stopped

  web:
    build:
      context: ./web
      dockerfile: Dockerfile
    ports:
      - "3000:80"
    environment:
      VITE_API_URL: http://localhost:5000
    depends_on:
      - api
    restart: unless-stopped
```

### `api/Dockerfile` (multi-stage, .NET 10)

```dockerfile
# build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY SocialWorker.Api/*.csproj ./SocialWorker.Api/
RUN dotnet restore ./SocialWorker.Api/SocialWorker.Api.csproj
COPY SocialWorker.Api/ ./SocialWorker.Api/
WORKDIR /src/SocialWorker.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "SocialWorker.Api.dll"]
```

### `web/Dockerfile` (multi-stage, Node build → nginx serve)

```dockerfile
# build
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# runtime
FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

### `web/nginx.conf`

Proxy `/api` to the `api` container so the frontend uses same-origin:

```nginx
server {
  listen 80;
  root /usr/share/nginx/html;
  index index.html;

  location /api/ {
    proxy_pass http://api:8080/;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_http_version 1.1;
    proxy_set_header Connection "";
    proxy_buffering off;          # SSE streaming
    proxy_cache off;
  }

  location / {
    try_files $uri $uri/ /index.html;
  }
}
```

With the nginx proxy in place, the frontend talks same-origin (`/api/chat`) and we don't need `VITE_API_URL` or CORS. Drop the env var from the compose file above if we go this way.

### `.env.example` (MVP)

```
LLM__ApiKey=or-...
LLM__Model=anthropic/claude-3.5-sonnet
```

### What this proves about the dev environment

- The multi-stage builds produce lean runtime images (SDK and node_modules don't ship)
- The compose file is the same shape we'll extend for v1 (just add `db`, `migrator`, `proxy`, `adminer`, `ollama` later)
- nginx same-origin proxying means no CORS shenanigans in the browser
- Anyone can clone and `docker compose up --build` with just Docker installed — no host toolchain


## Libraries actually needed for MVP

| Layer | Library |
|---|---|
| Backend LLM | `Microsoft.Extensions.AI` + `OpenAI` .NET SDK |
| Backend SSE | ASP.NET Core built-in `text/event-stream` |
| Frontend chat | `@assistant-ui/react` + `@assistant-ui/react-data-stream` |
| Editor | `@codemirror/state` `@codemirror/view` `@codemirror/commands` `@codemirror/language` `@codemirror/lang-markdown` `@codemirror/search` |
| Vim mode | `@replit/codemirror-vim` |
| CM6 React wrapper | `@uiw/react-codemirror` (or hand-rolled) |
| Layout | `react-resizable-panels` |
| Keymap | `@tanstack/react-hotkeys` (or `react-hotkeys-hook`) |
| State | `zustand` |
| Styling | `tailwindcss` |

Everything else from `UI-LIBRARIES.md` is deferred until after MVP.

## Time-box

This should be achievable in a focused session or two. If it's dragging, the scope above is still too big — cut the hotkey and the split-pane first, fall back to a stacked layout.
