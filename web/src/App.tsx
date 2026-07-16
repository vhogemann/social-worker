import { useEffect, useRef, useState } from "react";
import { Panel, PanelGroup, PanelResizeHandle } from "react-resizable-panels";
import { useHotkey } from "@tanstack/react-hotkeys";
import { BrowserRouter, Routes, Route, Link } from "react-router-dom";
import { ChatProvider, restoreChat } from "./api/chat";
import { ChatPanel } from "./components/ChatPanel/ChatPanel";
import { EditorPanel } from "./components/EditorPanel/EditorPanel";
import { DraftList } from "./components/DraftList/DraftList";
import { SourcesLibrary } from "./components/SourcesLibrary/SourcesLibrary";
import { FeedsPanel } from "./components/FeedsPanel/FeedsPanel";
import { useDraftStore } from "./store/draftStore";
import { useEditorStore } from "./store/editorStore";

function ComposerView({
  chatRef,
  editorRef,
}: {
  chatRef: React.RefObject<HTMLDivElement>;
  editorRef: React.RefObject<HTMLDivElement>;
}) {
  return (
    <div className="flex-1 min-h-0 flex">
      <DraftList />
      <div className="flex-1 min-w-0">
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
    </div>
  );
}

function AppContent() {
  const chatRef = useRef<HTMLDivElement>(null);
  const editorRef = useRef<HTMLDivElement>(null);
  const loadDrafts = useDraftStore((s) => s.loadDrafts);
  const setDoc = useEditorStore((s) => s.setDoc);
  const [isReady, setIsReady] = useState(false);

  useEffect(() => {
    let cancelled = false;

    void loadDrafts().then(async () => {
      const state = useDraftStore.getState();
      if (state.drafts.length > 0) {
        const first = state.drafts[0];
        useDraftStore.setState({ activeDraftId: first.id });
        setDoc(first.content ?? "");
        restoreChat(first.id);
      } else {
        const draft = await state.createDraft();
        setDoc(draft.content ?? "");
        restoreChat(draft.id);
      }

      if (!cancelled) {
        setIsReady(true);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [loadDrafts, setDoc]);

  useHotkey("Mod+J", () => chatRef.current?.focus());
  useHotkey("Mod+K", () => editorRef.current?.focus());

  if (!isReady) {
    return (
      <div className="h-screen flex flex-col overflow-hidden">
        <header className="shrink-0 px-4 py-2 border-b border-border text-xs font-mono text-muted">
          social-worker <span className="text-accent">·</span> mvp
        </header>
        <div className="flex-1 flex items-center justify-center text-xs font-mono text-muted">
          loading draft...
        </div>
      </div>
    );
  }

  return (
    <div className="h-screen flex flex-col overflow-hidden">
      <header className="shrink-0 px-4 py-2 border-b border-border text-xs font-mono text-muted flex items-center justify-between">
        <div>
          social-worker <span className="text-accent">·</span> mvp
        </div>
        <div className="flex gap-4">
          <Link to="/" className="hover:text-foreground">Composer</Link>
          <Link to="/sources" className="hover:text-foreground">Sources Library</Link>
          <Link to="/feeds" className="hover:text-foreground">Feeds</Link>
        </div>
      </header>
      <Routes>
        <Route path="/" element={<ComposerView chatRef={chatRef} editorRef={editorRef} />} />
        <Route path="/sources" element={<SourcesLibrary />} />
        <Route path="/feeds" element={<FeedsPanel />} />
      </Routes>
    </div>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <ChatProvider>
        <AppContent />
      </ChatProvider>
    </BrowserRouter>
  );
}