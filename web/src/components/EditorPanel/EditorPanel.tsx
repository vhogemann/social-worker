import React, { useEffect, useState } from "react";
import { MarkdownEditor } from "./MarkdownEditor";
import { ThreadPreview } from "./ThreadPreview";
import { SourcesPanel } from "./Sources/SourcesPanel";
import { EditorPanelHeader } from "./EditorPanelHeader";
import { AdaptVariantsModal } from "./AdaptVariantsModal";
import { ReplyTargetCard } from "./ReplyTargetCard";
import { useComposerDraft } from "../Composer/ComposerDraftContext";
import { useDraftStore } from "../../store/draftStore";

export const EditorPanel: React.FC = () => {
  const [isPublishing, setIsPublishing] = useState(false);
  const [adaptModalOpen, setAdaptModalOpen] = useState(false);

  const {
    doc,
    panelMode: mode,
    setPanelMode,
    activeDraft,
    activeDraftId,
    canonicalDraftId,
    addActivityCard,
    publish,
  } = useComposerDraft();

  const loadPlatformCapabilities = useDraftStore((s) => s.loadPlatformCapabilities);
  const supportsReplyTargetForPlatform = useDraftStore((s) => s.supportsReplyTargetForPlatform);

  const isCanonical = Boolean(activeDraft && !activeDraft.canonicalDraftId);
  const primaryThread = activeDraft?.threads?.[0];
  const isSent =
    primaryThread?.stage === "Sent" ||
    activeDraft?.status === "Published" ||
    activeDraft?.status === "Sent";
  const canShowReplyTarget = supportsReplyTargetForPlatform(activeDraft?.targetPlatform);

  useEffect(() => {
    loadPlatformCapabilities().catch((err) => {
      console.error("Failed to load platform capabilities", err);
    });
  }, [loadPlatformCapabilities]);

  const handlePublish = async () => {
    if (!activeDraftId) return;
    try {
      setIsPublishing(true);
      const result = await publish();

      if (result && !result.success && !(result as any).Success) {
        throw new Error(result.errorMessage || (result as any).ErrorMessage || "Failed to publish thread.");
      }
    } catch (err: any) {
      addActivityCard({
        title: "publish failed",
        message: err?.message || "Failed to publish",
        kind: "error",
      });
    } finally {
      setIsPublishing(false);
    }
  };

  const effectiveCanonicalId = activeDraft?.canonicalDraftId || canonicalDraftId || activeDraftId;

  return (
    <div className="h-full flex flex-col bg-panel overflow-hidden" data-testid="editor-panel">
      <EditorPanelHeader
        targetPlatform={activeDraft?.targetPlatform}
        mode={mode}
        onChangeMode={setPanelMode}
        isCanonical={isCanonical}
        isSent={Boolean(isSent)}
        canPublish={Boolean(activeDraft && (!isCanonical || primaryThread))}
        isPublishing={isPublishing}
        onOpenAdaptModal={() => setAdaptModalOpen(true)}
        onPublish={handlePublish}
      />

      {activeDraft && canShowReplyTarget && <ReplyTargetCard draft={activeDraft} />}

      <div
        className={`flex-1 overflow-y-auto px-3 pb-3 ${
          activeDraft && canShowReplyTarget ? "pt-3" : "pt-0"
        }`}
        data-testid={mode === "edit" ? "editor-panel-edit-mode" : "editor-panel-preview-mode"}
      >
        {mode === "edit" ? <MarkdownEditor /> : <ThreadPreview content={doc} />}
      </div>

      <SourcesPanel />

      <AdaptVariantsModal
        isOpen={adaptModalOpen}
        onClose={() => setAdaptModalOpen(false)}
        draftId={effectiveCanonicalId}
      />
    </div>
  );
};
