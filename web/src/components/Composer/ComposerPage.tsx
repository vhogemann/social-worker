import React, { useRef, useState } from "react";
import { Panel, PanelGroup, PanelResizeHandle } from "react-resizable-panels";
import { useHotkey } from "@tanstack/react-hotkeys";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faSpinner } from "@fortawesome/free-solid-svg-icons";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { ChatPanel } from "../ChatPanel/ChatPanel";
import { EditorPanel } from "../EditorPanel/EditorPanel";
import { DraftVariantTabs } from "./DraftVariantTabs";
import { AdaptVariantsModal } from "../EditorPanel/AdaptVariantsModal";
import { ComposerDraftProvider, useComposerDraft } from "./ComposerDraftContext";
import { ScopedChatProvider } from "../../api/chat";
import { useDraftStore } from "../../store/draftStore";

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
  } = useComposerDraft();

  const [adaptModalOpen, setAdaptModalOpen] = useState(false);

  const internalChatRef = useRef<HTMLDivElement>(null);
  const internalEditorRef = useRef<HTMLDivElement>(null);

  const chatRef = externalChatRef || internalChatRef;
  const editorRef = externalEditorRef || internalEditorRef;

  useHotkey("Mod+J", () => chatRef.current?.focus());
  useHotkey("Mod+K", () => editorRef.current?.focus());

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

  const effectiveCanonicalId = canonicalDraft?.id || canonicalDraftId || activeDraftId;
  const variants = canonicalDraft?.variants || [];

  return (
    <ScopedChatProvider
      draftId={activeDraftId}
      initialChatHistory={activeDraft.chatHistory}
      editorDoc={doc}
    >
      <div className="flex-1 min-h-0 flex flex-col relative" data-testid="composer-page">
        {/* Top Variant Tabs Level */}
        {effectiveCanonicalId && (
          <DraftVariantTabs
            workflowId={workflowId}
            canonicalDraftId={effectiveCanonicalId}
            activeDraftId={activeDraftId}
            variants={variants}
            onOpenAdaptModal={() => setAdaptModalOpen(true)}
          />
        )}

        {/* Main Full-Width Split View: Chat + Editor */}
        <div className="flex-1 min-h-0 flex">
          <PanelGroup direction="horizontal">
            <Panel defaultSize={45} minSize={25}>
              <div ref={chatRef} tabIndex={-1} className="h-full overflow-hidden focus:outline-none">
                <ChatPanel />
              </div>
            </Panel>
            <PanelResizeHandle className="w-1 bg-border hover:bg-accent transition-colors" />
            <Panel defaultSize={55} minSize={25}>
              <div ref={editorRef} tabIndex={-1} className="h-full overflow-hidden focus:outline-none">
                <EditorPanel />
              </div>
            </Panel>
          </PanelGroup>
        </div>

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
  React.useEffect(() => {
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
