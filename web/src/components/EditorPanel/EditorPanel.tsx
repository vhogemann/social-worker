import { useState } from "react";
import { MarkdownEditor } from "./MarkdownEditor";
import { ThreadPreview } from "./ThreadPreview";
import { SourcesPanel } from "./Sources/SourcesPanel";
import { useEditorStore } from "../../store/editorStore";
import { useDraftStore } from "../../store/draftStore";
import { AdaptVariantsModal } from "./AdaptVariantsModal";

export function EditorPanel() {
  const [mode, setMode] = useState<"edit" | "preview">("edit");
  const [isPublishing, setIsPublishing] = useState(false);
  const [adaptModalOpen, setAdaptModalOpen] = useState(false);
  
  const doc = useEditorStore((s) => s.doc);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const drafts = useDraftStore((s) => s.drafts);
  const publishThread = useDraftStore((s) => s.publishThread);
  const saveDraftContent = useDraftStore((s) => s.saveDraftContent);
  const updateThreadContent = useDraftStore((s) => s.updateThreadContent);

  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const isCanonical = activeDraft && !activeDraft.canonicalDraftId;
  const primaryThread = activeDraft?.threads?.[0];
  const isSent = primaryThread?.stage === "Sent";

  const handlePublish = async () => {
    if (!activeDraftId || !primaryThread) return;
    try {
      setIsPublishing(true);
      await saveDraftContent(activeDraftId, doc);
      await updateThreadContent(activeDraftId, primaryThread.id, doc);
      const result = await publishThread(activeDraftId, primaryThread.id);
      
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
        <span className="flex items-center gap-2">
          editor
          {activeDraft?.targetPlatform && (
            <span className="text-[10px] font-sans font-medium normal-case text-muted bg-border/60 px-1.5 py-0.5 rounded">
              {activeDraft.targetPlatform}
            </span>
          )}
        </span>
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
        <div className="flex items-center gap-2 ml-auto">
          {isCanonical && !isSent && (
            <button
              onClick={() => setAdaptModalOpen(true)}
              className="px-3 py-1 bg-zinc-600 hover:bg-zinc-700 text-white rounded-md text-xs font-semibold uppercase tracking-wider transition-colors"
            >
              Adapt
            </button>
          )}
          {primaryThread && (
            <button
              onClick={handlePublish}
              disabled={isPublishing || isSent}
              className="px-3 py-1 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white rounded-md text-xs font-semibold uppercase tracking-wider transition-colors"
            >
              {isPublishing ? "Publishing..." : isSent ? "Sent" : "Publish"}
            </button>
          )}
        </div>
      </div>
      <div className="flex-1 overflow-y-auto">
        {mode === "edit" ? <MarkdownEditor /> : <ThreadPreview content={doc} />}
      </div>
      <SourcesPanel />
      <AdaptVariantsModal
        isOpen={adaptModalOpen}
        onClose={() => setAdaptModalOpen(false)}
        draftId={activeDraftId}
      />
    </div>
  );
}
