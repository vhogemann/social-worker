import React, { useState, useEffect } from "react";
import { useDraftStore } from "../../store/draftStore";
import { useEditorStore } from "../../store/editorStore";
import { fetchSourceDetail, deleteSource, type SourceDto, type SourceDetailDto } from "../../api/drafts";

export const SourcesPanel: React.FC = () => {
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const drafts = useDraftStore((s) => s.drafts);
  const sources = useDraftStore((s) => s.sources);
  const loadSources = useDraftStore((s) => s.loadSources);
  const uploadFileSource = useDraftStore((s) => s.uploadFileSource);
  const deleteMediaAsset = useDraftStore((s) => s.deleteMediaAsset);

  const [expanded, setExpanded] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [previewSource, setPreviewSource] = useState<SourceDto | null>(null);
  const [previewDetail, setPreviewDetail] = useState<SourceDetailDto | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);

  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const mediaAssets = activeDraft?.mediaAssets || [];

  useEffect(() => {
    if (activeDraftId) {
      loadSources(activeDraftId);
    }
  }, [activeDraftId, loadSources]);

  useEffect(() => {
    if (!previewSource || !activeDraftId) {
      setPreviewDetail(null);
      return;
    }

    setLoadingDetail(true);
    fetchSourceDetail(activeDraftId, previewSource.id)
      .then(setPreviewDetail)
      .catch((err) => console.error("Failed to load source details: ", err))
      .finally(() => setLoadingDetail(false));
  }, [previewSource, activeDraftId]);

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

  const getYouTubeEmbedUrl = (url: string): string | null => {
    const regExp = /^.*(youtu.be\/|v\/|u\/\w\/|embed\/|watch\?v=|\&v=)([^#\&\?]*).*/;
    const match = url.match(regExp);
    if (match && match[2].length === 11) {
      return `https://www.youtube.com/embed/${match[2]}`;
    }
    return null;
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
                <div
                  key={source.id}
                  className="flex items-center justify-between gap-3 p-2 rounded-xl bg-white dark:bg-zinc-950 border border-zinc-200 dark:border-zinc-800 hover:border-zinc-300 dark:hover:border-zinc-700 transition group"
                >
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
                      <span className="text-[11px] font-semibold text-zinc-800 dark:text-zinc-200 truncate leading-tight">
                        {source.title || source.reference}
                      </span>
                      <span className="text-[9px] text-zinc-400 dark:text-zinc-500 truncate leading-none mt-0.5">
                        {source.reference}
                      </span>
                    </div>
                  </div>

                  <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition">
                    <button
                      onClick={() => setPreviewSource(source)}
                      className="text-zinc-400 hover:text-indigo-500 transition p-1 rounded hover:bg-zinc-50 dark:hover:bg-zinc-900 select-none"
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
                      onClick={() => handleDeleteSource(source)}
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
              ))}

              {mediaAssets.map((asset) => (
                <div
                  key={asset.id}
                  className="flex items-center justify-between gap-3 p-2 rounded-xl bg-white dark:bg-zinc-950 border border-zinc-200 dark:border-zinc-800 hover:border-zinc-300 dark:hover:border-zinc-700 transition group"
                >
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
                      onClick={async () => {
                        await deleteMediaAsset(asset.id);
                        const currentDoc = useEditorStore.getState().doc;
                        const refRegex = new RegExp(`!\\[[^\\]]*\\]\\(media://${asset.id}\\)`, "g");
                        const nextDoc = currentDoc.replace(refRegex, "").trim();
                        useEditorStore.getState().setDoc(nextDoc);
                      }}
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
              ))}
            </div>
          )}
        </div>
      )}
      {previewSource && (
        <div className="fixed inset-0 bg-zinc-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4 font-sans">
          <div className="bg-panel border border-border w-full max-w-3xl max-h-[85vh] rounded-2xl shadow-2xl overflow-hidden flex flex-col">
            {/* Modal Header */}
            <div className="px-6 py-4 border-b border-border flex items-center justify-between select-none">
              <div className="flex flex-col min-w-0">
                <h3 className="text-sm font-semibold text-zinc-900 dark:text-zinc-100 truncate">
                  {previewSource.title || previewSource.reference}
                </h3>
                <span className="text-[11px] text-zinc-500 truncate mt-0.5">
                  {previewSource.reference}
                </span>
              </div>
              <button
                onClick={() => setPreviewSource(null)}
                className="text-zinc-400 hover:text-zinc-600 dark:hover:text-zinc-200 transition p-1.5 rounded-lg hover:bg-zinc-100 dark:hover:bg-zinc-900 focus:outline-none"
              >
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  fill="none"
                  viewBox="0 0 24 24"
                  strokeWidth={2}
                  stroke="currentColor"
                  className="w-4 h-4"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M6 18 18 6M6 6l12 12"
                  />
                </svg>
              </button>
            </div>

            {/* Modal Content */}
            <div className="flex-1 overflow-y-auto p-6 min-h-[300px]">
              {previewSource.kind === "YouTube" && getYouTubeEmbedUrl(previewSource.reference) ? (
                <div className="flex flex-col gap-4 h-full">
                  <div className="aspect-video w-full rounded-xl overflow-hidden border border-border bg-black shadow-sm">
                    <iframe
                      width="100%"
                      height="100%"
                      src={getYouTubeEmbedUrl(previewSource.reference)!}
                      title="YouTube video player"
                      frameBorder="0"
                      allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
                      allowFullScreen
                    ></iframe>
                  </div>
                  {loadingDetail ? (
                    <div className="flex items-center gap-2 text-xs text-muted">
                      <div className="w-3.5 h-3.5 border-2 border-indigo-600/30 border-t-indigo-600 rounded-full animate-spin" />
                      <span>Loading transcript/description...</span>
                    </div>
                  ) : (
                    previewDetail?.content && (
                      <div className="bg-bg border border-border rounded-xl p-4">
                        <h4 className="text-[10px] font-mono uppercase tracking-wider text-muted mb-2 select-none">Transcript / Description</h4>
                        <pre className="text-xs text-foreground font-sans whitespace-pre-wrap leading-relaxed max-h-48 overflow-y-auto">
                          {previewDetail.content}
                        </pre>
                      </div>
                    )
                  )}
                </div>
              ) : loadingDetail ? (
                <div className="flex flex-col items-center justify-center h-48 gap-3 select-none">
                  <div className="w-6 h-6 border-2 border-indigo-600/30 border-t-indigo-600 rounded-full animate-spin" />
                  <span className="text-xs text-muted">Fetching source content...</span>
                </div>
              ) : previewDetail?.content ? (
                <div className="bg-bg border border-border rounded-xl p-5 h-full">
                  <pre className="text-xs text-foreground font-mono whitespace-pre-wrap leading-relaxed">
                    {previewDetail.content}
                  </pre>
                </div>
              ) : (
                <div className="text-xs text-muted italic text-center py-12 select-none">
                  No text content available for this source.
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
