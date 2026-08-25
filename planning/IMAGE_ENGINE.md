# Image Engine Microservice & Template Rendering Plan

## Context & Objectives

This document defines the architecture and implementation plan for a dedicated **`image-engine` microservice** (Node.js 22 + Fastify + Sharp + `@napi-rs/canvas`) and the corresponding data model and API integration in `SocialWorker.Api`.

### Primary Driver

Instagram publishing requires composable image templates: a base image with text overlays, watermarks, logos, and aspect-ratio formatting. The LLM agent autonomously selects templates, fills slots, and attaches rendered images to drafts. Humans design and edit templates via the web UI (future phase).

### What this achieves

1. **Maximized Off-the-Shelf Reuse**: Uses `@napi-rs/canvas` and `sharp` to provide precompiled native engines with zero C++ compilation dependencies in Docker (no `node-gyp`, `libcairo`, or `python` needed on Alpine).
2. **Data-Driven Templates**: JSON layer tree definitions stored in Postgres with named slots. No code deployment needed to add or modify templates.
3. **Stateless Image Rendering**: The image-engine receives everything it needs (template definition, slot values, image binaries) in a single multipart request.
4. **Instagram Aspect-Ratio Formatting**: Sharp provides sub-10ms 1:1 square, 4:5 portrait, and 9:16 story transformations with blur-padding (`sharp.composite`) and cover-cropping.
5. **Image Resize Extraction**: Upload resize (max 1200px) and vision resize (max 512px) move from in-process SkiaSharp to the image-engine.
6. **Partial SkiaSharp Removal**: `ImageResizer` and `ViewImageTool` SkiaSharp usage are replaced. Code image rendering (`CodeImageRenderer`) stays in the .NET API for now (deferred to a future phase).

### What this does NOT include

- Code image rendering (Shiki / syntax highlighting) — deferred, SkiaSharp stays for this.
- ComfyUI / Cloud AI generation — future phase.
- Visual template editor in the web UI — future phase.

---

## Architectural Layout

```mermaid
graph TD
    subgraph "social-worker Compose Stack"
        UI[web: Vite React Composer UI] -->|HTTP /api| API[api: .NET 10 Minimal API]
        API --> DB[(Postgres Database)]
        API --> Transcriber[transcriber: FastAPI :8102]
        API -->|multipart POST| ImageEngine[image-engine: Fastify :8103]

        subgraph "image-engine Microservice (Node.js 22 + TypeScript)"
            ImageEngine --> FastifyZod[Fastify + Type Provider Zod]
            ImageEngine --> SharpOps[Sharp: Resize & Aspect Blur-Padding]
            ImageEngine --> NapiCanvas[@napi-rs/canvas: Declarative Layer Engine]
        end

        subgraph "api (Consumer)"
            API --> IEC[IImageEngineClient]
            IEC --> MediaService[MediaService]
            IEC --> ViewImageTool[ViewImageTool]
            IEC --> RenderTemplateTool[RenderTemplateTool]
        end
    end
```

---

## Bill of Materials (BOM) & Technical Specifications

### 1. Technology Stack

| Role | Package / Tool | Version | Rationale & Capabilities |
| :--- | :--- | :--- | :--- |
| **Runtime** | Node.js 22 LTS (Alpine) | `22-alpine` | Minimal container footprint, ESM native. |
| **HTTP Engine** | `fastify` + `@fastify/multipart` + `@fastify/cors` | `^5.0.0` | High throughput, async multipart stream parsing. |
| **Image Transforms** | `sharp` | `^0.33.0` | `libvips` C++ engine. Fast resize, crop, blur, and dual-layer composite. |
| **2D Layer Renderer** | `@napi-rs/canvas` | `^0.1.50` | Pre-built Rust N-API Skia binaries for `linux-musl`. Zero compile toolchain on Alpine. Full Canvas 2D API + `GlobalFonts`. |
| **Validation & Types** | `zod` + `fastify-type-provider-zod` | `^3.23.0` | Schema validation + TypeScript inference with automatic 400 error handling. |
| **Testing** | `vitest` | `^2.0.0` | Instant TypeScript test runner. |

### 2. Zero-Compile Dockerfile Strategy

Because both `@napi-rs/canvas` and `sharp` distribute pre-built binaries for `linux-musl`, the Dockerfile requires no build tools (`build-base`, `cairo-dev`, `python3`):

```dockerfile
FROM node:22-alpine AS runtime
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev
COPY dist/ ./dist/
EXPOSE 8000
CMD ["node", "dist/index.js"]
```

### 3. REST Endpoints

| Endpoint | Method | Input | Output | Description |
| :--- | :--- | :--- | :--- | :--- |
| `/health` | `GET` | None | JSON | Health status and engine capabilities. |
| `/transform/resize` | `POST` | Multipart: image file + `max_dimension` | Binary Image + `X-Width`, `X-Height` headers | Resizes image to fit within max dimension bounding box. |
| `/transform/aspect-ratio` | `POST` | Multipart: image file + `target_ratio` (`"1:1"`, `"4:5"`, `"9:16"`) + `fit_mode` (`"blur_padding"`, `"cover_crop"`, `"solid_padding"`) | Binary Image | Formats image into target social aspect ratio with blur padding or crop. |
| `/render/template` | `POST` | Multipart: `template` (JSON schema) + `slots` (JSON values) + image files | PNG Image Binary | Renders JSON layer tree with slot substitution and composited image assets. |

---

## Code Minimization Strategies

### 1. Aspect Ratio Blur-Padding via Sharp (~15 lines)
Offloads padding, background scaling, Gaussian blur, and foreground compositing to Sharp's native C++ pipeline without manual canvas math:

```typescript
const background = await sharp(inputBuffer)
  .resize(targetWidth, targetHeight, { fit: 'cover' })
  .blur(25)
  .toBuffer();

const foreground = await sharp(inputBuffer)
  .resize(targetWidth, targetHeight, { fit: 'inside' })
  .toBuffer();

return sharp(background)
  .composite([{ input: foreground, gravity: 'center' }])
  .jpeg({ quality: 90 })
  .toBuffer();
```

### 2. Declarative Layer Engine (~80 lines)
A lightweight dispatcher iterates the JSON layer tree onto the canvas:
- `rect`: `ctx.fillStyle = layer.fill; ctx.fillRect(x, y, w, h)` (supports `ctx.roundRect`).
- `circle`: `ctx.arc(cx, cy, radius, 0, Math.PI * 2); ctx.fill()`.
- `image`: `ctx.drawImage(images.get(layer.slot ?? layer.src), x, y, w, h)`.
- `text`: `ctx.fillText` with a 15-line word-wrap helper utilizing `ctx.measureText`.

---

## Template Schema

Templates are stored as JSONB in Postgres. The image-engine validates and renders this schema at runtime.

### Layer Tree Definition

```json
{
  "name": "instagram_quote",
  "width": 1080,
  "height": 1080,
  "slots": [
    { "name": "background_image", "type": "image", "required": true, "description": "Background photo" },
    { "name": "quote_text", "type": "text", "required": true, "description": "Main quote text" },
    { "name": "author", "type": "text", "required": false, "description": "Attribution line" }
  ],
  "layers": [
    { "type": "image", "slot": "background_image", "fit": "cover", "x": 0, "y": 0, "w": "100%", "h": "100%" },
    { "type": "rect", "fill": "rgba(0,0,0,0.4)", "x": 0, "y": 0, "w": "100%", "h": "100%" },
    { "type": "text", "slot": "quote_text", "x": 80, "y": 400, "fontSize": 48, "fontFamily": "Inter", "color": "#ffffff", "maxWidth": 920, "lineHeight": 1.4, "align": "left" },
    { "type": "text", "slot": "author", "x": 80, "y": 880, "fontSize": 28, "fontFamily": "Inter", "color": "#cccccc", "prefix": "— " },
    { "type": "image", "src": "watermark", "x": 40, "y": 980, "w": 60, "h": 60, "opacity": 0.7 }
  ]
}
```

---

## LLM Tool Interface

Two tools in the .NET API:

### 1. `list_templates`
Returns available templates, dimensions, and slot parameter definitions so the LLM agent can browse options.

### 2. `render_template`
Executes template rendering with provided slot values.
1. .NET tool loads template JSON from Postgres.
2. Resolves `media://` URIs to binary disk streams.
3. Sends multipart POST to `image-engine:8000/render/template`.
4. Stores resulting PNG via `MediaService.UploadMediaAsync` attached to the active draft.
5. Returns `media://{id}` URI to the LLM.

---

## Data Model

### Postgres `Templates` Table

| Column | Type | Description |
| :--- | :--- | :--- |
| `Id` | `Guid` | Primary key. |
| `Name` | `string` | Template display name. |
| `Description` | `string?` | Description used by LLM tool listing. |
| `Width` | `int` | Canvas width in pixels. |
| `Height` | `int` | Canvas height in pixels. |
| `Schema` | `jsonb` | Full JSON layer tree definition and slot schemas. |
| `ThumbnailMediaAssetId` | `Guid?` | FK to `MediaAsset` for preview thumbnail. |
| `CreatedAt` | `DateTimeOffset` | Creation timestamp. |
| `UpdatedAt` | `DateTimeOffset` | Last modification timestamp. |

Static assets referenced by templates (e.g. logos, watermarks) are uploaded as `MediaAsset` rows and referenced by ID.

---

## .NET API Integration & Partial SkiaSharp Removal

### 1. New Feature: `SocialWorker.Api/Features/ImageEngine/`
- `ImageEngineOptions.cs`: `BaseUrl: "http://image-engine:8000"`, `TimeoutSeconds: 30`.
- `IImageEngineClient.cs` / `ImageEngineClient.cs`: Strongly typed `HttpClient` methods for resize, aspect ratio, and template rendering.

### 2. New Feature: `SocialWorker.Api/Features/Templates/`
- `Template.cs`: EF Core entity with JSONB schema mapping.
- `TemplateEndpoints.cs`: CRUD endpoints (`/api/templates`).

### 3. New Chat Tools: `SocialWorker.Api/Features/Chat/Tools/`
- `ListTemplatesTool.cs`
- `RenderTemplateTool.cs`

### 4. Porting API Callers
- `MediaService.cs`: Replaces `ImageResizer` with `_imageEngine.ResizeImageAsync(stream, mimeType, maxDimension: 1200)`.
- `ViewImageTool.cs`: Replaces inline `SKBitmap` with `_imageEngine.ResizeImageAsync(stream, mimeType, maxDimension: 512)`.

### 5. Partial SkiaSharp Removal
- Delete `Features/Media/Services/ImageResizer.cs`.
- Remove SkiaSharp dependencies from `ViewImageTool.cs`.
- **Keep for now**: `CodeImageRenderer.cs` and `SkiaSharp` package references until code image generation is migrated in a future phase.

---

## Tasks & Phases

### Phase 1: Microservice Scaffold & Template Data Model

**image-engine Microservice:**
- [x] Create `image-engine/` with `package.json`, `tsconfig.json`, `Dockerfile`, `vitest.config.ts`.
- [x] Implement Fastify server with `@fastify/multipart`, `fastify-type-provider-zod`, and `/health`.
- [x] Implement Sharp resize service (`/transform/resize`).
- [x] Implement Sharp aspect-ratio service (`/transform/aspect-ratio`).
- [x] Implement `@napi-rs/canvas` template renderer (`/render/template`).
- [x] Write unit tests in `image-engine/tests/` and verify with `vitest`.
- [x] Add `image-engine` service to `docker-compose.yml` (`8103:8000`) and `docker-compose.e2e.yml`.

**API Template Data Model:**
- [x] Create `Template.cs` entity in EF Core.
- [x] Generate EF Core migration for `Templates` table (`0008_add_templates.sql`).
- [x] Create CRUD endpoints in `TemplateEndpoints.cs`.
- [x] Write unit tests for template endpoints.

### Phase 2: .NET API Integration, LLM Tools & Partial SkiaSharp Removal

- [x] Create `ImageEngineOptions.cs`, `IImageEngineClient.cs`, `ImageEngineClient.cs`.
- [x] Register `IImageEngineClient` in DI (`ImageEngineExtensions.cs`).
- [x] Implement `ListTemplatesTool.cs` and `RenderTemplateTool.cs`.
- [x] Update `MediaService.cs` and `ViewImageTool.cs` to use `IImageEngineClient`.
- [x] Delete `ImageResizer.cs`.
- [x] Update backend unit tests in `SocialWorker.Api.Tests`.
- [x] Build and test containers with `docker compose build`.

### Deferred: Code Image Rendering Migration

- [ ] Port `CodeImageRenderer` to image-engine using Shiki or equivalent.
- [ ] Add `/render/code` endpoint to image-engine.
- [ ] Update `CodeImageService.cs` to call image-engine.
- [ ] Delete `CodeImageRenderer.cs`, `CodeTokenizer.cs`, `CodeTheme.cs`, `ColoredRun.cs`.
- [ ] Remove `SkiaSharp` and `SkiaSharp.NativeAssets.Linux.NoDependencies` from `SocialWorker.Api.csproj`.
