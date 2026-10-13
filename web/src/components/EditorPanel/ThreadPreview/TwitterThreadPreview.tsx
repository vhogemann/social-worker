import React from "react";
import { useDraftStore } from "../../../store/draftStore";
import { PreviewCard } from "./PreviewCard";
import { splitIntoSegments, parseSegment } from "./utils";

interface TwitterThreadPreviewProps {
  content: string;
}

export function calculateTwitterWeight(text: string): number {
  if (!text) return 0;
  const urlRegex = /https?:\/\/[^\s]+/gi;
  const urlMatches = text.match(urlRegex) || [];
  const textWithoutUrls = text.replace(urlRegex, "");

  let weight = urlMatches.length * 23;
  for (const char of textWithoutUrls) {
    const codePoint = char.codePointAt(0) ?? 0;
    if (codePoint <= 0x10ff) {
      weight += 1;
    } else if (codePoint >= 0x2000 && codePoint <= 0x200d) {
      weight += 1;
    } else if (codePoint >= 0x2010 && codePoint <= 0x201f) {
      weight += 1;
    } else if (codePoint >= 0x2032 && codePoint <= 0x2037) {
      weight += 1;
    } else {
      weight += 2;
    }
  }
  return weight;
}

export const TwitterThreadPreview: React.FC<TwitterThreadPreviewProps> = ({ content }) => {
  const segments = splitIntoSegments(content);

  if (segments.length === 0 || (segments.length === 1 && !segments[0].trim())) {
    return (
      <div className="flex h-full items-center justify-center p-8 text-zinc-500">
        No content to preview. Start writing in the editor to see your X (Twitter) preview.
      </div>
    );
  }

  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const twitterThread = activeDraft?.threads?.find(
    (t) => t.platform.toLowerCase() === "twitter" || t.platform.toLowerCase() === "x"
  );

  return (
    <div className="space-y-0 relative max-w-xl mx-auto py-6 px-4" data-testid="twitter-preview">
      {segments.map((segment, index) => {
        const post = twitterThread?.posts?.find((p) => p.segmentIndex === index);
        const { cleanText, images } = parseSegment(segment);
        const weightedCount = calculateTwitterWeight(cleanText);
        const isOverLimit = weightedCount > 280;
        const tooManyImages = images.length > 4;

        return (
          <div key={index} className="relative">
            {(isOverLimit || tooManyImages) && (
              <div className="mb-2 p-2.5 rounded-lg bg-red-950/20 border border-red-900/40 text-red-400 text-xs flex items-center justify-between">
                <span>
                  {isOverLimit && `Exceeds 280 weighted-character limit (${weightedCount}/280)`}
                  {isOverLimit && tooManyImages && " • "}
                  {tooManyImages && "Twitter allows a maximum of 4 images per tweet"}
                </span>
                <span className="font-mono text-[10px] uppercase tracking-wider bg-red-900/40 px-1.5 py-0.5 rounded">
                  X Limit
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
