import React from "react";
import { type MediaAssetDto } from "../../../api/drafts";

interface ImagePreviewProps {
  asset: MediaAssetDto;
}

export const ImagePreview: React.FC<ImagePreviewProps> = ({ asset }) => {
  return (
    <div className="h-full flex flex-col justify-between space-y-4">
      <div className="flex-1 min-h-0 flex items-center justify-center bg-black/20 rounded-2xl border border-border/40 p-4 overflow-hidden">
        <img
          src={`/api/media/${asset.id}`}
          alt={asset.fileName}
          className="max-h-full max-w-full object-contain rounded-xl shadow-lg"
        />
      </div>
      <div className="bg-bg/40 border border-border/40 rounded-xl p-3 flex justify-between text-[10px] font-mono text-muted shrink-0">
        <div>
          <span className="text-zinc-500 uppercase">dimensions:</span> {asset.width} × {asset.height}
        </div>
        <div>
          <span className="text-zinc-500 uppercase">size:</span> {Math.round(asset.sizeBytes / 1024)} KB
        </div>
        <div>
          <span className="text-zinc-500 uppercase">format:</span> {asset.mimeType.split("/")[1]?.toUpperCase() || "UNKNOWN"}
        </div>
      </div>
    </div>
  );
};
