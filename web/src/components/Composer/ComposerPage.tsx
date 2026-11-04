import React, { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useHotkey } from "@tanstack/react-hotkeys";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faSpinner,
  faArrowLeft,
  faEye,
  faPenToSquare,
  faComments,
  faLink,
  faCheck,
  faLayerGroup,
  faFolderOpen
} from "@fortawesome/free-solid-svg-icons";
import { ChatPanel } from "../ChatPanel/ChatPanel";
import { EditorPanel } from "../EditorPanel/EditorPanel";
import { DraftVariantTabs } from "./DraftVariantTabs";
import { AdaptVariantsModal } from "../EditorPanel/AdaptVariantsModal";
import { ComposerDraftProvider, useComposerDraft } from "./ComposerDraftContext";
import { ScopedChatProvider } from "../../api/chat";
import { useDraftStore } from "../../store/draftStore";
import { DraftPreviewHeader } from "./DraftPreviewHeader";
import { ThreadPreview } from "../EditorPanel/ThreadPreview";
import { ReplyTargetCard } from "../EditorPanel/ReplyTargetCard";
import { Button } from "../common/Button";
import { enqueueQueueItem } from "../../api/schedules";
import { PLATFORM_ICONS } from "../../config/platformBadges";

export interface ComposerPageProps {
  chatRef?: React.RefObject<HTMLDivElement | null>;
  editorRef?: React.RefObject<HTMLDivElement | null>;
}

const ComposerViewContent: React.FC<ComposerPageProps> = ({
  chatRef: externalChatRef,
  editorRef: externalEditorRef
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
    setPanelMode
  } = useComposerDraft();

  const [activeView, setActiveView] = useState<"preview" | "editor" | "chat">("preview");
  const [adaptModalOpen, setAdaptModalOpen] = useState(false);
  const [isPublishing, setIsPublishing] = useState(false);
  const [publishError, setPublishError] = useState<string | null>(null);
  const [scheduleNotice, setScheduleNotice] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

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
          : [activeDraft, ...s.drafts]
      }));
    }
  }, [activeDraft]);

  useHotkey("Mod+J", () => {
    setActiveView("chat");
    setTimeout(() => chatRef.current?.focus(), 50);
  });

  useHotkey("Mod+K", () => {
    setPanelMode?.("edit");
    setActiveView("editor");
    setTimeout(() => editorRef.current?.focus(), 50);
  });

  const handleCopyLink = async () => {
    try {
      await navigator.clipboard.writeText(window.location.href);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {}
  };

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
        throw new Error(
          result.errorMessage || (result as any).ErrorMessage || "Failed to publish thread."
        );
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

  return (
    <ScopedChatProvider
      draftId={activeDraftId}
      initialChatHistory={activeDraft.chatHistory}
      editorDoc={doc}
    >
      <div
        className="flex-1 min-h-0 h-full flex overflow-hidden font-sans bg-bg"
        data-testid="composer-page"
      >
        {/* Sidebar navigation (Matching SettingsPage layout) */}
        <aside
          className="w-64 bg-panel border-r border-border p-4 flex flex-col justify-between select-none shrink-0 overflow-y-auto"
          data-testid="draft-sidebar"
        >
          <div className="space-y-6">
            {/* Back link */}
            <div>
              <Link
                to={workflowId ? `/workflows/${workflowId}` : "/drafts"}
                className="inline-flex items-center gap-1.5 text-xs font-mono text-muted hover:text-foreground transition-colors"
                data-testid="draft-back-link"
              >
                <FontAwesomeIcon icon={faArrowLeft} className="w-3 h-3" />
                <span>{workflowId ? "Back to Workflow" : "Back to Drafts"}</span>
              </Link>
              <div className="mt-3 px-1">
                <div className="text-[10px] font-mono uppercase tracking-wider text-muted">
                  Draft Studio
                </div>
                <div
                  className="text-sm font-bold text-foreground truncate mt-0.5"
                  title={activeDraft.title}
                >
                  {activeDraft.title || "Untitled Draft"}
                </div>
              </div>
            </div>

            {/* Navigation sections */}
            <div className="space-y-4">
              {/* Views */}
              <div>
                <div className="px-1 text-[10px] font-mono uppercase tracking-wider text-muted/80 font-semibold mb-2">
                  Views
                </div>
                <nav className="space-y-1">
                  <button
                    type="button"
                    data-testid="menu-view-preview"
                    onClick={() => setActiveView("preview")}
                    className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-mono transition-colors cursor-pointer ${
                      activeView === "preview"
                        ? "bg-accent/15 text-accent font-medium border border-accent/30"
                        : "text-muted hover:text-foreground hover:bg-border/20 border border-transparent"
                    }`}
                  >
                    <div className="flex items-center gap-2.5">
                      <FontAwesomeIcon icon={faEye} className="w-3.5 h-3.5 shrink-0" />
                      <span>Thread Preview</span>
                    </div>
                    {activeView === "preview" && (
                      <span className="text-[10px] font-mono uppercase px-1.5 py-0.5 rounded bg-accent/20 text-accent font-semibold">
                        Active
                      </span>
                    )}
                  </button>

                  <button
                    type="button"
                    data-testid="menu-view-editor"
                    onClick={() => {
                      setPanelMode?.("edit");
                      setActiveView("editor");
                    }}
                    className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-mono transition-colors cursor-pointer ${
                      activeView === "editor"
                        ? "bg-accent/15 text-accent font-medium border border-accent/30"
                        : "text-muted hover:text-foreground hover:bg-border/20 border border-transparent"
                    }`}
                  >
                    <div className="flex items-center gap-2.5">
                      <FontAwesomeIcon icon={faPenToSquare} className="w-3.5 h-3.5 shrink-0" />
                      <span>Markdown Editor</span>
                    </div>
                    {activeView === "editor" && (
                      <span className="text-[10px] font-mono uppercase px-1.5 py-0.5 rounded bg-accent/20 text-accent font-semibold">
                        Active
                      </span>
                    )}
                  </button>

                  <button
                    type="button"
                    data-testid="menu-view-chat"
                    onClick={() => setActiveView("chat")}
                    className={`w-full flex items-center justify-between px-3 py-2 rounded-lg text-xs font-mono transition-colors cursor-pointer ${
                      activeView === "chat"
                        ? "bg-accent/15 text-accent font-medium border border-accent/30"
                        : "text-muted hover:text-foreground hover:bg-border/20 border border-transparent"
                    }`}
                  >
                    <div className="flex items-center gap-2.5">
                      <FontAwesomeIcon icon={faComments} className="w-3.5 h-3.5 shrink-0" />
                      <span>AI Assistant Chat</span>
                    </div>
                    {activeView === "chat" && (
                      <span className="text-[10px] font-mono uppercase px-1.5 py-0.5 rounded bg-accent/20 text-accent font-semibold">
                        Active
                      </span>
                    )}
                  </button>
                </nav>
              </div>

              {/* Actions */}
              <div>
                <div className="px-1 text-[10px] font-mono uppercase tracking-wider text-muted/80 font-semibold mb-2">
                  Actions
                </div>
                <nav className="space-y-1">
                  <button
                    type="button"
                    data-testid="menu-copy-draft-link"
                    onClick={handleCopyLink}
                    className="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-mono text-muted hover:text-foreground hover:bg-border/20 transition-colors cursor-pointer"
                  >
                    <FontAwesomeIcon
                      icon={copied ? faCheck : faLink}
                      className={`w-3.5 h-3.5 shrink-0 ${copied ? "text-emerald-400" : ""}`}
                    />
                    <span>{copied ? "Link Copied!" : "Copy Draft Link"}</span>
                  </button>

                  <button
                    type="button"
                    data-testid="menu-adapt-platforms"
                    onClick={() => setAdaptModalOpen(true)}
                    className="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs font-mono text-muted hover:text-foreground hover:bg-border/20 transition-colors cursor-pointer"
                  >
                    <FontAwesomeIcon icon={faLayerGroup} className="w-3.5 h-3.5 shrink-0" />
                    <span>Adapt for Platforms</span>
                  </button>
                </nav>
              </div>
            </div>
          </div>

          {/* Sidebar Footer */}
          <div className="space-y-3 pt-4 border-t border-border">
            <div className="px-3 py-2 border border-border rounded-lg bg-bg/40 text-xs font-mono">
              <div className="text-[10px] text-muted uppercase tracking-wider mb-0.5">
                Target Platform
              </div>
              <div className="truncate text-foreground font-sans font-medium flex items-center gap-1.5">
                {activeDraft.targetPlatform && PLATFORM_ICONS[activeDraft.targetPlatform] && (
                  <FontAwesomeIcon
                    icon={PLATFORM_ICONS[activeDraft.targetPlatform]}
                    className="w-3.5 h-3.5 text-accent"
                  />
                )}
                <span>{activeDraft.targetPlatform || "Generic Thread"}</span>
              </div>
            </div>
            <Link
              to="/drafts"
              data-testid="menu-nav-drafts"
              className="w-full text-left px-3 py-2 text-xs font-mono text-muted hover:text-foreground hover:bg-border/20 border border-border rounded-lg transition-colors flex items-center justify-between"
            >
              <span>Drafts Library</span>
              <FontAwesomeIcon icon={faFolderOpen} className="w-3.5 h-3.5" />
            </Link>
          </div>
        </aside>

        {/* Main Content Area */}
        <main className="flex-1 flex flex-col h-full overflow-hidden bg-bg">
          {activeView === "preview" && (
            <div
              className="flex-1 min-h-0 flex flex-col overflow-hidden"
              data-testid="composer-preview-view"
            >
              <DraftPreviewHeader
                title={activeDraft.title}
                targetPlatform={activeDraft.targetPlatform}
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
                      {primaryThread?.lastPublishError
                        ? ` Error: ${primaryThread.lastPublishError}`
                        : ""}
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

              <div
                className="flex-1 overflow-y-auto p-4"
                data-testid="thread-preview-scroll-container"
              >
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
            <div
              className="flex-1 min-h-0 h-full flex flex-col bg-panel overflow-hidden"
              data-testid="composer-editor-view"
            >
              <div className="px-4 py-2.5 border-b border-border bg-panel flex items-center justify-between text-xs shrink-0">
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
              </div>
              <div
                ref={editorRef}
                tabIndex={-1}
                className="flex-1 min-h-0 h-full flex flex-col overflow-hidden focus:outline-none"
              >
                <EditorPanel />
              </div>
            </div>
          )}

          {activeView === "chat" && (
            <div
              className="flex-1 min-h-0 flex flex-col bg-panel overflow-hidden"
              data-testid="composer-chat-view"
            >
              <div className="px-4 py-2.5 border-b border-border bg-panel flex items-center justify-between text-xs">
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
              </div>
              <div
                ref={chatRef}
                tabIndex={-1}
                className="flex-1 min-h-0 overflow-hidden focus:outline-none"
              >
                <ChatPanel />
              </div>
            </div>
          )}
        </main>

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
  const { draftId } = useParams<{ draftId?: string }>();
  return (
    <ComposerDraftProvider key={draftId}>
      <ComposerViewContent {...props} />
    </ComposerDraftProvider>
  );
};
