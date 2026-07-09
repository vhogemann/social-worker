import { useState } from "react";
import { MarkdownEditor } from "./MarkdownEditor";
import { ThreadPreview } from "./ThreadPreview";
import { SourcesPanel } from "./SourcesPanel";
import { useEditorStore } from "../../store/editorStore";
import { useDraftStore } from "../../store/draftStore";

export function EditorPanel() {
  const [mode, setMode] = useState<"edit" | "preview">("edit");
  const [isPublishing, setIsPublishing] = useState(false);
  
  const doc = useEditorStore((s) => s.doc);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const drafts = useDraftStore((s) => s.drafts);
  const publishThread = useDraftStore((s) => s.publishThread);
  const saveDraftContent = useDraftStore((s) => s.saveDraftContent);
  const updateThreadContent = useDraftStore((s) => s.updateThreadContent);

  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const blueskyThread = activeDraft?.threads?.find(t => t.platform === "Bluesky");

  const handlePublish = async () => {
    if (!activeDraftId || !blueskyThread) return;
    try {
      setIsPublishing(true);
      await saveDraftContent(activeDraftId, doc);
      await updateThreadContent(activeDraftId, blueskyThread.id, doc);
      const result = await publishThread(activeDraftId, blueskyThread.id);
      
      if (result && !result.success && !result.Success) {
        throw new Error(result.errorMessage || result.ErrorMessage || "Failed to publish thread.");
      }
    } catch (err: any) {
      alert(err.message || "Failed to publish");
    } finally {
      setIsPublishing(false);
    }
  };

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
        {blueskyThread && (
          <button
            onClick={handlePublish}
            disabled={isPublishing || blueskyThread.stage === "Sent"}
            className="ml-auto px-3 py-1 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white rounded-md text-xs font-semibold uppercase tracking-wider transition-colors"
          >
            {isPublishing ? "Publishing..." : blueskyThread.stage === "Sent" ? "Sent" : "Publish"}
          </button>
        )}
      </div>
      <div className="flex-1 overflow-y-auto">
        {mode === "edit" ? <MarkdownEditor /> : <ThreadPreview content={doc} />}
      </div>
      <SourcesPanel />
    </div>
  );
}
