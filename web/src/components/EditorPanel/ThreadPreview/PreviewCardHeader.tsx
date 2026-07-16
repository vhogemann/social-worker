import React, { useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faCheck, faCopy, faReply, faUpRightFromSquare } from "@fortawesome/free-solid-svg-icons";

interface PreviewCardHeaderProps {
  index: number;
  total: number;
  postUrl?: string;
  cleanText: string;
  canCreateReplyDraft: boolean;
  creatingReplyDraft: boolean;
  onCreateReplyDraft: () => void;
}

export const PreviewCardHeader: React.FC<PreviewCardHeaderProps> = ({
  index,
  total,
  postUrl,
  cleanText,
  canCreateReplyDraft,
  creatingReplyDraft,
  onCreateReplyDraft,
}) => {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(cleanText);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch (err) {
      console.error("Failed to copy text:", err);
    }
  };

  return (
    <>
      <div className="flex items-center justify-between gap-2 mb-2">
        <div className="flex items-center gap-1.5 min-w-0">
          <span className="font-semibold text-zinc-900 dark:text-zinc-50 truncate text-sm">
            You
          </span>
          <span className="text-zinc-500 text-xs truncate">
            @social_worker
          </span>
          <span className="text-zinc-400 dark:text-zinc-600 text-[10px]">•</span>
          <span className="text-xs font-medium text-indigo-600 dark:text-indigo-400 shrink-0">
            {index + 1}/{total}
          </span>
        </div>

        <div className="flex items-center gap-2">
          {postUrl && canCreateReplyDraft && (
            <button
              onClick={onCreateReplyDraft}
              disabled={creatingReplyDraft}
              title={creatingReplyDraft ? "Creating reply draft" : "Create reply draft"}
              aria-label={creatingReplyDraft ? "Creating reply draft" : "Create reply draft"}
              className="flex items-center justify-center w-8 h-8 rounded-lg text-xs font-medium border transition-colors select-none bg-indigo-50 dark:bg-indigo-950/30 text-indigo-600 dark:text-indigo-300 border-indigo-200 dark:border-indigo-800 hover:bg-indigo-100 dark:hover:bg-indigo-900/40 disabled:opacity-60"
            >
              <FontAwesomeIcon icon={faReply} className="w-3.5 h-3.5" />
            </button>
          )}

          <button
            onClick={handleCopy}
            title={copied ? "Copied" : "Copy"}
            aria-label={copied ? "Copied" : "Copy"}
            className={`flex items-center justify-center w-8 h-8 rounded-lg text-xs font-medium border transition-colors select-none ${
              copied
                ? "bg-emerald-50 dark:bg-emerald-950/20 text-emerald-600 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800/50"
                : "bg-zinc-50 dark:bg-zinc-900 text-zinc-600 dark:text-zinc-400 border-zinc-200 dark:border-zinc-800 hover:bg-zinc-100 dark:hover:bg-zinc-800"
            }`}
          >
            <FontAwesomeIcon icon={copied ? faCheck : faCopy} className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {postUrl && (
        <a
          href={postUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="mb-3 inline-flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-700 dark:text-indigo-400 dark:hover:text-indigo-300"
        >
          <FontAwesomeIcon icon={faUpRightFromSquare} className="w-3.5 h-3.5" />
          View on Bluesky
        </a>
      )}
    </>
  );
};
