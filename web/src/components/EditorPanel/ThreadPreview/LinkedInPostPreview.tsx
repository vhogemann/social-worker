import React from "react";
import { useDraftStore } from "../../../store/draftStore";
import { PreviewCard } from "./PreviewCard";
import { splitIntoSegments } from "./utils";

interface LinkedInPostPreviewProps {
  content: string;
}

export const LinkedInPostPreview: React.FC<LinkedInPostPreviewProps> = ({ content }) => {
  const segments = splitIntoSegments(content);

  if (segments.length === 0 || (segments.length === 1 && !segments[0].trim())) {
    return (
      <div className="flex h-full items-center justify-center p-8 text-zinc-500">
        No content to preview. Start writing in the editor to see your post.
      </div>
    );
  }

  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const linkedInThread = activeDraft?.threads?.find(t => t.platform === "LinkedIn");
  const post = linkedInThread?.posts?.find(p => p.segmentIndex === 0);

  const hasMultipleSegments = segments.filter(s => s.trim()).length > 1;

  return (
    <div className="max-w-xl mx-auto py-6 px-4" data-testid="linkedin-preview">
      {hasMultipleSegments && (
        <div className="bg-amber-50 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-900/50 text-amber-800 dark:text-amber-300 rounded-xl p-4 mb-6 text-sm flex items-start gap-3 shadow-sm" data-testid="linkedin-multi-segment-warning">
          <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor" className="w-5 h-5 mt-0.5 shrink-0">
            <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
          </svg>
          <div>
            <span className="font-semibold block mb-0.5">LinkedIn Single Post Rule</span>
            LinkedIn publishing does not support multi-post threads. Only the first non-empty segment will be published.
          </div>
        </div>
      )}
      <PreviewCard
        content={segments[0]}
        index={0}
        total={1}
        isLast={true}
        postUrl={post?.url}
      />
    </div>
  );
};
