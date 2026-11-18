import React from "react";
import { useDraftStore } from "../../../store/draftStore";
import { PreviewCard } from "./PreviewCard";
import { splitIntoSegments, parseSegment } from "./utils";

interface MastodonThreadPreviewProps {
  content: string;
}

const URL_REGEX = /https?:\/\/[^\s<>()]+/gi;

export function calculateMastodonLength(text: string): number {
  if (!text) return 0;
  return text.replace(URL_REGEX, "x".repeat(23)).length;
}

export const MastodonThreadPreview: React.FC<MastodonThreadPreviewProps> = ({ content }) => {
  const segments = splitIntoSegments(content);

  if (segments.length === 0 || (segments.length === 1 && !segments[0].trim())) {
    return (
      <div className="flex h-full items-center justify-center p-8 text-zinc-500">
        No content to preview. Start writing in the editor to see your Mastodon preview.
      </div>
    );
  }

  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const mastodonThread = activeDraft?.threads?.find(
    (t) => t.platform.toLowerCase() === "mastodon"
  );

  return (
    <div className="space-y-0 relative max-w-xl mx-auto py-6 px-4" data-testid="mastodon-preview">
      {segments.map((segment, index) => {
        const post = mastodonThread?.posts?.find((p) => p.segmentIndex === index);
        const { cleanText, images } = parseSegment(segment);
        const charCount = calculateMastodonLength(cleanText);
        const isOverLimit = charCount > 500;
        const isOverMediaLimit = images.length > 4;

        return (
          <div key={index} className="relative">
            {(isOverLimit || isOverMediaLimit) && (
              <div className="mb-2 p-2.5 rounded-lg bg-red-950/20 border border-red-900/40 text-red-400 text-xs flex items-center justify-between">
                <span>
                  {isOverLimit && `Exceeds 500-character Mastodon limit (${charCount}/500)`}
                  {isOverLimit && isOverMediaLimit && " • "}
                  {isOverMediaLimit && `Exceeds 4 media attachments limit (${images.length}/4)`}
                </span>
                <span className="font-mono text-[10px] uppercase tracking-wider bg-red-900/40 px-1.5 py-0.5 rounded">
                  Mastodon Limit
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
