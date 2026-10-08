import React, { useEffect, useState } from "react";
import { MarkdownEditor } from "./MarkdownEditor";
import { ThreadPreview } from "./ThreadPreview";
import { SourcesPanel } from "./Sources/SourcesPanel";
import { EditorPanelHeader } from "./EditorPanelHeader";
import { AdaptVariantsModal } from "./AdaptVariantsModal";
import { ReplyTargetCard } from "./ReplyTargetCard";
import { useComposerDraft } from "../Composer/ComposerDraftContext";
import { useDraftStore } from "../../store/draftStore";
import { enqueueQueueItem } from "../../api/schedules";
import { Button } from "../common/Button";

export const EditorPanel: React.FC = () => {
  const [isPublishing, setIsPublishing] = useState(false);
  const [adaptModalOpen, setAdaptModalOpen] = useState(false);
  const [publishError, setPublishError] = useState<string | null>(null);
  const [scheduleNotice, setScheduleNotice] = useState<string | null>(null);

  const {
    doc,
    panelMode: mode,
    setPanelMode,
    activeDraft,
    activeDraftId,
    canonicalDraftId,
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
  const isPartiallySent = primaryThread?.stage === "PartiallySent";
  const publishedUrl =
    primaryThread?.posts?.find((p) => p.url)?.url ||
    activeDraft?.threads?.flatMap((t) => t.posts || []).find((p) => p.url)?.url;
  const canShowReplyTarget = supportsReplyTargetForPlatform(activeDraft?.targetPlatform);

  useEffect(() => {
    loadPlatformCapabilities().catch((err) => {
      console.error("Failed to load platform capabilities", err);
    });
  }, [loadPlatformCapabilities]);

  const handlePublish = async () => {
    if (!activeDraftId) return;
    try {
      setPublishError(null);
      setIsPublishing(true);
      const result = await publish();

      if (result && !result.success && !(result as any).Success) {
        throw new Error(result.errorMessage || (result as any).ErrorMessage || "Failed to publish thread.");
      }
    } catch (err: any) {
      setPublishError(err?.message || "Failed to publish thread.");
    } finally {
      setIsPublishing(false);
    }
  };

  const handleAddToSchedule = async (scheduleId: string) => {
    if (!activeDraft) return;
    const thread = primaryThread || activeDraft.threads?.[0];
    if (!thread) {
      setPublishError("Draft must have at least one platform thread to add to schedule.");
      return;
    }
    setPublishError(null);
    setScheduleNotice(null);
    try {
      await enqueueQueueItem(scheduleId, {
        draftId: activeDraft.id,
        platformThreadId: thread.id
      });
      setScheduleNotice("Draft successfully queued to schedule.");
    } catch (err: any) {
      setPublishError(err?.message || "Failed to queue draft to schedule.");
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
        isPartiallySent={Boolean(isPartiallySent)}
        canPublish={Boolean(activeDraft && (!isCanonical || primaryThread))}
        isPublishing={isPublishing}
        onOpenAdaptModal={() => setAdaptModalOpen(true)}
        onPublish={handlePublish}
        onAddToSchedule={handleAddToSchedule}
        publishedUrl={publishedUrl}
      />

      {isPartiallySent && !isSent && (
        <div
          className="mx-3 mt-2 px-3 py-2 text-xs rounded-md bg-amber-950/60 border border-amber-800 text-amber-300 flex items-center justify-between shadow-sm"
          data-testid="partially-sent-banner"
        >
          <div className="flex flex-col gap-0.5">
            <span className="font-semibold">Publication incomplete</span>
            <span>
              {primaryThread?.posts?.length ?? 0} post(s) published.
              {primaryThread?.lastPublishError ? ` Error: ${primaryThread.lastPublishError}` : ""}
            </span>
          </div>
          <Button
            size="xs"
            variant="primary"
            onClick={handlePublish}
            loading={isPublishing}
            data-testid="resume-publish-banner-btn"
          >
            Resume Publish
          </Button>
        </div>
      )}

      {scheduleNotice && (
        <div
          className="mx-3 mt-2 px-3 py-2 text-xs rounded-md bg-blue-950/60 border border-blue-800 text-blue-300 flex items-center justify-between shadow-sm"
          data-testid="schedule-notice-banner"
        >
          <span>{scheduleNotice}</span>
          <button
            type="button"
            onClick={() => setScheduleNotice(null)}
            className="text-blue-400 hover:text-blue-200 ml-2 font-mono text-sm leading-none"
            aria-label="Dismiss schedule notice"
          >
            ×
          </button>
        </div>
      )}

      {publishError && (
        <div
          className="mx-3 mt-2 px-3 py-2 text-xs rounded-md bg-red-950/60 border border-red-800 text-red-300 flex items-center justify-between shadow-sm"
          data-testid="publish-error-banner"
        >
          <span>{publishError}</span>
          <button
            type="button"
            onClick={() => setPublishError(null)}
            className="text-red-400 hover:text-red-200 ml-2 font-mono text-sm leading-none"
            aria-label="Dismiss publish error"
          >
            ×
          </button>
        </div>
      )}

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
