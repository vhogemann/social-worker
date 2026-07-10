import React, { useState, useEffect } from "react";
import { useDraftStore } from "../../../store/draftStore";
import { useEditorStore } from "../../../store/editorStore";
import { fetchSourceDetail, deleteSource, type SourceDto, type SourceDetailDto, type MediaAssetDto } from "../../../api/drafts";
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
          onClose={() => setPreviewItem(null)}
        />
      )}
    </div>
  );
};
