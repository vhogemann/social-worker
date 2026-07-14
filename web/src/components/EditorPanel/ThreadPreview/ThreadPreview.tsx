import React from "react";
import { useDraftStore } from "../../../store/draftStore";
import { PreviewCard } from "./PreviewCard";
import { splitIntoSegments } from "./utils";

interface ThreadPreviewProps {
  content: string;
}

export const ThreadPreview: React.FC<ThreadPreviewProps> = ({ content }) => {
  const segments = splitIntoSegments(content);

  if (segments.length === 0) {
    return (
      <div className="flex h-full items-center justify-center p-8 text-zinc-500">
        No content to preview. Start writing in the editor to see your thread.
      </div>
    );
  }

  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const blueskyThread = activeDraft?.threads?.find(t => t.platform === "Bluesky");

  return (
    <div className="space-y-0 relative max-w-xl mx-auto py-6 px-4" data-testid="thread-preview">
      {segments.map((segment, index) => {
        const post = blueskyThread?.posts?.find(p => p.segmentIndex === index);
        return (
          <PreviewCard
            key={index}
            content={segment}
            index={index}
            total={segments.length}
            isLast={index === segments.length - 1}
            postUrl={post?.url}
          />
        );
      })}
    </div>
  );
};
