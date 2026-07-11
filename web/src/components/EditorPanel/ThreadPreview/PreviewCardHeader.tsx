import React, { useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faCheck, faCopy, faUpRightFromSquare } from "@fortawesome/free-solid-svg-icons";

interface PreviewCardHeaderProps {
  index: number;
  total: number;
  postUrl?: string;
  cleanText: string;
}

export const PreviewCardHeader: React.FC<PreviewCardHeaderProps> = ({ index, total, postUrl, cleanText }) => {
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

        <button
          onClick={handleCopy}
          className={`flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-medium border transition-colors select-none ${
            copied
              ? "bg-emerald-50 dark:bg-emerald-950/20 text-emerald-600 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800/50"
              : "bg-zinc-50 dark:bg-zinc-900 text-zinc-600 dark:text-zinc-400 border-zinc-200 dark:border-zinc-800 hover:bg-zinc-100 dark:hover:bg-zinc-800"
          }`}
        >
          {copied ? (
            <>
              <FontAwesomeIcon icon={faCheck} className="w-3.5 h-3.5" />
              <span>Copied</span>
            </>
          ) : (
            <>
              <FontAwesomeIcon icon={faCopy} className="w-3.5 h-3.5" />
              <span>Copy</span>
            </>
          )}
        </button>
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
