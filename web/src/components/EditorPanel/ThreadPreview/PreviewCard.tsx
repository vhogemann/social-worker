import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import Box from "@mui/material/Box";
import Card from "@mui/material/Card";
import Avatar from "@mui/material/Avatar";
import Typography from "@mui/material/Typography";
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
    <Box sx={{ position: "relative", display: "flex", alignItems: "flex-start", gap: 2, pb: 4 }}>
      {!isLast && (
        <Box
          sx={{
            position: "absolute",
            left: 20,
            top: 40,
            bottom: 0,
            width: 2,
            bgcolor: "divider",
          }}
        />
      )}

      <Avatar
        sx={{
          width: 40,
          height: 40,
          bgcolor: "primary.main",
          color: "primary.contrastText",
          fontWeight: 600,
          fontSize: "0.875rem",
          zIndex: 1,
          boxShadow: 1,
        }}
      >
        U
      </Avatar>

      <Card
        variant="outlined"
        sx={{
          flex: 1,
          minWidth: 0,
          borderRadius: 3,
          p: 2,
          bgcolor: "background.paper",
          borderColor: "divider",
          "&:hover": {
            borderColor: "primary.main",
          },
          transition: "border-color 0.2s ease",
          position: "relative",
        }}
      >
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
          <Typography
            variant="body2"
            sx={{
              color: "text.primary",
              whiteSpace: "pre-wrap",
              wordBreak: "break-word",
              lineHeight: 1.6,
              userSelect: "text",
            }}
          >
            {cleanText}
          </Typography>
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
      </Card>
    </Box>
  );
};
