# MCP Image Sources Access Plan

**Status**: In Progress  
**Tracking**: [PLAN.md](../PLAN.md)  
**Date**: 2026-10-19  

---

## 1. Context & Objectives

External AI agents (such as Claude Desktop, Cursor, Goose, and CLI clients) connect to SocialWorker's MCP server (`/api/mcp` and `/api/mcp/sse`) to manage drafts, workflows, and published threads.

Previously:
- `list_sources` only returned text and URL sources from the `Sources` table, ignoring attached image media assets.
- `fetch_source` only looked up IDs in `_db.Sources`, failing when passed a media asset ID.
- Calling agents had no MCP tool to view or inspect image content (as base64 or visual pixels).
- `McpResourceRegistry` did not expose media assets, and `McpResourceContent` lacked the MCP specification's `blob` property for binary image resources.

## 2. Implemented Architecture

1. **Unified Source Discovery in `list_sources`**:
   - `list_sources` returns both textual/URL `Source` entities and `MediaAsset` entities for the draft.
   - Images are returned with `kind: "Image"`, `reference: "media://{guid}"`, `embedKind: "image"`, and media metadata.

2. **Dual Image Retrieval Interfaces**:
   - `fetch_source(sourceId)`: Polymorphically returns the image content part when passed an image source ID.
   - `view_image(id, draftId?, maxDimension?)`: Dedicated visual inspection tool accepting `media://{guid}`, raw GUIDs, `file://{guid}`, or external HTTP/HTTPS URLs.

3. **Vision Token Optimization**:
   - `maxDimension` parameter (default 1024px) downscales large images using `IImageEngineClient` to conserve agent context and tokens, while allowing `maxDimension: 0` for raw uncompressed bytes.

4. **MCP Binary Resource Support**:
   - `McpResourceContent` supports `blob` (base64 string) alongside `text`.
   - `resources/list` exposes recent media assets as `socialworker://media/{id}`.
   - `resources/read` returns binary blob contents with appropriate image MIME type.

5. **External URL Handling**:
   - Direct HTTP/HTTPS URLs passed to `view_image` are downloaded and inspected on the fly, and attached to a draft if an optional `draftId` is provided.
