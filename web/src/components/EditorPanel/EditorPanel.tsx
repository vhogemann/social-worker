import { useState } from "react";
import { MarkdownEditor } from "./MarkdownEditor";
import { ThreadPreview } from "./ThreadPreview";
import { SourcesPanel } from "./SourcesPanel";
import { useEditorStore } from "../../store/editorStore";

export function EditorPanel() {
  const [mode, setMode] = useState<"edit" | "preview">("edit");
  const doc = useEditorStore((s) => s.doc);

  return (
    <div className="h-full flex flex-col bg-panel overflow-hidden">
      <div className="px-3 py-2 border-b border-border flex items-center justify-between text-xs font-mono text-muted uppercase tracking-wider select-none">
        <span>editor</span>
        <div className="flex items-center bg-zinc-100 dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-lg p-0.5 font-sans lowercase">
          <button
            onClick={() => setMode("edit")}
            className={`px-3 py-1 rounded-md text-xs font-medium transition duration-150 select-none ${
              mode === "edit"
                ? "bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-50 shadow-sm"
                : "text-zinc-500 hover:text-zinc-900 dark:hover:text-zinc-200"
            }`}
          >
            edit
          </button>
          <button
            onClick={() => setMode("preview")}
            className={`px-3 py-1 rounded-md text-xs font-medium transition duration-150 select-none ${
              mode === "preview"
                ? "bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-50 shadow-sm"
                : "text-zinc-500 hover:text-zinc-900 dark:hover:text-zinc-200"
            }`}
          >
            preview
          </button>
        </div>
      </div>
      <div className="flex-1 overflow-y-auto">
        {mode === "edit" ? <MarkdownEditor /> : <ThreadPreview content={doc} />}
      </div>
      <SourcesPanel />
    </div>
  );
}
