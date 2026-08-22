import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { MarkdownEditor } from "./MarkdownEditor";
import { ThreadPreview } from "./ThreadPreview";
import { SourcesPanel } from "./Sources/SourcesPanel";
import { useEditorStore } from "../../store/editorStore";
import { useDraftStore } from "../../store/draftStore";
import { useChatStore } from "../../store/chatStore";
import { AdaptVariantsModal } from "./AdaptVariantsModal";
import { ReplyTargetCard } from "./ReplyTargetCard";

export function EditorPanel() {
  const [isPublishing, setIsPublishing] = useState(false);
  const [adaptModalOpen, setAdaptModalOpen] = useState(false);
  
  const doc = useEditorStore((s) => s.doc);
  const mode = useEditorStore((s) => s.panelMode);
  const setPanelMode = useEditorStore((s) => s.setPanelMode);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const drafts = useDraftStore((s) => s.drafts);
  const publishThread = useDraftStore((s) => s.publishThread);
  const saveDraftContent = useDraftStore((s) => s.saveDraftContent);
  const updateThreadContent = useDraftStore((s) => s.updateThreadContent);
  const loadPlatformCapabilities = useDraftStore((s) => s.loadPlatformCapabilities);
  const supportsReplyTargetForPlatform = useDraftStore((s) => s.supportsReplyTargetForPlatform);
  const addActivityCard = useChatStore((s) => s.addActivityCard);

  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const isCanonical = activeDraft && !activeDraft.canonicalDraftId;
  const primaryThread = activeDraft?.threads?.[0];
  const isSent = primaryThread?.stage === "Sent";
  const canShowReplyTarget = supportsReplyTargetForPlatform(activeDraft?.targetPlatform);

  useEffect(() => {
    loadPlatformCapabilities().catch((err) => {
      console.error("Failed to load platform capabilities", err);
    });
  }, [loadPlatformCapabilities]);

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
      addActivityCard(activeDraftId, {
        title: "publish failed",
        message: err?.message || "Failed to publish",
        kind: "error",
      });
    } finally {
      setIsPublishing(false);
    }
  };

  return (
    <div className="h-full flex flex-col bg-panel overflow-hidden" data-testid="editor-panel">
      <div className="px-3 py-2 border-b border-border flex items-center justify-between text-xs font-mono text-muted uppercase tracking-wider select-none">
        <span className="flex items-center gap-2">
          editor
          {activeDraft?.targetPlatform && (
            <span className="text-[10px] font-sans font-medium normal-case text-muted bg-border/60 px-1.5 py-0.5 rounded">
              {activeDraft.targetPlatform}
            </span>
          )}
        </span>
        <div className="flex items-center bg-bg border border-border rounded-lg p-0.5 font-sans lowercase">
          <button
            onClick={() => setPanelMode("edit")}
            className={`px-3 py-1 rounded-md text-xs font-medium transition duration-150 select-none ${
              mode === "edit"
                ? "bg-border text-foreground shadow-sm font-semibold"
                : "text-muted hover:text-foreground"
            }`}
          >
            edit
          </button>
          <button
            onClick={() => setPanelMode("preview")}
            className={`px-3 py-1 rounded-md text-xs font-medium transition duration-150 select-none ${
              mode === "preview"
                ? "bg-border text-foreground shadow-sm font-semibold"
                : "text-muted hover:text-foreground"
            }`}
          >
            preview
          </button>
        </div>
        <div className="flex items-center gap-2 ml-auto">
          <Link
            to="/"
            className="px-2.5 py-1 bg-border/60 hover:bg-border text-muted hover:text-foreground rounded-md text-[11px] font-mono normal-case transition"
          >
            ← Workflows
          </Link>
          {isCanonical && !isSent && (
            <button
              onClick={() => setAdaptModalOpen(true)}
              className="px-3 py-1 bg-border hover:bg-border-light text-foreground rounded-md text-xs font-semibold uppercase tracking-wider transition-colors"
            >
              Adapt
            </button>
          )}
          {primaryThread && !isSent && (
            <button
              onClick={handlePublish}
              disabled={isPublishing}
              className="px-3 py-1 bg-accent text-bg hover:opacity-90 disabled:opacity-50 rounded-md text-xs font-semibold uppercase tracking-wider transition-opacity"
            >
              {isPublishing ? "Publishing..." : "Publish"}
            </button>
          )}
        </div>
      </div>
      {activeDraft && canShowReplyTarget && <ReplyTargetCard draft={activeDraft} />}
      <div
        className={`flex-1 overflow-y-auto px-3 pb-3 ${activeDraft && canShowReplyTarget ? "pt-3" : "pt-0"}`}
        data-testid={mode === "edit" ? "editor-panel-edit-mode" : "editor-panel-preview-mode"}
      >
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
