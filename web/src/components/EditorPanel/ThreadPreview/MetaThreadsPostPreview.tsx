import React from "react";
import { useDraftStore } from "../../../store/draftStore";
import { PreviewCard } from "./PreviewCard";
import { splitIntoSegments, parseSegment } from "./utils";

interface MetaThreadsPostPreviewProps {
  content: string;
}

export const MetaThreadsPostPreview: React.FC<MetaThreadsPostPreviewProps> = ({ content }) => {
  const segments = splitIntoSegments(content);

  if (segments.length === 0 || (segments.length === 1 && !segments[0].trim())) {
    return (
      <div className="flex h-full items-center justify-center p-8 text-zinc-500">
        No content to preview. Start writing in the editor to see your Threads preview.
      </div>
    );
  }

  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const threadsThread = activeDraft?.threads?.find(
    (t) => t.platform.toLowerCase() === "metathreads" || t.platform.toLowerCase() === "threads"
  );

  return (
    <div className="space-y-0 relative max-w-xl mx-auto py-6 px-4" data-testid="metathreads-preview">
      {segments.map((segment, index) => {
        const post = threadsThread?.posts?.find((p) => p.segmentIndex === index);
        const { cleanText, images } = parseSegment(segment);
        const isOverLimit = cleanText.length > 500;
        const isOverMediaLimit = images.length > 10;

        return (
          <div key={index} className="relative">
            {(isOverLimit || isOverMediaLimit) && (
              <div className="mb-2 p-2.5 rounded-lg bg-red-950/20 border border-red-900/40 text-red-400 text-xs flex items-center justify-between">
                <span>
                  {isOverLimit && `Exceeds 500-character Threads limit (${cleanText.length}/500)`}
                  {isOverLimit && isOverMediaLimit && " • "}
                  {isOverMediaLimit && `Exceeds 10 media items carousel limit (${images.length}/10)`}
                </span>
                <span className="font-mono text-[10px] uppercase tracking-wider bg-red-900/40 px-1.5 py-0.5 rounded">
                  Threads Limit
                </span>
              </div>
            )}
            <PreviewCard
              content={segment}
              index={index}
              total={segments.length}
              isLast={index === segments.length - 1}
              postUrl={post?.url}
            />
          </div>
        );
      })}
    </div>
  );
};
