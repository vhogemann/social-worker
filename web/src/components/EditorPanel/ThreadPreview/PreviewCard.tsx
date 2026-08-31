import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useComposerDraft } from "../../Composer/ComposerDraftContext";
import { useDraftStore } from "../../../store/draftStore";
import { renderCodeImage } from "../../../api/drafts";
import { CodeFence } from "./types";
import { parseSegment, extractCodeFences, extractCodeFenceFromAltText } from "./utils";
import { PreviewCardHeader } from "./PreviewCardHeader";
import { CodeFencesSection } from "./CodeFencesSection";
import { ImagesSection } from "./ImagesSection";
import { YouTubeSection } from "./YouTubeSection";
import { LinksSection } from "./LinksSection";
import { ConflictWarning } from "./ConflictWarning";

interface PreviewCardProps {
  content: string;
  index: number;
  total: number;
  isLast: boolean;
  postUrl?: string;
}

export const PreviewCard: React.FC<PreviewCardProps> = ({ content, index, total, isLast, postUrl }) => {
  const [renderingFence, setRenderingFence] = useState<string | null>(null);
  const [creatingReplyDraft, setCreatingReplyDraft] = useState(false);
  const navigate = useNavigate();

  const { activeDraft, activeDraftId, applyExternal, saveContent } = useComposerDraft();
  const createReplyDraftFromBlueskyPostUrl = useDraftStore((s) => s.createReplyDraftFromBlueskyPostUrl);

  const { cleanText, images, youtubeUrls, links } = parseSegment(content);
  const hasConflict = images.length > 0 && youtubeUrls.length > 0;
  const codeFences = extractCodeFences(content);

  const handleRenderFence = async (fence: CodeFence) => {
    if (!activeDraftId || !activeDraft?.content) return;
    setRenderingFence(fence.raw);
    try {
      const result = await renderCodeImage(activeDraftId, fence.code, fence.language);
      const updated = activeDraft.content.replace(fence.raw, result.markdownTag);
      applyExternal(updated);
      await saveContent(updated);
    } catch (err) {
      console.error("Failed to render code image:", err);
    } finally {
      setRenderingFence(null);
    }
  };

  const handleRevertCodeFence = async (img: { alt: string; id: string; raw: string }) => {
    if (!activeDraftId || !activeDraft?.content) {
      console.error("Missing activeDraftId or draft content");
      return;
    }
    const codeFence = extractCodeFenceFromAltText(img.alt);
    if (!codeFence) {
      console.error("Could not extract code fence from alt text:", img.alt);
      return;
    }
    try {
      const updated = activeDraft.content.replace(img.raw, codeFence);
      
      if (updated === activeDraft.content) {
        console.error("Content did not change - img.raw not found in draft content");
        return;
      }
      
      applyExternal(updated);
      await saveContent(updated);
    } catch (err) {
      console.error("Failed to revert code fence:", err);
    }
  };

  const handleCreateReplyDraft = async () => {
    if (!activeDraftId) return;

    const sourceReplyUrl = activeDraft?.blueskyReplyTarget?.replyParentUrl ?? postUrl;
    if (!sourceReplyUrl) return;

    setCreatingReplyDraft(true);
    try {
      const replyDraft = await createReplyDraftFromBlueskyPostUrl(sourceReplyUrl);
      navigate(`/draft/${replyDraft.id}`);
    } catch (err) {
      console.error("Failed to create reply draft:", err);
    } finally {
      setCreatingReplyDraft(false);
    }
  };

  const canCreateReplyDraft = (activeDraft?.targetPlatform ?? "").toLowerCase() === "bluesky";

  return (
    <div className="relative flex items-start gap-4 pb-8 group">
      {!isLast && (
        <div className="absolute left-[20px] top-[40px] bottom-0 w-[2px] bg-zinc-200 dark:bg-zinc-800" />
      )}

      <div className="relative z-10 flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-indigo-500 to-purple-600 text-white font-semibold text-sm shadow-sm select-none">
        U
      </div>

      <div className="flex-1 min-w-0 bg-white dark:bg-zinc-950 border border-zinc-200 dark:border-zinc-800 rounded-2xl p-4 shadow-sm group-hover:border-zinc-300 dark:group-hover:border-zinc-700 transition duration-150 relative">
        <PreviewCardHeader
          index={index}
          total={total}
          postUrl={postUrl}
          cleanText={cleanText}
          canCreateReplyDraft={canCreateReplyDraft}
          creatingReplyDraft={creatingReplyDraft}
          onCreateReplyDraft={handleCreateReplyDraft}
          platformName={activeDraft?.targetPlatform}
        />

        {cleanText && (
          <div className="text-zinc-800 dark:text-zinc-200 text-sm whitespace-pre-wrap break-words leading-relaxed select-text">
            {cleanText}
          </div>
        )}

        <CodeFencesSection
          codeFences={codeFences}
          renderingFence={renderingFence}
          onRender={handleRenderFence}
        />

        <ImagesSection images={images} onRevert={handleRevertCodeFence} />

        <YouTubeSection youtubeUrls={youtubeUrls} />

        <LinksSection links={links} />

        <ConflictWarning hasConflict={hasConflict} />
      </div>
    </div>
  );
};
