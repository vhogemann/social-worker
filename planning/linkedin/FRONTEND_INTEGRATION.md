# Implementation Plan: LinkedIn Frontend UI Integration [COMPLETE]

Integrate LinkedIn single-post publishing capability into the React frontend, ensuring clear validation warnings for multi-segment content and displaying a tailored single-card preview instead of a multi-post thread line.

## Goals
* Update the editor panel preview mode to render a single-card preview for LinkedIn drafts rather than a thread of posts with connecting lines.
* Provide early validation feedback in the preview/editor UI if the user inputs multi-segment content (split by `---`) when LinkedIn is the selected platform.
* Cleanly split presentation concerns to keep platform-specific behaviors decoupled (e.g. reply draft features, connector lines, and segment limits).

## User Review Required
> [!IMPORTANT]
> **Decoupled Preview Rendering**: We propose refactoring the current `ThreadPreview.tsx` container into a platform switcher that routes to either `BlueskyThreadPreview.tsx` (for thread views) or `LinkedInPostPreview.tsx` (for single-card LinkedIn posts). This prevents adding if-else branching inside `PreviewCard` and preserves the clean layout of each preview representation.

> [!NOTE]
> **Multi-Segment Validation Warning**: Since LinkedIn does not support threads, if a user drafts content containing `---` (segment separators) or has multiple non-empty segments, we will display an explicit inline validation warning banner in both the editor and preview modes for LinkedIn drafts, warning that only the first segment will be published.

## Proposed Changes

---

### Frontend Components (`web/src/components/EditorPanel/`)

#### [NEW] `web/src/components/EditorPanel/ThreadPreview/LinkedInPostPreview.tsx`
Create a LinkedIn-specific preview container:
* Extracts segments from content.
* Renders a warning header if `segments.length > 1`, explaining that LinkedIn only supports a single post.
* Renders exactly one `PreviewCard` representing the first segment, with `isLast={true}`, and disabling reply target components or thread-specific metadata.

#### [NEW] `web/src/components/EditorPanel/ThreadPreview/BlueskyThreadPreview.tsx`
Extract the current Bluesky/threaded logic from `ThreadPreview.tsx` into a dedicated container:
* Renders the traditional thread of cards connected by visual timeline lines.
* Retains reply-draft generation buttons and Bluesky-specific thread header data.

#### [MODIFY] `web/src/components/EditorPanel/ThreadPreview/ThreadPreview.tsx`
Convert the existing component into a clean routing switcher:
```tsx
import React from "react";
import { useDraftStore } from "../../../store/draftStore";
import { BlueskyThreadPreview } from "./BlueskyThreadPreview";
import { LinkedInPostPreview } from "./LinkedInPostPreview";

interface ThreadPreviewProps {
  content: string;
}

export const ThreadPreview: React.FC<ThreadPreviewProps> = ({ content }) => {
  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const platform = (activeDraft?.targetPlatform ?? "").toLowerCase();

  if (platform === "linkedin") {
    return <LinkedInPostPreview content={content} />;
  }

  // Default to standard threaded preview for Bluesky/Twitter
  return <BlueskyThreadPreview content={content} />;
};
```

#### [MODIFY] `web/src/components/EditorPanel/EditorPanel.tsx`
* Ensure any publishing validation warnings or error banners are correctly rendered based on the active draft's platform policy check response.
* Check if a warning is needed in the editor toolbar/status bar for LinkedIn drafts when multiple segments exist.

## Verification Plan

### Automated Tests
Run Vitest checks inside the Docker container to ensure no regressions in existing component rendering:
```bash
docker compose exec web sh -c "npm run test"
```

### Manual Verification
1. **Create/Open a LinkedIn Draft**:
   * Verify the editor shows the `LinkedIn` tag.
   * Switch to the **preview** tab; verify it renders as a single card without thread timeline lines.
2. **Multi-segment warning test**:
   * Enter multiple segments split by `---` in the editor.
   * Verify that a clear validation warning banner appears, informing the user that LinkedIn only supports a single post.
3. **Publishing**:
   * Click **Publish** on a valid LinkedIn post and verify the trigger initiates the backend publisher successfully.
