import { useState, useMemo, useRef, useEffect } from "react";
import CodeMirror from "@uiw/react-codemirror";
import { EditorView, keymap } from "@codemirror/view";
import { defaultKeymap, history, historyKeymap } from "@codemirror/commands";
import { searchKeymap, highlightSelectionMatches } from "@codemirror/search";
import { markdown, markdownLanguage } from "@codemirror/lang-markdown";
import { vim, getCM } from "@replit/codemirror-vim";
import { useEditorStore } from "../../store/editorStore";
import { useDraftStore } from "../../store/draftStore";

function VimStatus({ view }: { view: EditorView | null }) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!view) return;
    const cm = getCM(view);
    if (!cm) return;
    const update = () => {
      if (ref.current) {
        const mode = (cm as unknown as { state?: { vim?: { mode?: string } } }).state?.vim?.mode;
        ref.current.textContent = mode ? `--${mode.toUpperCase()}--` : "";
      }
    };
    cm.on("vim-mode-change", update);
    update();
    return () => cm.off("vim-mode-change", update);
  }, [view]);

  return (
    <div
      ref={ref}
      className="absolute top-2 right-3 px-2 py-0.5 text-xs font-mono text-accent bg-panel/80 rounded border border-border z-10"
    />
  );
}

export function MarkdownEditor() {
  const doc = useEditorStore((s) => s.doc);
  const version = useEditorStore((s) => s.version);
  const setDoc = useEditorStore((s) => s.setDoc);
  const viewRef = useRef<EditorView | null>(null);

  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const saveDraftContent = useDraftStore((s) => s.saveDraftContent);
  const uploadFileSource = useDraftStore((s) => s.uploadFileSource);
  const uploadMediaAsset = useDraftStore((s) => s.uploadMediaAsset);

  const [isDragging, setIsDragging] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const extensions = useMemo(
    () => [
      vim(),
      history(),
      markdown({ base: markdownLanguage }),
      highlightSelectionMatches(),
      EditorView.lineWrapping,
      keymap.of([...defaultKeymap, ...historyKeymap, ...searchKeymap]),
    ],
    []
  );

  useEffect(() => {
    if (viewRef.current && version > 0) {
      const view = viewRef.current;
      const current = view.state.doc.toString();
      if (current !== doc) {
        view.dispatch({
          changes: { from: 0, to: current.length, insert: doc },
        });
      }
    }
  }, [version, doc]);

  const drafts = useDraftStore((s) => s.drafts);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const isLocked = activeDraft?.status === "Sourcing" || activeDraft?.status === "Formatting";

  useEffect(() => {
    if (!activeDraftId || !doc || isLocked) return;
    const timer = setTimeout(async () => {
      await saveDraftContent(activeDraftId, doc);
    }, 1500);

    return () => clearTimeout(timer);
  }, [doc, activeDraftId, saveDraftContent, isLocked]);

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault();
    if (!isLocked) setIsDragging(true);
  };

  const handleDragLeave = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  };

  const handleDrop = async (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    if (isLocked || !activeDraftId) return;

    const file = e.dataTransfer.files?.[0];
    if (!file) return;

    const ext = file.name.split(".").pop()?.toLowerCase();
    const isImage = ["jpg", "jpeg", "png", "webp", "gif"].includes(ext || "");
    const isDoc = ["pdf", "txt", "md"].includes(ext || "");

    if (!isImage && !isDoc) {
      setErrorMsg("Unsupported file type. Supported types: JPG, PNG, WEBP, GIF, PDF, TXT, MD.");
      const timer = setTimeout(() => setErrorMsg(null), 5000);
      return;
    }

    try {
      let markdownToInsert = "";
      if (isImage) {
        const res = await uploadMediaAsset(activeDraftId, file);
        markdownToInsert = res.markdownTag;
      } else {
        const res = await uploadFileSource(activeDraftId, file);
        markdownToInsert = res.markdownLink;
      }

      const view = viewRef.current;
      if (view) {
        const state = view.state;
        const selection = state.selection.main;
        view.dispatch({
          changes: { from: selection.from, to: selection.to, insert: markdownToInsert },
          selection: { anchor: selection.from + markdownToInsert.length }
        });
        setDoc(view.state.doc.toString());
      } else {
        const separator = doc ? "\n\n" : "";
        setDoc(`${doc}${separator}${markdownToInsert}`);
      }
    } catch (err: any) {
      setErrorMsg(err.message || "Failed to upload file");
      setTimeout(() => setErrorMsg(null), 5000);
    }
  };

  const handlePaste = async (e: React.ClipboardEvent) => {
    if (isLocked || !activeDraftId) return;
    const items = e.clipboardData?.items;
    if (!items) return;

    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.type.indexOf("image") === 0) {
        const file = item.getAsFile();
        if (!file) continue;

        e.preventDefault();

        try {
          const res = await uploadMediaAsset(activeDraftId, file);
          const view = viewRef.current;
          if (view) {
            const state = view.state;
            const selection = state.selection.main;
            view.dispatch({
              changes: { from: selection.from, to: selection.to, insert: res.markdownTag },
              selection: { anchor: selection.from + res.markdownTag.length }
            });
            setDoc(view.state.doc.toString());
          } else {
            const separator = doc ? "\n\n" : "";
            setDoc(`${doc}${separator}${res.markdownTag}`);
          }
        } catch (err: any) {
          setErrorMsg(err.message || "Failed to upload image from clipboard");
          setTimeout(() => setErrorMsg(null), 5000);
        }
      }
    }
  };

  return (
    <div
      className={`relative h-full transition-all ${
        isDragging ? "bg-indigo-600/5 ring-2 ring-dashed ring-indigo-500" : ""
      }`}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
      onPaste={handlePaste}
    >
      <VimStatus view={viewRef.current} />
      {errorMsg && (
        <div className="absolute top-2 left-3 right-3 bg-red-500/10 border border-red-500/20 text-red-600 dark:text-red-400 text-xs px-3 py-2 rounded-xl z-20 shadow-lg flex items-center justify-between transition-all select-none">
          <span className="truncate pr-4">{errorMsg}</span>
          <button onClick={() => setErrorMsg(null)} className="text-red-400 hover:text-red-600 transition text-[10px] font-bold">
            ✕
          </button>
        </div>
      )}
      {isLocked && (
        <div className="absolute inset-0 bg-zinc-950/40 backdrop-blur-[1px] flex flex-col items-center justify-center z-20 transition-all select-none">
          <div className="bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-2xl p-6 shadow-xl max-w-sm text-center flex flex-col items-center gap-4">
            <div className="w-10 h-10 border-4 border-indigo-600/30 border-t-indigo-600 rounded-full animate-spin" />
            <div>
              <h3 className="font-semibold text-zinc-950 dark:text-zinc-50 text-sm">
                {activeDraft?.status === "Sourcing" ? "📁 Fetching Sources..." : "⚙️ Formatting Thread..."}
              </h3>
              <p className="text-xs text-zinc-500 mt-1 leading-relaxed">
                The draft is temporarily locked while background metadata operations are in progress.
              </p>
            </div>
          </div>
        </div>
      )}
      <CodeMirror
        value={doc}
        height="100%"
        theme="dark"
        editable={!isLocked}
        extensions={extensions}
        onCreateEditor={(view) => {
          viewRef.current = view;
        }}
        onChange={(value) => setDoc(value)}
        basicSetup={{
          lineNumbers: false,
          foldGutter: false,
          highlightActiveLine: true,
          highlightActiveLineGutter: false,
          autocompletion: false,
          searchKeymap: false,
        }}
      />
    </div>
  );
}
