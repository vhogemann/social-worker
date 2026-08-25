# Template Studio & AI Template Generation

## Status
- **Phase**: Completed
- **Target Release**: v0.1.0

## Overview
Social-worker includes a full declarative 2D canvas rendering and template generation suite powered by `image-engine` (Fastify + Sharp + `@napi-rs/canvas`), supporting social media post cards, quotes, metrics, announcements, carousels, and code snippets.

## Architecture & Features
1. **Canvas Microservice (`image-engine`)**:
   - Renders 2D graphic templates defined as JSON layer trees (rects, circles, images, text).
   - Layout validation covers multiline word-wrap, text alignments, percentage sizing, image fit modes (`cover`, `contain`, `fill`), and text transforms.
2. **Seed Template Library**:
   - 6 production-grade templates seeded automatically on startup:
     - `Quote Card (Square)` (1080x1080)
     - `Quote Card (Portrait)` (1080x1350)
     - `Stat Highlight` (1080x1080)
     - `Announcement Card` (1080x1080)
     - `Carousel Slide` (1080x1350)
     - `Code Snippet Card` (1080x1080)
   - Restore missing defaults via `POST /api/templates/restore-defaults`.
3. **AI Template Generator (`TemplateGeneratorService`)**:
   - Generates valid declarative template JSON using configured LLM.
   - Includes 1-shot auto-repair retry against `image-engine` if test render fails.
   - Provides live preview thumbnail and slot synthesis.
4. **Chat Agent Tools**:
   - `list_templates`: Lists available templates and configurable slots.
   - `render_template`: Renders a template into an image and attaches to active draft.
   - `create_template`: Generates, verifies, and saves a new template directly from conversation.
5. **Standalone Template Studio Web UI (`/templates`)**:
   - Template gallery grid with card previews, dimensions badges, slot tags, and CRUD actions.
   - Dual-mode Studio Editor with syntax-validated JSON layer tree editor, interactive slot inputs, and live canvas preview.
   - AI Template Generator modal for prompt-to-template creation.
