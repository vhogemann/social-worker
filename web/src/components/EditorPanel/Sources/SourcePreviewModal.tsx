import React from "react";
import { type SourceDto, type SourceDetailDto, type MediaAssetDto } from "../../../api/drafts";
import { YouTubePreview } from "./YouTubePreview";
import { TextPreview } from "./TextPreview";
import { ImagePreview } from "./ImagePreview";

interface SourcePreviewModalProps {
  item:
    | { kind: "source"; source: SourceDto; detail: SourceDetailDto | null; loading: boolean }
    | { kind: "image"; asset: MediaAssetDto };
  onClose: () => void;
}

export const SourcePreviewModal: React.FC<SourcePreviewModalProps> = ({ item, onClose }) => {
  const isSource = item.kind === "source";
  const title = isSource ? (item.source.title || item.source.reference) : item.asset.fileName;
  const subtitle = isSource ? item.source.reference : `media://${item.asset.id}`;
  const showOpenLink = isSource && (item.source.kind === "Url" || item.source.kind === "YouTube");

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4" data-testid="source-preview-modal-overlay">
      <div className="bg-panel border border-border w-full max-w-2xl h-[500px] rounded-2xl shadow-2xl overflow-hidden flex flex-col font-sans" data-testid="source-preview-modal">
        <div className="px-6 py-4 border-b border-border/60 flex items-center justify-between">
          <div className="min-w-0 flex-1 pr-4">
            <div className="flex items-center gap-3">
              <h3 className="text-sm font-semibold text-foreground truncate">
                {title}
              </h3>
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

        <div className="flex-1 p-6 overflow-y-auto min-h-0 select-text">
          {item.kind === "image" ? (
            <ImagePreview asset={item.asset} />
          ) : item.loading ? (
            <div className="flex flex-col items-center justify-center h-full gap-3 text-muted">
              <div className="w-6 h-6 border-2 border-indigo-600/30 border-t-indigo-600 rounded-full animate-spin" />
              <span className="text-xs font-mono">Loading full contents...</span>
            </div>
          ) : (
            <>
              {item.source.kind === "YouTube" ? (
                <YouTubePreview
                  reference={item.source.reference}
                  content={item.detail?.content || null}
                />
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
