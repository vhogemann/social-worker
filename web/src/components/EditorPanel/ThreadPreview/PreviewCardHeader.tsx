import React, { useState } from "react";

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
              <svg
                xmlns="http://www.w3.org/2000/svg"
                viewBox="0 0 20 20"
                fill="currentColor"
                className="w-3.5 h-3.5"
              >
                <path
                  fillRule="evenodd"
                  d="M16.704 4.153a.75.75 0 0 1 .143 1.052l-8 10.5a.75.75 0 0 1-1.127.075l-4.5-4.5a.75.75 0 0 1 1.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 0 1 1.05-.143Z"
                  clipRule="evenodd"
                />
              </svg>
              <span>Copied</span>
            </>
          ) : (
            <>
              <svg
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
                strokeWidth={1.8}
                stroke="currentColor"
                className="w-3.5 h-3.5"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M8.25 7.5V6.108c0-1.135.845-2.098 1.976-2.192.373-.03.748-.057 1.123-.08M15.75 18H18a3 3 0 0 0 3-3V7.5M19.5 3.75c-.373.03-.748.057-1.123.08M18 7.5a3 3 0 0 0-3-3M18 7.5V18m-9-9h9M9 9a3 3 0 0 0-3 3v6a3 3 0 0 0 3 3h6a3 3 0 0 0 3-3V12a3 3 0 0 0-3-3H9Z"
                />
              </svg>
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
          <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5">
            <path fillRule="evenodd" d="M4.25 5.5a.75.75 0 00-.75.75v8.5c0 .414.336.75.75.75h8.5a.75.75 0 00.75-.75v-4a.75.75 0 011.5 0v4A2.25 2.25 0 0112.75 17h-8.5A2.25 2.25 0 012 14.75v-8.5A2.25 2.25 0 014.25 4h5a.75.75 0 010 1.5h-5z" clipRule="evenodd" />
            <path fillRule="evenodd" d="M6.194 12.753a.75.75 0 001.06.053L16.5 4.44v2.81a.75.75 0 001.5 0v-4.5a.75.75 0 00-.75-.75h-4.5a.75.75 0 000 1.5h2.553l-9.056 8.194a.75.75 0 00-.053 1.06z" clipRule="evenodd" />
          </svg>
          View on Bluesky
        </a>
      )}
    </>
  );
};
