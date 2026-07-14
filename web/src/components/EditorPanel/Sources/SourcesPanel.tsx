import React, { useState, useEffect } from "react";
import { useDraftStore } from "../../../store/draftStore";
import { useEditorStore } from "../../../store/editorStore";
import {
  fetchSourceDetail,
  fetchSourceStatus,
  retrySourceTranscription,
  deleteSource,
  linkSourceToDraft,
  searchSources,
  type SourceDto,
  type SourceDetailDto,
  type SourceSearchItemDto,
  type MediaAssetDto,
} from "../../../api/drafts";
import { SourceItem } from "./SourceItem";
import { MediaAssetItem } from "./MediaAssetItem";
import { SourcePreviewModal } from "./SourcePreviewModal";

export const SourcesPanel: React.FC = () => {
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const drafts = useDraftStore((s) => s.drafts);
  const sources = useDraftStore((s) => s.sources);
  const loadSources = useDraftStore((s) => s.loadSources);
  const uploadFileSource = useDraftStore((s) => s.uploadFileSource);
  const deleteMediaAsset = useDraftStore((s) => s.deleteMediaAsset);

  const [expanded, setExpanded] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [previewItem, setPreviewItem] = useState<{ kind: "source"; source: SourceDto } | { kind: "image"; asset: MediaAssetDto } | null>(null);
  const [previewDetail, setPreviewDetail] = useState<SourceDetailDto | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [libraryQuery, setLibraryQuery] = useState("");
  const [libraryResults, setLibraryResults] = useState<SourceSearchItemDto[]>([]);
  const [librarySearching, setLibrarySearching] = useState(false);
  const [libraryError, setLibraryError] = useState<string | null>(null);
  const [linkingSourceId, setLinkingSourceId] = useState<string | null>(null);
  const [retryingSourceId, setRetryingSourceId] = useState<string | null>(null);

  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const mediaAssets = activeDraft?.mediaAssets || [];

  useEffect(() => {
    if (activeDraftId) {
      loadSources(activeDraftId);
    }
  }, [activeDraftId, loadSources]);

  const hasFetchingSources = sources.some(s => s.title === "Fetching...");

  useEffect(() => {
    if (!hasFetchingSources || !activeDraftId) return;

    const interval = setInterval(() => {
      loadSources(activeDraftId);
    }, 2000);

    return () => clearInterval(interval);
  }, [hasFetchingSources, activeDraftId, loadSources]);

  useEffect(() => {
    if (!previewItem || previewItem.kind !== "source" || !activeDraftId) {
      setPreviewDetail(null);
      return;
    }

    setLoadingDetail(true);
    fetchSourceDetail(activeDraftId, previewItem.source.id)
      .then(setPreviewDetail)
      .catch((err) => console.error("Failed to load source details: ", err))
      .finally(() => setLoadingDetail(false));
  }, [previewItem, activeDraftId]);

  useEffect(() => {
    if (!previewItem || previewItem.kind !== "source") {
      return;
    }

    const status = previewDetail?.transcriptStatus || previewItem.source.transcriptStatus;
    if (status !== "Pending" && status !== "Processing") {
      return;
    }

    let cancelled = false;
    const syncStatus = async () => {
      try {
        const result = await fetchSourceStatus(previewItem.source.id);
        if (!result) {
          return;
        }
        if (cancelled) {
          return;
        }

        setPreviewDetail((current) => {
          if (!current) {
            return current;
          }

          return {
            ...current,
            summary: result.summary,
            transcriptStatus: result.transcriptStatus,
            youtubeVideoId: result.youtubeVideoId,
          };
        });
      } catch (err) {
        console.error("Failed to load source status:", err);
      }
    };

    void syncStatus();
    const interval = setInterval(syncStatus, 2000);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [previewItem, previewDetail]);

  const handleRetryTranscription = async (source: SourceDto) => {
    if (!activeDraftId) {
      return;
    }

    setRetryingSourceId(source.id);
    setLibraryError(null);
    try {
      const status = await retrySourceTranscription(source.id);
      setPreviewDetail((current) => {
        if (!current || current.id !== source.id) {
          return current;
        }

        return {
          ...current,
          transcriptStatus: status.transcriptStatus,
          summary: status.summary,
          youtubeVideoId: status.youtubeVideoId,
        };
      });
      await loadSources(activeDraftId);
    } catch (err) {
      setLibraryError(err instanceof Error ? err.message : "Failed to retry transcription.");
    } finally {
      setRetryingSourceId(null);
    }
  };

  if (!activeDraft) return null;

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file || !activeDraftId) return;

    setUploading(true);
    try {
      const res = await uploadFileSource(activeDraftId, file);
      const currentDoc = useEditorStore.getState().doc;
      const separator = currentDoc ? "\n\n" : "";
      const newDoc = `${currentDoc}${separator}${res.markdownLink}`;
      useEditorStore.getState().setDoc(newDoc);
    } catch (err) {
      console.error("Failed to upload file source: ", err);
    } finally {
      setUploading(false);
    }
  };

  const handleDeleteSource = async (source: SourceDto) => {
    const currentDoc = useEditorStore.getState().doc;
    let nextDoc = currentDoc;

    if (source.kind === "Url") {
      const urlEscaped = source.reference.replace(/[-\/\\^$*+?.()|[\]{}]/g, "\\$&");
      const markdownLinkRegex = new RegExp(`\\[[^\\]]*\\]\\(${urlEscaped}\\)`, "g");
      const nakedUrlRegex = new RegExp(urlEscaped, "g");
      nextDoc = nextDoc.replace(markdownLinkRegex, "").replace(nakedUrlRegex, "");
    } else if (source.kind === "File") {
      const fileRefRegex = new RegExp(`\\[[^\\]]*\\]\\(file://${source.id}\\)`, "g");
      nextDoc = nextDoc.replace(fileRefRegex, "");
    }

    useEditorStore.getState().setDoc(nextDoc.trim());

    if (activeDraftId) {
      try {
        await deleteSource(activeDraftId, source.id);
        loadSources(activeDraftId);
      } catch (err) {
        console.error("Failed to delete source:", err);
      }
    }
  };

  const handleInsertSource = (source: SourceDto) => {
    let markdown = "";
    if (source.kind === "Url" || source.kind === "YouTube") {
      const displayTitle = source.title && source.title !== "Fetching..." ? source.title : "Link";
      markdown = `[${displayTitle}](${source.reference})`;
    } else if (source.kind === "File") {
      markdown = `[${source.title || "File"}](file://${source.id})`;
    }
    if (markdown) {
      window.dispatchEvent(new CustomEvent("editor-insert", { detail: markdown }));
    }
  };

  const handleInsertMedia = (asset: MediaAssetDto) => {
    const tag = `![${asset.fileName}](media://${asset.id})`;
    window.dispatchEvent(new CustomEvent("editor-insert", { detail: tag }));
  };

  const handleDeleteMedia = async (id: string) => {
    await deleteMediaAsset(id);
    const currentDoc = useEditorStore.getState().doc;
    const refRegex = new RegExp(`!\\[[^\\]]*\\]\\(media://${id}\\)`, "g");
    const nextDoc = currentDoc.replace(refRegex, "").trim();
    useEditorStore.getState().setDoc(nextDoc);
  };

  const handleSearchLibrary = async () => {
    const query = libraryQuery.trim();
    if (!query) {
      setLibraryResults([]);
      setLibraryError(null);
      return;
    }

    setLibrarySearching(true);
    setLibraryError(null);
    try {
      const result = await searchSources(query);
      setLibraryResults(result.items);
    } catch (err) {
      setLibraryError(err instanceof Error ? err.message : "Failed to search sources.");
    } finally {
      setLibrarySearching(false);
    }
  };

  const handleLinkSource = async (sourceId: string) => {
    if (!activeDraftId) {
      return;
    }

    setLinkingSourceId(sourceId);
    try {
      await linkSourceToDraft(activeDraftId, sourceId);
      await loadSources(activeDraftId);
    } catch (err) {
      setLibraryError(err instanceof Error ? err.message : "Failed to link source.");
    } finally {
      setLinkingSourceId(null);
    }
  };

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
