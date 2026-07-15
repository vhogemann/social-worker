import React from "react";
import { SourceItem } from "./SourceItem";
import { MediaAssetItem } from "./MediaAssetItem";
import { SourcePreviewModal } from "./SourcePreviewModal";
import { useSourcesPanelManager } from "./useSourcesPanelManager";

export const SourcesPanel: React.FC = () => {
  const {
    activeDraft,
    activeDraftId,
    sources,
    mediaAssets,
    expanded,
    uploading,
    previewItem,
    previewDetail,
    loadingDetail,
    libraryQuery,
    libraryResults,
    librarySearching,
    libraryError,
    linkingSourceId,
    retryingSourceId,
    setExpanded,
    setPreviewItem,
    setLibraryQuery,
    handleRetryTranscription,
    handleFileChange,
    handleDeleteSource,
    handleInsertSource,
    handleInsertMedia,
    handleDeleteMedia,
    handleSearchLibrary,
    handleLinkSource,
  } = useSourcesPanelManager();

  if (!activeDraft) return null;

  return (
    <div className="w-full border-t border-border bg-panel shrink-0 transition-all duration-200">
      <div className="px-4 py-2 flex items-center justify-between select-none">
        <button
          onClick={() => setExpanded(!expanded)}
          className="flex items-center gap-2 text-xs font-mono text-muted uppercase hover:text-foreground transition focus:outline-none"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            viewBox="0 0 20 20"
            fill="currentColor"
            className={`w-4 h-4 transform transition-transform duration-200 ${
              expanded ? "rotate-180" : ""
            }`}
          >
            <path
              fillRule="evenodd"
              d="M14.77 12.79a.75.75 0 0 1-1.06-.02L10 8.832 6.29 12.77a.75.75 0 1 1-1.08-1.04l4.25-4.5a.75.75 0 0 1 1.08 0l4.25 4.5a.75.75 0 0 1-.02 1.06Z"
              clipRule="evenodd"
            />
          </svg>
          <span>Sources & Images ({sources.length + mediaAssets.length})</span>
        </button>

        <div className="flex items-center gap-2">
          {uploading && (
            <div className="w-3.5 h-3.5 border-2 border-indigo-600/30 border-t-indigo-600 rounded-full animate-spin shrink-0" />
          )}
          <label
            htmlFor="attach-source-file"
            className="flex items-center gap-1 px-2.5 py-1 bg-zinc-100 dark:bg-zinc-800 border border-zinc-200 dark:border-zinc-700 hover:bg-zinc-200 dark:hover:bg-zinc-700 text-[10px] font-semibold rounded-lg shadow-sm cursor-pointer transition select-none text-zinc-700 dark:text-zinc-300"
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
                d="M12 4.5v15m7.5-7.5h-15"
              />
            </svg>
            <span>Attach File</span>
          </label>
          <input
            type="file"
            id="attach-source-file"
            className="hidden"
            accept=".txt,.md,.pdf"
            onChange={handleFileChange}
            disabled={uploading}
          />
        </div>
      </div>

      {expanded && (
        <div className="px-4 pb-4 max-h-40 overflow-y-auto border-t border-border/40 pt-3">
          <div className="mb-3 rounded-xl border border-zinc-200 bg-white p-2 dark:border-zinc-800 dark:bg-zinc-950">
            <div className="mb-2 text-[10px] font-mono uppercase tracking-wider text-muted">Search source library</div>
            <div className="flex items-center gap-2">
              <input
                value={libraryQuery}
                onChange={(e) => setLibraryQuery(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    void handleSearchLibrary();
                  }
                }}
                placeholder="Find reusable sources across drafts"
                className="flex-1 rounded-lg border border-zinc-200 bg-white px-2 py-1.5 text-xs text-zinc-800 outline-none focus:border-indigo-500 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-200"
              />
              <button
                onClick={() => void handleSearchLibrary()}
                className="rounded-lg bg-accent px-2 py-1.5 text-xs font-semibold text-bg disabled:opacity-50"
                disabled={librarySearching}
              >
                {librarySearching ? "Searching..." : "Search"}
              </button>
            </div>
            {libraryError ? <div className="mt-2 text-[11px] text-red-400">{libraryError}</div> : null}
            {libraryResults.length > 0 ? (
              <div className="mt-2 space-y-1.5">
                {libraryResults.map((result) => {
                  const alreadyLinked = sources.some((source) => source.id === result.id);
                  return (
                    <div key={result.id} className="flex items-center justify-between gap-3 rounded-lg border border-zinc-200 p-2 dark:border-zinc-800">
                      <div className="min-w-0">
                        <div className="truncate text-[11px] font-semibold text-zinc-800 dark:text-zinc-200">{result.title || result.reference}</div>
                        <div className="truncate text-[10px] text-zinc-500 dark:text-zinc-500">{result.reference}</div>
                        {result.summary ? (
                          <div className="mt-1 line-clamp-2 text-[10px] text-zinc-500 dark:text-zinc-400">{result.summary}</div>
                        ) : null}
                      </div>
                      <button
                        onClick={() => void handleLinkSource(result.id)}
                        disabled={alreadyLinked || linkingSourceId === result.id}
                        className="rounded-lg border border-zinc-200 px-2 py-1 text-[10px] font-semibold text-zinc-700 disabled:opacity-50 dark:border-zinc-700 dark:text-zinc-300"
                      >
                        {alreadyLinked ? "Linked" : linkingSourceId === result.id ? "Linking..." : "Link"}
                      </button>
                    </div>
                  );
                })}
              </div>
            ) : null}
          </div>

          {sources.length === 0 && mediaAssets.length === 0 ? (
            <div className="text-[11px] text-zinc-500 italic py-2">
              No sources or attached images detected. Drag/paste images or attach files.
            </div>
          ) : (
            <div className="space-y-1.5">
              {sources.map((source) => (
                <SourceItem
                  key={source.id}
                  source={source}
                  onInsert={handleInsertSource}
                  onPreview={(src) => setPreviewItem({ kind: "source", source: src })}
                  onDelete={handleDeleteSource}
                />
              ))}

              {mediaAssets.map((asset) => (
                <MediaAssetItem
                  key={asset.id}
                  asset={asset}
                  onInsert={handleInsertMedia}
                  onPreview={(img) => setPreviewItem({ kind: "image", asset: img })}
                  onDelete={handleDeleteMedia}
                />
              ))}
            </div>
          )}
        </div>
      )}

      {previewItem && (
        <SourcePreviewModal
          item={
            previewItem.kind === "image"
              ? { kind: "image", asset: previewItem.asset }
              : {
                  kind: "source",
                  source: previewItem.source,
                  detail: previewDetail,
                  loading: loadingDetail,
                }
          }
          canRetryTranscription={
            previewItem.kind === "source" &&
            previewItem.source.kind === "YouTube" &&
            (previewDetail?.transcriptStatus || previewItem.source.transcriptStatus) !== "Processing"
          }
          retryingTranscription={previewItem.kind === "source" && retryingSourceId === previewItem.source.id}
          onRetryTranscription={
            previewItem.kind === "source"
              ? () => void handleRetryTranscription(previewItem.source)
              : undefined
          }
          onClose={() => setPreviewItem(null)}
        />
      )}
    </div>
  );
};
