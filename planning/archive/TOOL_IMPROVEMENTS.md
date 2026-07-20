# Tool Descriptions Catalogue & Improvements Tracker

This document catalogs all LLM-facing tools in `social-worker`, their current schemas, and potential areas for description enhancements.

We will iterate through these using the local Ollama instance (`gemma-e4b-32k` on `192.168.0.216`) to find the most effective prompts.

---

## 1. list_sources
- **Description**: `List all sources attached to the active draft (e.g. text notes or URLs parsed from the text).`
- **Parameters**: None.
- **Output structure**: `ListSourcesResult` containing a list of objects with:
  - `id` (Guid): Unique ID of the source.
  - `kind` (string): 'Url', 'YouTube', or 'File'.
  - `reference` (string): Source URL or filename.
  - `title` (string?): Extracted title.
  - `processingStatus` (string): 'Pending', 'Processing', 'Complete', or 'Failed'.
  - `canonicalUrl`, `citationLabel`, `embedKind`, `canonicalEmbedMarkdown`, `plainLinkLine`: formatting and embed helpers for citation insertion in drafts.
- **Key Behavior**: Does not expose the full `content` text, but does expose the `processingStatus` to help the LLM check if a source is fully digested or transcribing before calling `fetch_source`.

## 2. fetch_source
- **Description**: `Fetch the cached text content of a specific source by Guid ID. Returns a JSON object with: id, kind (Url/YouTube/File), reference (URL/filename), title, content (full text or video transcript), processingStatus (Pending/Processing/Complete/Failed), and formatting helper fields. If processingStatus is Pending or Processing, the content is not yet populated.`
- **Parameters**:
  - `id` (string): The unique Guid identifier of the source (can prefix with `file://` or `media://`).
- **Output structure**: `FetchSourceResult` containing:
  - `id`, `kind`, `reference`, `title`, `content` (string), `processingStatus` (string), `canonicalUrl`, `citationLabel`, `embedKind`, `canonicalEmbedMarkdown`, `plainLinkLine`.
- **Key Behavior**: Full text `content` is only returned if `processingStatus` is `Complete`. If `Pending` or `Processing`, the content is not yet populated.

## 3. search_sources
- **Description**: `Search the user's source library for existing sources by keyword. Returns sources NOT already linked to the active draft. Use this to find relevant sources before adding new ones, to avoid duplication.`
- **Parameters**:
  - `query` (string, required): Keyword search terms.
  - `limit` (integer, optional): Max results to return (1-20, default 5).
- **Output structure**: `SearchSourcesResult` containing:
  - `id` (Guid): Unique ID.
  - `title` (string): Title.
  - `kind` (string): Source type.
  - `preview` (string): Snippet of the summary.
  - `summary` (string): Full summary of content.
- **Key Behavior**: Filters out any sources that are already linked/associated with the active draft to avoid duplicates.

## 4. add_source
- **Description**: `Add a source to this draft. Either provide a new URL/YouTube/File reference, or pass an existing sourceId (from search_sources) to link an existing source without duplicating it.`
- **Parameters**:
  - `source_id` (string, optional): ID of an existing source (from library search) to link to this draft.
  - `kind` (string, optional): 'Url', 'YouTube', or 'File'. Required if adding a new source.
  - `reference` (string, optional): Absolute HTTP/HTTPS URL or file reference. Required if adding a new source.
  - `title` (string, optional): Custom title override.
  - `content` (string, optional): Custom text content.
- **Output structure**: `AddSourceResult` containing:
  - `success` (boolean), `message` (string), `sourceId` (Guid?), `kind` (string?), `error` (string?).
- **Key Behavior**: If `source_id` is passed, it links it directly. If creating a new Url or YouTube source, it strictly validates that the URL starts with `http://` or `https://`.

## 5. replace_editor_content
- **Description**: `Replace the entire content of the markdown editor with the provided text.`
- **Parameters**:
  - `text` (string, required): Full draft content in markdown format.
- **Key Behavior**: Replaces the entire active draft editor text.

## 6. validate_draft
- **Description**: `Validates the draft's formatting compliance for Bluesky (character limits, image counts, YouTube embeds, and missing ALT texts). YouTube videos must use embed syntax: ![Title](https://www.youtube.com/watch?v=VIDEO_ID).`
- **Parameters**:
  - `content` (string, optional): Markdown text to validate. If omitted, pulls active draft content from DB.
- **Output structure**: `ValidateDraftResult` containing:
  - `posts` (array): Each post's validation details including character counts, image count, and list of issues.
  - `overallStatus` (enum): 'Valid', 'Warnings', or 'Failed'.
- **Key Behavior**: Validates character constraints (e.g. 280-300 characters limit) and image constraints (limit 4 images per post on Bluesky). Must be run immediately after `replace_editor_content`.

<!-- Suppressed:
## 7. generate_platform_variants
- **Description**: `Generate platform-specific adaptations of the current draft for other social networks. The LLM will restructure content per-platform constraints (character limits, tone, format).`
- **Parameters**: `platforms` (array of string enums: Twitter, LinkedIn, Facebook, Instagram).
-->

## 8. publish
- **Description**: `Triggers the publication of a drafted thread to a target platform. This is only allowed when the draft's platform variant is not already Sent.`
- **Parameters**:
  - `platform` (string enum: Bluesky): The target social network.
- **Output structure**: `PublishPlatformToolResult` containing:
  - `success` (boolean): True if published successfully.
  - `message` (string): Informational message.
  - `posts` (array of PublishedPost objects): Contains details of each published post (e.g. URI, URL).
  - `error` (string?): Optional error message.
  - `authUrl` (string?): Authentication URL if user's account credentials require verification.
- **Key Behavior**: Can only publish if the variant stage is Ready/Confirmed.

## 9. image_search
- **Description**: `Search the web for images and return compact candidate URLs. For visual inspection, import a candidate via add_image_source first, then call view_image with media://{guid}.`
- **Parameters**:
  - `query` (string, required): Keywords to find images.
- **Output structure**: `ImageSearchResult` containing:
  - `query` (string): Keywords searched.
  - `results` (array): List of image results with `title`, `url`, and optional `description`.
  - `usageNotes` (array of strings): Instructions on how to download or view the images.
  - `error` (string?).

## 10. add_image_source
- **Description**: `Downloads an image from a URL, processes it, resizes it, saves it as a media asset for this draft, and returns the markdown image tag (e.g. ![alt](media://{guid})).`
- **Parameters**:
  - `url` (string, required): Direct link to the image file.
  - `altText` (string, optional): ALT text for screen readers.
- **Output structure**: `AddImageSourceResult` containing:
  - `success` (boolean), `message` (string), `markdownTag` (string? in the form `![Alt](media://{guid})`), `error` (string?).

## 11. view_image
- **Description**: `Fetch a specific image for visual inspection. Supports media://{guid}, file://{guid}, plain guid, or a direct http/https image URL (which will be imported first).`
- **Parameters**:
  - `id` (string, required): The image identifier or URL.
- **Output structure**: `ViewImageToolResult` containing:
  - `items` (array): Display items with type, text description, or image URLs.
  - `summary` (string): Textual summary of the image.

## 12. render_code_blocks
- **Description**: `Renders code blocks (triple-backtick fences) in the current draft as syntax-highlighted images and attaches them. Use when the user wants to post code as a visual image (Carbon-style). After rendering, the code fence is replaced with a compact ![code snippet](media://...) reference, which significantly reduces the post's character/word count and helps resolve character limit errors.`
- **Parameters**:
  - `theme` (string enum: Dark, Light): Visual theme for the code image. Defaults to Dark.
  - `blockIndex` (integer, optional): Render only a specific block index. If null, renders all blocks.
- **Output structure**: `RenderCodeBlocksResult` containing:
  - `success` (boolean), `renderedBlocks` (array of objects containing index, language, and markdownTag), `totalBlocks` (integer), `message` (string), `error` (string?).

## 13. set_bluesky_reply_target
- **Description**: `Set the active draft's Bluesky reply target from a strict URL in the form https://bsky.app/profile/<handle>/post/<rkey>. Once set, the reply target cannot be changed.`
- **Parameters**:
  - `url` (string, required): Absolute reply post URL.
- **Output structure**: `SetBlueskyReplyTargetToolResult` containing:
  - `success` (boolean), `message` (string), `replyParentUrl` (string?), `replyParentAuthor` (string?), `replyParentText` (string?), `replyParentAvatarUrl` (string?), `error` (string?).

## 14. web_search
- **Description**: `Search the web for current information, facts, news, or articles.`
- **Parameters**:
  - `query` (string, required): Search query keywords.
- **Output structure**: `WebSearchResult` containing:
  - `query` (string), `usageNotes` (array), `results` (array of items with rank, title, url, snippet), `error` (string?).

<!-- Suppressed:
## 15. propose_stage_transition
- **Description**: `Propose transitioning the draft to a new stage (e.g. Draft, Sent).`
- **Parameters**: `stage` (string enum: RoughDraft, Sourcing, Refining, Formatting, Ready, Sent).
-->

## 16. format_validate_platform_content
- **Description**: `Formats and validates a draft content block against target platform constraints. Use this before saving or publishing platform variants.`
- **Parameters**:
  - `platform` (string, required): Target social platform.
  - `content` (string, required): Raw text content to validate.
  - `normalizeFormatting` (boolean): Clean formatting policies.
- **Output structure**: `FormatValidatePlatformContentResult` containing:
  - `platform` (string), `isValid` (boolean), `content` (string), `normalizedContent` (string), `errors` (array of strings), `warnings` (array of strings).
