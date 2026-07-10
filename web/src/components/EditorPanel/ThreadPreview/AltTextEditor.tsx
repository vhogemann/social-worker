import React, { useState } from "react";
import { useDraftStore } from "../../../store/draftStore";
import { isCodeFenceAltText } from "./utils";

interface AltTextEditorProps {
  mediaId: string;
  initialAlt: string;
  onRevert?: () => void;
}

export const AltTextEditor: React.FC<AltTextEditorProps> = ({ mediaId, initialAlt, onRevert }) => {
  const [isEditing, setIsEditing] = useState(false);
  const [alt, setAlt] = useState(initialAlt);
  const updateMediaAltText = useDraftStore((s) => s.updateMediaAltText);
  const isCodeFence = isCodeFenceAltText(initialAlt);

  const handleSave = async () => {
    try {
      await updateMediaAltText(mediaId, alt);
      setIsEditing(false);
    } catch (err) {
      console.error("Failed to save alt text:", err);
    }
  };

  if (isEditing) {
    return (
      <div className="absolute inset-x-0 bottom-0 bg-black/80 p-2 flex items-center gap-2">
        <input
          type="text"
          value={alt}
          onChange={(e) => setAlt(e.target.value)}
          placeholder="Describe this image for screen readers..."
          className="flex-1 px-2.5 py-1 bg-zinc-900 text-white text-xs rounded border border-zinc-700 focus:outline-none focus:border-indigo-500"
          autoFocus
        />
        <button onClick={handleSave} className="px-2 py-1 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded">
          Save
        </button>
        <button onClick={() => setIsEditing(false)} className="px-2 py-1 bg-zinc-800 hover:bg-zinc-700 text-white text-xs font-semibold rounded">
          Cancel
        </button>
      </div>
    );
  }

  if (isCodeFence && onRevert) {
    return (
      <div className="absolute bottom-2 left-2 flex items-center gap-1.5 opacity-90 hover:opacity-100 transition">
        <button
          onClick={() => setIsEditing(true)}
          className="px-2 py-1 rounded bg-black/60 backdrop-blur-sm text-white text-[10px] font-semibold flex items-center gap-1 shadow hover:bg-black/80"
        >
          <span>ALT</span>
          <span className="text-zinc-300 truncate max-w-[60px]">code fence</span>
        </button>
        <button
          onClick={onRevert}
          className="px-2 py-1 rounded bg-indigo-600/80 backdrop-blur-sm text-white text-[10px] font-semibold flex items-center gap-1 shadow hover:bg-indigo-600"
          title="Replace image with source code fence"
        >
          <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M9 15L3 9m0 0l6-6M3 9h12a6 6 0 010 12h-3" />
          </svg>
          <span>Revert</span>
        </button>
      </div>
    );
  }

  return (
    <div className="absolute bottom-2 left-2 flex items-center gap-1.5 opacity-90 hover:opacity-100 transition">
      <button
        onClick={() => setIsEditing(true)}
        className="px-2 py-1 rounded bg-black/60 backdrop-blur-sm text-white text-[10px] font-semibold flex items-center gap-1 shadow hover:bg-black/80"
      >
        <span>ALT</span>
        <span className="text-zinc-300 truncate max-w-[120px]">
          {initialAlt ? `: ${initialAlt}` : " (missing)"}
        </span>
      </button>
    </div>
  );
};
