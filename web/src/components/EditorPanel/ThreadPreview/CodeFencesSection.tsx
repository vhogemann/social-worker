import React from "react";
import { CodeFence } from "./types";

interface CodeFencesSectionProps {
  codeFences: CodeFence[];
  renderingFence: string | null;
  onRender: (fence: CodeFence) => Promise<void>;
}

export const CodeFencesSection: React.FC<CodeFencesSectionProps> = ({
  codeFences,
  renderingFence,
  onRender,
}) => {
  if (codeFences.length === 0) return null;

  return (
    <div className="mt-3 flex flex-wrap gap-2">
      {codeFences.map((fence, i) => {
        const isRendering = renderingFence === fence.raw;
        const label = fence.language ? fence.language : "code";
        return (
          <button
            key={i}
            onClick={() => onRender(fence)}
            disabled={renderingFence !== null}
            className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-medium border transition-colors select-none ${
              isRendering
                ? "bg-indigo-50 dark:bg-indigo-950/30 text-indigo-500 border-indigo-200 dark:border-indigo-800/50 cursor-wait"
                : "bg-zinc-50 dark:bg-zinc-900 text-zinc-600 dark:text-zinc-400 border-zinc-200 dark:border-zinc-800 hover:bg-indigo-50 dark:hover:bg-indigo-950/30 hover:text-indigo-600 dark:hover:text-indigo-400 hover:border-indigo-200 dark:hover:border-indigo-800/50"
            }`}
          >
            {isRendering ? (
              <svg className="w-3.5 h-3.5 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
              </svg>
            ) : (
              <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
              </svg>
            )}
            <span>{isRendering ? "Rendering…" : `Render ${label} as image`}</span>
          </button>
        );
      })}
    </div>
  );
};
