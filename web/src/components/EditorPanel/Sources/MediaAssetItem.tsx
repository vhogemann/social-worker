import React from "react";
import { type MediaAssetDto } from "../../../api/drafts";

interface MediaAssetItemProps {
  asset: MediaAssetDto;
  onInsert: (asset: MediaAssetDto) => void;
  onPreview: (asset: MediaAssetDto) => void;
  onDelete: (id: string) => void;
}

export const MediaAssetItem: React.FC<MediaAssetItemProps> = ({
  asset,
  onInsert,
  onPreview,
  onDelete,
}) => {
  return (
    <div className="flex items-center justify-between gap-3 p-2 rounded-xl bg-white dark:bg-zinc-950 border border-zinc-200 dark:border-zinc-800 hover:border-zinc-300 dark:hover:border-zinc-700 transition group">
      <div className="flex items-center gap-2 min-w-0">
        <svg
          xmlns="http://www.w3.org/2000/svg"
          fill="none"
          viewBox="0 0 24 24"
          strokeWidth={1.8}
          stroke="currentColor"
          className="w-4 h-4 text-purple-500 shrink-0"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="m2.25 15.75 5.159-5.159a2.25 2.25 0 0 1 3.182 0l5.159 5.159m-1.5-1.5 1.409-1.409a2.25 2.25 0 0 1 3.182 0l2.909 2.909m-18 3.75h16.5a1.5 1.5 0 0 0 1.5-1.5V6a1.5 1.5 0 0 0-1.5-1.5H3.75A1.5 1.5 0 0 0 2.25 6v12a1.5 1.5 0 0 0 1.5 1.5Zm10.5-11.25h.008v.008h-.008V8.25Zm.375 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Z"
          />
        </svg>
        <div className="flex flex-col min-w-0">
          <span className="text-[11px] font-semibold text-zinc-800 dark:text-zinc-200 truncate leading-tight">
            {asset.fileName}
          </span>
          <span className="text-[9px] text-zinc-400 dark:text-zinc-500 truncate leading-none mt-0.5">
            media://{asset.id} • {asset.width}x{asset.height} ({Math.round(asset.sizeBytes / 1024)} KB)
          </span>
        </div>
      </div>

      <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition">
        <button
          onClick={() => onInsert(asset)}
          className="text-zinc-400 hover:text-indigo-500 transition p-1 rounded hover:bg-zinc-50 dark:hover:bg-zinc-900 select-none"
          title="Insert image into editor"
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
          onClick={() => onPreview(asset)}
          className="text-zinc-400 hover:text-indigo-500 transition p-1 rounded hover:bg-zinc-50 dark:hover:bg-zinc-900 select-none"
          title="Preview image"
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
          onClick={() => {
            const tag = `![${asset.fileName}](media://${asset.id})`;
            navigator.clipboard.writeText(tag);
          }}
          className="text-zinc-400 hover:text-indigo-500 transition p-1 rounded hover:bg-zinc-50 dark:hover:bg-zinc-900 select-none"
          title="Copy markdown tag"
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
              d="M8.25 7.5V6.108c0-1.135.845-2.098 1.976-2.192.373-.03.748-.057 1.123-.08M15.75 18H18a3 3 0 0 0 3-3V7.5M19.5 3.75c-.373.03-.748.057-1.123.08M18 7.5a3 3 0 0 0-3-3M18 7.5V18m-9-9h9M9 9a3 3 0 0 0-3 3v6a3 3 0 0 0 3 3h6a3 3 0 0 0 3-3V12a3 3 0 0 0-3-3H9Z"
            />
          </svg>
        </button>
        <button
          onClick={() => onDelete(asset.id)}
          className="text-zinc-400 hover:text-red-500 dark:hover:text-red-400 transition p-1 rounded hover:bg-zinc-50 dark:hover:bg-zinc-900 select-none"
          title="Delete attached image"
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
