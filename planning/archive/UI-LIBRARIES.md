# UI Libraries

Shopping list for assembling the social-worker GUI from ready-to-use open-source libraries. Mapped to the components defined in `UI-DISCOVERY.md`.

## Editor (vim + markdown + syntax highlight)

- **CodeMirror 6** (core) — `@codemirror/state`, `@codemirror/view`, `@codemirror/commands`, `@codemirror/language`, `@codemirror/lang-markdown`, `@codemirror/search`. The markdown language pack + Lezer highlight style gives us syntax highlight on the source. GFM base via `markdownLanguage`.
- **@replit/codemirror-vim** — the canonical vim keybindings extension for CM6. Normal/insert/visual modes, ex-command bar, `Vim.defineEx` for custom `:` commands (we wire `:w`, `:setstage`, `:publish` here), `Vim.map`/`Vim.unmap` for key remaps. Must be first in the `extensions` array.
- **@uiw/react-codemirror** (optional wrapper) — React bindings for CM6 so we don't reinvent `useMarkdownEditor` from the Zenn article. We can also roll our own hook if we want full control (Zenn did and dropped the wrapper).
- Avoid the heavier "atomic-editor"/"fedoup/markdown-editor" Obsidian-style live-preview libs — our spec calls for split-pane source | preview, not inline WYSIWYG.

## Markdown model + preview

- **unified + remark-parse + remark-stringify + mdast** — the canonical markdown AST. Source-of-truth pipeline: parse md → mdast → walk for segments (split on `---` thematic breaks) → feed platform preview panes → serialize back. `remark-frontmatter` for frontmatter, `remark-gfm` for tables/strikethrough, `remark-directive` if we want `:::` blocks for per-platform overrides.
- **react-markdown** — for the rich-text preview pane. Built on the same unified pipeline so the preview stays in sync with the source AST by construction. Supports custom component overrides (handy for rendering media references and segment dividers).

## Chat window

- **assistant-ui** (`@assistant-ui/react`) — production-grade React chat primitives: streaming, auto-scroll, attachments, markdown in assistant messages, **tool-call renderers** (we surface "fetching source", "set_stage" etc. as inline React components), message history adapters. MIT, 125k+ weekly downloads, TS-native.
- Wire it via `@assistant-ui/react-data-stream` with `protocol: "data-stream"` against our .NET SSE endpoint — explicitly *not* the Vercel AI SDK adapter (AGENTS.md bans it). We implement the data stream protocol server-side in ASP.NET; assistant-ui consumes it.

## Layout shell

- **react-resizable-panels** — the de facto resizable split-pane for React in 2026 (Brian Vaughn, 5.3k stars, MIT). We need at least two split groups: a horizontal chat | editor split, then nested editor | platform-previews. Supports collapsible panels and persisted sizes via `autoSaveId` — good for the vim-feel "hide the chat and focus the editor" toggle.

## Global keymap (vim feel across the app)

- **TanStack Hotkeys** (`@tanstack/react-hotkeys`) — newer, type-safe, has `useHotkeySequence` for vim-style multi-key sequences (`gg`, `gG`, `<leader>cp`). Cross-platform `Mod` key. Has devtools/cheatsheet helpers we can repurpose as a `:help` panel.
- Alternative: **react-hotkeys-hook** (more mature, 3.5M weekly downloads) if TanStack Hotkeys is too green. It has scopes (`<HotkeysProvider>`) and `sequenceTimeoutMs` for sequences.

## Platform preview primitives

No library fits — this is bespoke. But for the per-platform preview cards, **radix-ui** primitives (Tabs, ScrollArea, Separator) or **shadcn/ui** (built on radix) give us accessible, themeable tab shells to drop platform-styled previews into. Constraint violation badges = custom.

## Media / link previews

Pasted links in the chat and editor should render rich previews: OG cards, YouTube thumbnails, video embeds, etc.

- **linkpeek** (`thegruber/linkpeek`) — server-side URL unfurl for Node/Bun/Deno/edge. Open Graph + Twitter Cards + JSON-LD + oEmbed discovery. SSRF-safe by default (rejects private IPs), small footprint, MIT. Best fit for our .NET-friendly world: call it from a small service (or port the unfurl logic) and cache results in Postgres alongside the `Source` table. Returns title, description, image, video, audio, favicon, site name, canonical URL, author, published date, keywords.
- **react-player** (`cookpete/react-player`) — 2M weekly downloads, MIT. Plays YouTube, Vimeo, Twitch, SoundCloud, Streamable, Wistia, DailyMotion, file paths, HLS, DASH, Mux. Has a `light` mode that renders the video thumbnail with a play icon and lazy-loads the full player on click — perfect for not blowing up the chat scroll with autoplaying iframes. Use `react-player/lazy` to code-split per provider.
- **@microlink/react** (optional, alternative all-in-one) — drop-in component that fetches metadata and renders a styled card. Beautiful but pulls the Microlink SaaS API; only consider if we want zero infrastructure and accept the free-tier rate limits. Otherwise stick with linkpeek + react-player for a self-hosted path.

Recommendation: **linkpeek** for server-side metadata extraction (we own the cache, no external API dependency) + **react-player** for video/audio playback in both chat and editor preview panes.

## State (already specced in AGENTS.md)

- **zustand** for the draft/editor/stage state shared between chat and editor views.
- **@tanstack/react-query** for server state (drafts, sources, posts).

## Utilities

- **react-dropzone** — drag-drop + clipboard paste for the media uploader (46M weekly downloads, MIT).
- **react-hook-form** + **zod** — forms for account credentials, brand prompts, settings. Schema validation reusable on both client and (conceptually) server.
- **sonner** — toasts for save/errors/publish feedback. Part of the shadcn ecosystem, plays well with our radix/shadcn primitives.
- **lucide-react** — icons. Already in PLAN.md, keeping it.
- **@testing-library/react** + **vitest** — when we add tests (AGENTS.md says "if/when"). Placeholder for now.

## Notable skips

- **Tiptap / Milkdown** — WYSIWYG-first, source is JSON/HTML, not markdown text. Wrong model for us.
- **Monaco** — overkill (full IDE), heavier than CM6, vim extension is less polished than `@replit/codemirror-vim`.
- **Vercel AI SDK** — explicitly banned by AGENTS.md.

## Summary table

| Component | Library |
|---|---|
| Editor core | CodeMirror 6 + `@codemirror/lang-markdown` |
| Vim mode | `@replit/codemirror-vim` |
| React CM6 wrapper | `@uiw/react-codemirror` (or roll our own) |
| Markdown AST | `unified` + `remark-parse` + `remark-stringify` + `remark-frontmatter` + `remark-gfm` |
| Rich-text preview | `react-markdown` |
| Chat | `@assistant-ui/react` + `@assistant-ui/react-data-stream` |
| Layout | `react-resizable-panels` |
| Keymap | `@tanstack/react-hotkeys` |
| UI primitives | `radix-ui` / `shadcn` |
| Link unfurl | `linkpeek` (server-side) |
| Video/audio | `react-player` (lazy) |
| State | `zustand` + `@tanstack/react-query` |
| Media upload | `react-dropzone` |
| Forms | `react-hook-form` + `zod` |
| Toasts | `sonner` |
| Icons | `lucide-react` |
| Tests (later) | `@testing-library/react` + `vitest` |
