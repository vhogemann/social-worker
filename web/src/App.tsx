import { useEffect, useRef, useState } from "react";
import { Panel, PanelGroup, PanelResizeHandle } from "react-resizable-panels";
import { useHotkey } from "@tanstack/react-hotkeys";
import { BrowserRouter, Routes, Route, Link, useNavigate, useParams, useLocation } from "react-router-dom";
import { ChatProvider } from "./api/chat";
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
  const applyExternal = useEditorStore((s) => s.applyExternal);
  const [isReady, setIsReady] = useState(false);

  const navigate = useNavigate();
  const location = useLocation();
  const match = location.pathname.match(/\/draft\/([^/]+)/);
  const draftId = match ? match[1] : null;

  // 1. Load drafts once on mount and initialize active draft if path matches
  useEffect(() => {
    let cancelled = false;
    void loadDrafts().then(() => {
      if (cancelled) return;
      
      const state = useDraftStore.getState();
      const match = window.location.pathname.match(/\/draft\/([^/]+)/);
      const initialDraftId = match ? match[1] : null;

      if (initialDraftId) {
        const draft = state.drafts.find((d) => d.id === initialDraftId);
        if (draft) {
          useDraftStore.setState({ activeDraftId: initialDraftId });
          applyExternal(draft.content ?? "");
        }
      }

      setIsReady(true);
    });
    return () => {
      cancelled = true;
    };
  }, [loadDrafts, applyExternal]);

  // 2. Redirect from "/" or "/index.html" to the active or first draft once loaded
  useEffect(() => {
    if (!isReady) return;

    if (location.pathname === "/" || location.pathname === "/index.html") {
      const state = useDraftStore.getState();
      let targetId = state.activeDraftId;
      if (!targetId && state.drafts.length > 0) {
        targetId = state.drafts[0].id;
      }
      if (!targetId) {
        void state.createDraft().then((draft) => {
          navigate(`/draft/${draft.id}`, { replace: true });
        });
      } else {
        navigate(`/draft/${targetId}`, { replace: true });
      }
    }
  }, [isReady, location.pathname, navigate]);

  // 3. Synchronize draft active state when draftId parameter in route changes
  useEffect(() => {
    if (!isReady || !draftId) return;

    const state = useDraftStore.getState();
    const draft = state.drafts.find((d) => d.id === draftId);
    if (draft) {
      useDraftStore.setState({ activeDraftId: draftId });
      applyExternal(draft.content ?? "");
    } else {
      void useDraftStore.getState().switchDraft(draftId).then((loaded) => {
        applyExternal(loaded.content ?? "");
      }).catch(() => {
        navigate("/", { replace: true });
      });
    }
  }, [draftId, isReady, applyExternal, navigate]);

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
        <Route path="/draft/:draftId" element={<ComposerView chatRef={chatRef} editorRef={editorRef} />} />
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