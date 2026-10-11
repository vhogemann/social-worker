# MENTAL_MODEL.md — image-engine (Image Composition & Template Service)

## Purpose

This document provides the architecture, layer rendering engine, transformation pipeline, and verification guidelines for the Node.js `image-engine` microservice.

## Tech Stack

- **Runtime & Framework**: Node.js 22 (ES modules), Fastify 5 with `fastify-type-provider-zod`.
- **Language**: TypeScript 5.7.
- **Image Processing**: `sharp` v0.33 for native libvips raster transformations.
- **Canvas Rendering**: `@napi-rs/canvas` v0.1.65 (Skia-backed hardware-accelerated 2D canvas).
- **Validation**: `zod` for strongly-typed request contracts.
- **Typography**: `@fontsource/inter` and `@fontsource/jetbrains-mono` pre-registered for canvas text rendering.
- **Testing**: Vitest 3.

## Service Architecture & Layout

```
image-engine/
├── Dockerfile                         # Node 22 Alpine production image
├── package.json                       # Scripts and dependencies
├── src/
│   ├── app.ts                         # Fastify server instance, CORS, multipart, and routes registration
│   ├── index.ts                       # Server bootstrap listening on port 8000
│   ├── routes/
│   │   ├── health.ts                  # GET /health
│   │   ├── render.ts                  # POST /render (declarative layer tree rendering)
│   │   └── transform.ts               # POST /transform (Sharp pipeline operations)
│   ├── services/
│   │   ├── template.service.ts        # Canvas drawing for backgrounds, shapes, text, and images
│   │   └── transform.service.ts       # Sharp pipeline execution (resize, crop, format, composite)
│   └── types/
│       └── schema.ts                  # Zod validation schemas for templates and transforms
└── tests/                             # Vitest unit & integration tests (14 tests in 3 files)
```

## Core Pipelines

### 1. Template Rendering Engine (`POST /render`)

Renders declarative JSON layer trees onto a Skia canvas:
- **Layer Types**:
  - `background`: Solid fill or multi-stop linear/radial gradient.
  - `rectangle`: Filled or bordered box with border-radius and opacity.
  - `text`: Wrapped text supporting font family (`Inter`, `JetBrains Mono`), weight, size, line-height, text alignment, and bounding-box clipping.
  - `image`: URL or base64 image layer with fit modes (`cover`, `contain`, `stretch`).
- Returns output as an optimized `image/png`, `image/jpeg`, or `image/webp` binary buffer.

### 2. Image Transformation Pipeline (`POST /transform`)

Processes input images through Sharp libvips operations:
- Operations include: resize (width, height, fit, position), crop, format conversion, and watermark compositing.

## API Contracts

### `GET /health`
```json
{
  "status": "ok",
  "version": "1.0.0"
}
```

### `POST /render`
Request payload:
```json
{
  "width": 1200,
  "height": 675,
  "format": "png",
  "layers": [
    {
      "type": "background",
      "color": "#18181b"
    },
    {
      "type": "text",
      "text": "Announcing Social Worker v1",
      "fontFamily": "Inter",
      "fontSize": 48,
      "color": "#ffffff",
      "x": 60,
      "y": 100,
      "maxWidth": 1080
    }
  ]
}
```
Response: Binary image stream (`Content-Type: image/png`).

## Verification & Testing

Tests run via Docker Compose with the `tooling` profile:

```bash
# Run full Vitest suite (14 tests in 3 files)
docker compose --profile tooling run --rm image-engine-test

# Run single test file
docker compose --profile tooling run --rm image-engine-test "npx vitest run tests/render.test.ts"
```
