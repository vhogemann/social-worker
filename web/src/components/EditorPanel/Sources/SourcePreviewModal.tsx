import React, { useEffect, useState } from "react";
import { type SourceDto, type SourceDetailDto, type MediaAssetDto } from "../../../api/drafts";
import { YouTubePreview } from "./YouTubePreview";
import { TextPreview } from "./TextPreview";
import { ImagePreview } from "./ImagePreview";

interface SourcePreviewModalProps {
  item:
    | { kind: "source"; source: SourceDto; detail: SourceDetailDto | null; loading: boolean }
    | { kind: "image"; asset: MediaAssetDto };
  onClose: () => void;
  onRetryTranscription?: () => void;
  canRetryTranscription?: boolean;
  retryingTranscription?: boolean;
}

export const SourcePreviewModal: React.FC<SourcePreviewModalProps> = ({
  item,
  onClose,
  onRetryTranscription,
  canRetryTranscription = false,
  retryingTranscription = false,
}) => {
  const [activeYouTubeTab, setActiveYouTubeTab] = useState<"video" | "transcript">("video");
  const isSource = item.kind === "source";
  const title = isSource ? (item.source.title || item.source.reference) : item.asset.fileName;
  const subtitle = isSource ? item.source.reference : `media://${item.asset.id}`;
  const showOpenLink = isSource && (item.source.kind === "Url" || item.source.kind === "YouTube");
  const isYouTubeSource = isSource && item.source.kind === "YouTube";
  const processingStatus = isSource ? item.detail?.processingStatus || item.source.processingStatus : null;
  const summary = isSource ? item.detail?.summary || item.source.summary : null;
  const transcriptContent = isSource ? item.detail?.content || null : null;

  useEffect(() => {
    setActiveYouTubeTab("video");
  }, [isYouTubeSource, title, subtitle]);

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4" data-testid="source-preview-modal-overlay">
      <div className="bg-panel border border-border w-full max-w-2xl h-[500px] rounded-2xl shadow-2xl overflow-hidden flex flex-col font-sans" data-testid="source-preview-modal">
        <div className="px-6 py-4 border-b border-border/60 flex items-center justify-between">
          <div className="min-w-0 flex-1 pr-4">
            <div className="flex items-center gap-3">
              <h3 className="text-sm font-semibold text-foreground truncate">
                {title}
              </h3>
              {processingStatus ? (
                <span className="rounded-full border border-border px-2 py-0.5 text-[10px] font-mono uppercase tracking-wider text-muted shrink-0">
                  {processingStatus}
                </span>
              ) : null}
              {showOpenLink && (
                <a
                  href={item.source.reference}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-[10px] text-indigo-500 hover:text-indigo-400 font-mono underline shrink-0"
                >
                  Open Original
                </a>
              )}
              {canRetryTranscription && onRetryTranscription ? (
                <button
                  onClick={onRetryTranscription}
                  disabled={retryingTranscription}
                  className="rounded-md border border-zinc-300 px-2 py-1 text-[10px] font-mono uppercase tracking-wider text-zinc-600 transition hover:border-zinc-400 hover:text-zinc-800 disabled:opacity-50 dark:border-zinc-700 dark:text-zinc-400 dark:hover:border-zinc-500 dark:hover:text-zinc-200"
                >
                  {retryingTranscription ? "Retrying..." : "Retry Transcription"}
                </button>
              ) : null}
            </div>
            <span className="text-[10px] text-muted font-mono block mt-0.5 truncate">
              {subtitle}
            </span>
          </div>
          <button
            onClick={onClose}
            className="text-xs font-mono text-muted hover:text-foreground uppercase tracking-widest shrink-0"
          >
            close
          </button>
        </div>

        <div className="flex-1 p-6 min-h-0 select-text flex flex-col">
          {item.kind === "image" ? (
            <ImagePreview asset={item.asset} />
          ) : item.loading ? (
            <div className="flex flex-col items-center justify-center h-full gap-3 text-muted">
              <div className="w-6 h-6 border-2 border-indigo-600/30 border-t-indigo-600 rounded-full animate-spin" />
              <span className="text-xs font-mono">Loading full contents...</span>
            </div>
          ) : (
            <>
              {summary ? (
                <div className="mb-4 rounded-xl border border-border/40 bg-bg/30 p-3">
                  <div className="mb-1 text-[10px] font-mono uppercase tracking-wider text-muted">Summary</div>
                  <div className="text-xs text-zinc-300 leading-relaxed">{summary}</div>
                </div>
              ) : null}

              {item.source.kind === "YouTube" ? (
                <div className="flex flex-col min-h-0 flex-1">
                  <div className="mb-3 inline-flex rounded-lg border border-border/60 p-1 bg-bg/20 self-start">
                    <button
                      className={`rounded-md px-3 py-1.5 text-[10px] font-mono uppercase tracking-wider transition ${
                        activeYouTubeTab === "video"
                          ? "bg-zinc-800 text-zinc-100 dark:bg-zinc-100 dark:text-zinc-900"
                          : "text-muted hover:text-foreground"
                      }`}
                      onClick={() => setActiveYouTubeTab("video")}
                    >
                      Video
                    </button>
                    <button
                      className={`rounded-md px-3 py-1.5 text-[10px] font-mono uppercase tracking-wider transition ${
                        activeYouTubeTab === "transcript"
                          ? "bg-zinc-800 text-zinc-100 dark:bg-zinc-100 dark:text-zinc-900"
                          : "text-muted hover:text-foreground"
                      }`}
                      onClick={() => setActiveYouTubeTab("transcript")}
                    >
                      Transcript
                    </button>
                  </div>

                  <div className="flex-1 min-h-0">
                    {activeYouTubeTab === "video" ? (
                      <YouTubePreview reference={item.source.reference} />
                    ) : transcriptContent ? (
                      <TextPreview content={transcriptContent} />
                    ) : (
                      <div className="flex items-center justify-center h-full text-xs text-muted font-mono rounded-xl border border-border/40 bg-bg/20">
                        No transcript content extracted yet.
                      </div>
                    )}
                  </div>
                </div>
              ) : item.detail?.content ? (
                <TextPreview content={item.detail.content} />
              ) : (
                <div className="flex items-center justify-center h-full text-xs text-muted font-mono">
                  No text content extracted for this reference.
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
};
