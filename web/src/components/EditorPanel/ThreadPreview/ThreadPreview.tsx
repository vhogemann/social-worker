import React from "react";
import { useDraftStore } from "../../../store/draftStore";
import { BlueskyThreadPreview } from "./BlueskyThreadPreview";
import { LinkedInPostPreview } from "./LinkedInPostPreview";
import { MetaThreadsPostPreview } from "./MetaThreadsPostPreview";
import { TwitterThreadPreview } from "./TwitterThreadPreview";

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

  if (platform === "metathreads" || platform === "threads") {
    return <MetaThreadsPostPreview content={content} />;
  }

  if (platform === "twitter" || platform === "x") {
    return <TwitterThreadPreview content={content} />;
  }

  return <BlueskyThreadPreview content={content} />;
};
