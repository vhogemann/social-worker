import React from "react";
import { type SourceDto } from "../../../api/drafts";

interface SourceItemProps {
  source: SourceDto;
  onInsert: (source: SourceDto) => void;
  onPreview: (source: SourceDto) => void;
  onDelete: (source: SourceDto) => void;
}

export const SourceItem: React.FC<SourceItemProps> = ({
  source,
  onInsert,
  onPreview,
  onDelete,
}) => {
  return (
    <div className="flex items-center justify-between gap-3 p-2 rounded-xl bg-white dark:bg-zinc-950 border border-zinc-200 dark:border-zinc-800 hover:border-zinc-300 dark:hover:border-zinc-700 transition group">
      <div className="flex items-center gap-2 min-w-0">
        {source.kind === "Url" || source.kind === "YouTube" ? (
          <svg
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={1.8}
            stroke="currentColor"
            className="w-4 h-4 text-indigo-500 shrink-0"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M12 21a9.004 9.004 0 0 0 8.716-6.747M12 21a9.004 9.004 0 0 1-8.716-6.747M12 21c2.485 0 4.5-4.03 4.5-9S14.485 3 12 3m0 18c-2.485 0-4.5-4.03-4.5-9S9.515 3 12 3m0 0a8.997 8.997 0 0 1 7.843 4.582M12 3a8.997 8.997 0 0 0-7.843 4.582m15.686 0A11.953 11.953 0 0 1 12 10.5c-2.998 0-5.74-1.1-7.843-2.918m15.686 0A8.959 8.959 0 0 1 21 12c0 .778-.099 1.533-.284 2.253m0 0A17.919 17.919 0 0 1 12 16.5c-3.162 0-6.133-.815-8.716-2.247m0 0A9.015 9.015 0 0 1 3 12c0-.778.099-1.533.284-2.253m0 0A17.919 17.919 0 0 0 12 10.5c3.162 0 6.133.815 8.716 2.247V12z"
            />
          </svg>
        ) : (
          <svg
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={1.8}
            stroke="currentColor"
            className="w-4 h-4 text-emerald-500 shrink-0"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9z"
            />
          </svg>
        )}
        <div className="flex flex-col min-w-0">
          <div className="flex items-center gap-1.5">
            <span className="text-[11px] font-semibold text-zinc-800 dark:text-zinc-200 truncate leading-tight">
              {source.title || source.reference}
            </span>
            {source.transcriptStatus ? (
              <span className="rounded-full border border-zinc-200 px-1.5 py-0.5 text-[8px] font-mono uppercase tracking-wider text-zinc-500 dark:border-zinc-700 dark:text-zinc-400">
                {source.transcriptStatus}
              </span>
            ) : null}
            {source.title === "Fetching..." && (
              <div className="w-2.5 h-2.5 border border-indigo-600/30 border-t-indigo-600 rounded-full animate-spin shrink-0" />
            )}
          </div>
          <span className="text-[9px] text-zinc-400 dark:text-zinc-500 truncate leading-none mt-0.5">
            {source.reference}
          </span>
        </div>
      </div>

      <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition">
        <button
          onClick={() => onInsert(source)}
          disabled={source.title === "Fetching..."}
          className="text-zinc-400 hover:text-indigo-500 disabled:opacity-30 disabled:pointer-events-none transition p-1 rounded hover:bg-zinc-50 dark:hover:bg-zinc-900 select-none"
          title="Insert link into editor"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={2}
            stroke="currentColor"
            className="w-3.5 h-3.5"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M12 9v6m3-3H9m12 0a9 9 0 11-18 0 9 9 0 0118 0z"
            />
          </svg>
        </button>
        <button
          onClick={() => onPreview(source)}
          disabled={source.title === "Fetching..."}
          className="text-zinc-400 hover:text-indigo-500 disabled:opacity-30 disabled:pointer-events-none transition p-1 rounded hover:bg-zinc-50 dark:hover:bg-zinc-900 select-none"
          title="Preview source content"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={2}
            stroke="currentColor"
            className="w-3.5 h-3.5"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M2.036 12.322a1.012 1.012 0 0 1 0-.639C3.423 7.51 7.36 4.5 12 4.5c4.638 0 8.573 3.007 9.963 7.178.07.207.07.431 0 .639C20.577 16.49 16.64 19.5 12 19.5c-4.638 0-8.573-3.007-9.963-7.178Z"
            />
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
            />
          </svg>
        </button>
        <button
          onClick={() => onDelete(source)}
          className="text-zinc-400 hover:text-red-500 dark:hover:text-red-400 transition p-1 rounded hover:bg-zinc-50 dark:hover:bg-zinc-900 select-none"
          title="Remove link reference from draft"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            fill="none"
            viewBox="0 0 24 24"
            strokeWidth={2}
            stroke="currentColor"
            className="w-3.5 h-3.5"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12 .562c.34-.059.68-.114 1.022-.165m0 0a48.11 48.11 0 0 1 3.478-.397m7.5 0v-.916c0-1.18-.91-2.164-2.09-2.201a51.964 51.964 0 0 0-3.32 0c-1.18.037-2.09 1.022-2.09 2.201v.916m7.5 0a48.667 48.667 0 0 0-7.5 0"
            />
          </svg>
        </button>
      </div>
    </div>
  );
};
