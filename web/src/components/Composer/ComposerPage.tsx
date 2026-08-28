import React, { useRef, useState } from "react";
import { Panel, PanelGroup, PanelResizeHandle } from "react-resizable-panels";
import { useHotkey } from "@tanstack/react-hotkeys";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faSpinner } from "@fortawesome/free-solid-svg-icons";
import { ChatPanel } from "../ChatPanel/ChatPanel";
import { EditorPanel } from "../EditorPanel/EditorPanel";
import { DraftVariantTabs } from "./DraftVariantTabs";
import { AdaptVariantsModal } from "../EditorPanel/AdaptVariantsModal";
import { useComposerDraftSync } from "./useComposerDraftSync";
import { useDraftStore } from "../../store/draftStore";

export interface ComposerPageProps {
  chatRef?: React.RefObject<HTMLDivElement | null>;
  editorRef?: React.RefObject<HTMLDivElement | null>;
}

export const ComposerPage: React.FC<ComposerPageProps> = ({
  chatRef: externalChatRef,
  editorRef: externalEditorRef,
}) => {
  const { isReady, workflowId, canonicalDraftId, activeDraftId } = useComposerDraftSync();
  const [adaptModalOpen, setAdaptModalOpen] = useState(false);

  const drafts = useDraftStore((s) => s.drafts);

  const internalChatRef = useRef<HTMLDivElement>(null);
  const internalEditorRef = useRef<HTMLDivElement>(null);

  const chatRef = externalChatRef || internalChatRef;
  const editorRef = externalEditorRef || internalEditorRef;

  useHotkey("Mod+J", () => chatRef.current?.focus());
  useHotkey("Mod+K", () => editorRef.current?.focus());

  if (!isReady) {
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

  // Identify canonical draft and variants
  const activeId = activeDraftId || "";
  let canonicalDraft = drafts.find(
    (d) => d.id === canonicalDraftId || (d.variants && d.variants.some((v) => v.id === activeId))
  );
  if (!canonicalDraft && activeId) {
    canonicalDraft = drafts.find((d) => d.id === activeId);
  }

  const effectiveCanonicalId = canonicalDraft?.canonicalDraftId || canonicalDraft?.id || activeId;
  const variants = canonicalDraft?.variants || [];

  return (
    <div className="flex-1 min-h-0 flex flex-col relative" data-testid="composer-page">
      {/* Top Variant Tabs Level */}
      {effectiveCanonicalId && (
        <DraftVariantTabs
          workflowId={workflowId}
          canonicalDraftId={effectiveCanonicalId}
          activeDraftId={activeId}
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
  );
};

export { ComposerPage as ComposerView };
