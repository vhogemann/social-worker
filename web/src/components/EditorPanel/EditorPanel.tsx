import React, { useEffect, useState } from "react";
import { MarkdownEditor } from "./MarkdownEditor";
import { ThreadPreview } from "./ThreadPreview";
import { SourcesPanel } from "./Sources/SourcesPanel";
import { EditorPanelHeader } from "./EditorPanelHeader";
import { AdaptVariantsModal } from "./AdaptVariantsModal";
import { ReplyTargetCard } from "./ReplyTargetCard";
import { useEditorStore } from "../../store/editorStore";
import { findDraft, useDraftStore } from "../../store/draftStore";
import { useChatStore } from "../../store/chatStore";

export const EditorPanel: React.FC = () => {
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

  const activeDraft = findDraft(drafts, activeDraftId);
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
      await saveDraftContent(activeDraftId, doc);

      let threadId = primaryThread?.id;
      if (!threadId) {
        const fresh = await useDraftStore.getState().switchDraft(activeDraftId);
        threadId = fresh.threads?.[0]?.id;
      }

      if (!threadId) {
        throw new Error("No platform thread available to publish.");
      }

      await updateThreadContent(activeDraftId, threadId, doc);
      const result = await publishThread(activeDraftId, threadId);

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
        draftId={activeDraftId}
      />
    </div>
  );
};
