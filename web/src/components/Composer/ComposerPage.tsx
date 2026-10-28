import React, { useEffect, useRef, useState } from "react";
import { useHotkey } from "@tanstack/react-hotkeys";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faSpinner, faArrowLeft, faBars } from "@fortawesome/free-solid-svg-icons";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { ChatPanel } from "../ChatPanel/ChatPanel";
import { EditorPanel } from "../EditorPanel/EditorPanel";
import { DraftVariantTabs } from "./DraftVariantTabs";
import { AdaptVariantsModal } from "../EditorPanel/AdaptVariantsModal";
import { ComposerDraftProvider, useComposerDraft } from "./ComposerDraftContext";
import { ScopedChatProvider } from "../../api/chat";
import { useDraftStore } from "../../store/draftStore";
import { DraftPreviewHeader } from "./DraftPreviewHeader";
import { DraftMenuDrawer } from "./DraftMenuDrawer";
import { ThreadPreview } from "../EditorPanel/ThreadPreview";
import { ReplyTargetCard } from "../EditorPanel/ReplyTargetCard";
import { Button } from "../common/Button";
import { enqueueQueueItem } from "../../api/schedules";

export interface ComposerPageProps {
  chatRef?: React.RefObject<HTMLDivElement | null>;
  editorRef?: React.RefObject<HTMLDivElement | null>;
}

const ComposerViewContent: React.FC<ComposerPageProps> = ({
  chatRef: externalChatRef,
  editorRef: externalEditorRef,
}) => {
  const {
    activeDraft,
    canonicalDraft,
    workflowId,
    canonicalDraftId,
    activeDraftId,
    doc,
    isLoading,
    publish,
  } = useComposerDraft();

  const [activeView, setActiveView] = useState<"preview" | "editor" | "chat">("preview");
  const [isDrawerOpen, setIsDrawerOpen] = useState(false);
  const [adaptModalOpen, setAdaptModalOpen] = useState(false);
  const [isPublishing, setIsPublishing] = useState(false);
  const [publishError, setPublishError] = useState<string | null>(null);
  const [scheduleNotice, setScheduleNotice] = useState<string | null>(null);

  const internalChatRef = useRef<HTMLDivElement>(null);
  const internalEditorRef = useRef<HTMLDivElement>(null);

  const chatRef = externalChatRef || internalChatRef;
  const editorRef = externalEditorRef || internalEditorRef;

  const supportsReplyTargetForPlatform = useDraftStore((s) => s.supportsReplyTargetForPlatform);
  const loadPlatformCapabilities = useDraftStore((s) => s.loadPlatformCapabilities);

  useEffect(() => {
    loadPlatformCapabilities().catch(() => {});
  }, [loadPlatformCapabilities]);

  useEffect(() => {
    if (activeDraft) {
      useDraftStore.setState((s) => ({
        activeDraftId: activeDraft.id,
        drafts: s.drafts.some((d) => d.id === activeDraft.id)
          ? s.drafts.map((d) => (d.id === activeDraft.id ? { ...d, ...activeDraft } : d))
          : [activeDraft, ...s.drafts],
      }));
    }
  }, [activeDraft]);

  useHotkey("Mod+J", () => {
    setActiveView("chat");
    setTimeout(() => chatRef.current?.focus(), 50);
  });

  useHotkey("Mod+K", () => {
    setActiveView("editor");
    setTimeout(() => editorRef.current?.focus(), 50);
  });

  if (isLoading || !activeDraft) {
    return (
      <div
        className="flex-1 min-h-0 flex items-center justify-center bg-bg text-xs font-mono text-muted gap-2"
        data-testid="composer-loading"
      >
        <FontAwesomeIcon icon={faSpinner} spin className="text-sm text-accent" />
        <span>loading draft...</span>
      </div>
    );
  }

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

  const effectiveCanonicalId = canonicalDraft?.id || canonicalDraftId || activeDraftId;
  const variants = canonicalDraft?.variants || [];

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
        platformThreadId: thread.id,
      });
      setScheduleNotice("Draft successfully queued to schedule.");
    } catch (err: any) {
      setPublishError(err?.message || "Failed to queue draft to schedule.");
    }
  };

  return (
    <ScopedChatProvider
      draftId={activeDraftId}
      initialChatHistory={activeDraft.chatHistory}
      editorDoc={doc}
    >
      <div className="flex-1 min-h-0 flex flex-col relative bg-bg" data-testid="composer-page">
        {activeView === "preview" && (
          <div className="flex-1 min-h-0 flex flex-col overflow-hidden" data-testid="composer-preview-view">
            <DraftPreviewHeader
              title={activeDraft.title}
              targetPlatform={activeDraft.targetPlatform}
              isCanonical={isCanonical}
              isSent={Boolean(isSent)}
              isPartiallySent={Boolean(isPartiallySent)}
              canPublish={Boolean(activeDraft && (!isCanonical || primaryThread))}
              isPublishing={isPublishing}
              onOpenMenu={() => setIsDrawerOpen(true)}
              onOpenAdaptModal={() => setAdaptModalOpen(true)}
              onPublish={handlePublish}
              onAddToSchedule={handleAddToSchedule}
              publishedUrl={publishedUrl}
            />

            {effectiveCanonicalId && (
              <DraftVariantTabs
                workflowId={workflowId}
                canonicalDraftId={effectiveCanonicalId}
                activeDraftId={activeDraftId}
                variants={variants}
                onOpenAdaptModal={() => setAdaptModalOpen(true)}
              />
            )}

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
                  className="text-blue-400 hover:text-blue-200 ml-2 font-mono text-sm leading-none cursor-pointer"
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
                  className="text-red-400 hover:text-red-200 ml-2 font-mono text-sm leading-none cursor-pointer"
                  aria-label="Dismiss publish error"
                >
                  ×
                </button>
              </div>
            )}

            <div className="flex-1 overflow-y-auto p-4" data-testid="thread-preview-scroll-container">
              {activeDraft && canShowReplyTarget && (
                <div className="max-w-xl mx-auto mb-4">
                  <ReplyTargetCard draft={activeDraft} />
                </div>
              )}
              <div className="max-w-xl mx-auto">
                <ThreadPreview content={doc} />
              </div>
            </div>
          </div>
        )}

        {activeView === "editor" && (
          <div className="flex-1 min-h-0 flex flex-col bg-panel overflow-hidden" data-testid="composer-editor-view">
            <div className="px-3 py-2 border-b border-border bg-panel flex items-center justify-between text-xs">
              <div className="flex items-center gap-2">
                <Button
                  variant="ghost"
                  size="xs"
                  onClick={() => setActiveView("preview")}
                  data-testid="back-to-preview-btn"
                  className="flex items-center gap-1.5 text-xs text-muted hover:text-foreground"
                >
                  <FontAwesomeIcon icon={faArrowLeft} className="w-3 h-3" />
                  <span>Back to Preview</span>
                </Button>
                <span className="text-muted/40">|</span>
                <span className="font-semibold text-foreground truncate max-w-[200px]">
                  {activeDraft.title || "Untitled Draft"}
                </span>
              </div>
              <button
                type="button"
                onClick={() => setIsDrawerOpen(true)}
                className="p-1.5 text-muted hover:text-foreground hover:bg-border/60 rounded-md transition cursor-pointer"
                data-testid="draft-hamburger-btn"
                aria-label="Open draft menu"
              >
                <FontAwesomeIcon icon={faBars} className="w-4 h-4" />
              </button>
            </div>
            <div ref={editorRef} tabIndex={-1} className="flex-1 min-h-0 overflow-hidden focus:outline-none">
              <EditorPanel />
            </div>
          </div>
        )}

        {activeView === "chat" && (
          <div className="flex-1 min-h-0 flex flex-col bg-panel overflow-hidden" data-testid="composer-chat-view">
            <div className="px-3 py-2 border-b border-border bg-panel flex items-center justify-between text-xs">
              <div className="flex items-center gap-2">
                <Button
                  variant="ghost"
                  size="xs"
                  onClick={() => setActiveView("preview")}
                  data-testid="back-to-preview-btn"
                  className="flex items-center gap-1.5 text-xs text-muted hover:text-foreground"
                >
                  <FontAwesomeIcon icon={faArrowLeft} className="w-3 h-3" />
                  <span>Back to Preview</span>
                </Button>
                <span className="text-muted/40">|</span>
                <span className="font-semibold text-foreground">AI Assistant Chat</span>
              </div>
              <button
                type="button"
                onClick={() => setIsDrawerOpen(true)}
                className="p-1.5 text-muted hover:text-foreground hover:bg-border/60 rounded-md transition cursor-pointer"
                data-testid="draft-hamburger-btn"
                aria-label="Open draft menu"
              >
                <FontAwesomeIcon icon={faBars} className="w-4 h-4" />
              </button>
            </div>
            <div ref={chatRef} tabIndex={-1} className="flex-1 min-h-0 overflow-hidden focus:outline-none">
              <ChatPanel />
            </div>
          </div>
        )}

        <DraftMenuDrawer
          isOpen={isDrawerOpen}
          onClose={() => setIsDrawerOpen(false)}
          activeView={activeView}
          onSelectView={(v) => setActiveView(v)}
          onOpenAdaptModal={() => setAdaptModalOpen(true)}
          workflowId={workflowId}
          canonicalDraftId={effectiveCanonicalId}
          activeDraftId={activeDraftId}
        />

        {adaptModalOpen && (
          <AdaptVariantsModal
            isOpen={adaptModalOpen}
            onClose={() => setAdaptModalOpen(false)}
            draftId={effectiveCanonicalId}
          />
        )}
      </div>
    </ScopedChatProvider>
  );
};

export const ComposerPage: React.FC<ComposerPageProps> = (props) => {
  const { workflowId, draftId, variantId } = useParams<{
    workflowId?: string;
    draftId?: string;
    variantId?: string;
  }>();
  const location = useLocation();
  const navigate = useNavigate();
  const createDraft = useDraftStore((s) => s.createDraft);
  const loadDrafts = useDraftStore((s) => s.loadDrafts);

  // Handle "/workflows/:workflowId/draft" and "/composer" creation redirects
  useEffect(() => {
    if (workflowId && !draftId && location.pathname.endsWith("/draft")) {
      void createDraft(undefined, undefined, undefined, undefined, workflowId).then((newDraft) => {
        navigate(`/workflows/${workflowId}/draft/${newDraft.id}`, { replace: true });
      });
      return;
    }

    if (location.pathname === "/composer") {
      void loadDrafts().then(() => {
        const state = useDraftStore.getState();
        if (state.drafts.length > 0) {
          navigate(`/draft/${state.drafts[0].id}`, { replace: true });
        } else {
          void createDraft().then((newDraft) => {
            navigate(`/draft/${newDraft.id}`, { replace: true });
          });
        }
      });
    }
  }, [workflowId, draftId, location.pathname, createDraft, loadDrafts, navigate]);

  const activeTargetId = variantId ?? draftId;

  if (!activeTargetId) {
    return (
      <div
        className="flex-1 min-h-0 flex items-center justify-center bg-bg text-xs font-mono text-muted gap-2"
        data-testid="composer-loading"
      >
        <FontAwesomeIcon icon={faSpinner} spin className="text-sm text-accent" />
        <span>initializing draft...</span>
      </div>
    );
  }

  return (
    <ComposerDraftProvider key={activeTargetId}>
      <ComposerViewContent {...props} />
    </ComposerDraftProvider>
  );
};

export { ComposerPage as ComposerView };
