import { useEffect, useState } from "react";
import { useDraftStore } from "../../store/draftStore";
import { useEditorStore } from "../../store/editorStore";
import { saveCurrentChat, restoreChat, clearChat } from "../../api/chat";
import { SettingsModal } from "../Settings/SettingsModal";

export function DraftList() {
  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const loadDrafts = useDraftStore((s) => s.loadDrafts);
  const createDraft = useDraftStore((s) => s.createDraft);
  const switchDraft = useDraftStore((s) => s.switchDraft);
  const saveDraftContent = useDraftStore((s) => s.saveDraftContent);
  const setDoc = useEditorStore((s) => s.setDoc);

  const archiveDraft = useDraftStore((s) => s.archiveDraft);
  const unarchiveDraft = useDraftStore((s) => s.unarchiveDraft);
  const deleteDraft = useDraftStore((s) => s.deleteDraft);
  const updateDraftTitle = useDraftStore((s) => s.updateDraftTitle);

  const [settingsOpen, setSettingsOpen] = useState(false);
  const [editingTitleId, setEditingTitleId] = useState<string | null>(null);
  const [editTitleValue, setEditTitleValue] = useState("");
  const [showArchived, setShowArchived] = useState(false);

  useEffect(() => {
    loadDrafts();
  }, []);

  const handleSelect = async (id: string) => {
    if (id === activeDraftId) return;
    if (activeDraftId) {
      saveCurrentChat(activeDraftId);
      await saveDraftContent(activeDraftId, useEditorStore.getState().doc);
    }
    const draft = await switchDraft(id);
    setDoc(draft.content ?? "");
    restoreChat(id);
  };

  const handleNew = async () => {
    if (activeDraftId) {
      saveCurrentChat(activeDraftId);
      await saveDraftContent(activeDraftId, useEditorStore.getState().doc);
    }
    const draft = await createDraft();
    setDoc(draft.content ?? "");
    restoreChat(draft.id);
  };

  const handleSaveTitle = async (id: string) => {
    setEditingTitleId(null);
    await updateDraftTitle(id, editTitleValue.trim());
  };

  const startEditTitle = (id: string, currentTitle: string) => {
    setEditingTitleId(id);
    setEditTitleValue(currentTitle);
  };

  const handleDelete = async (id: string) => {
    if (!confirm("Are you sure you want to delete this draft?")) return;

    clearChat(id);

    if (id === activeDraftId) {
      const remaining = drafts.filter((d) => d.id !== id && d.status !== "Deleted");
      await deleteDraft(id);

      if (remaining.length > 0) {
        const nextId = remaining[0].id;
        const draft = await switchDraft(nextId);
        setDoc(draft.content ?? "");
        restoreChat(nextId);
      } else {
        const draft = await createDraft();
        setDoc(draft.content ?? "");
        restoreChat(draft.id);
      }
    } else {
      await deleteDraft(id);
    }
  };

  const visibleDrafts = drafts.filter((d) => {
    if (d.status === "Deleted") return false;
    if (d.status === "Archived") return showArchived;
    return !showArchived;
  });

  return (
    <div className="w-56 flex flex-col h-full bg-panel border-r border-border shrink-0">
      <div className="px-3 py-2 border-b border-border flex items-center justify-between">
        <span className="text-xs font-mono text-muted uppercase tracking-wider">
          {showArchived ? "archived" : "drafts"}
        </span>
        {!showArchived && (
          <button
            onClick={handleNew}
            className="text-xs font-mono text-accent hover:opacity-80"
          >
            + new
          </button>
        )}
      </div>
      <div className="flex-1 overflow-y-auto divide-y divide-border">
        {visibleDrafts.map((d) => (
          <div
            key={d.id}
            className={`group relative w-full hover:bg-border/40 transition-colors ${
              d.id === activeDraftId ? "bg-border/40" : ""
            }`}
          >
            {d.id === activeDraftId && (
              <span className="absolute left-0 top-0 bottom-0 w-0.5 bg-accent" />
            )}
            <button
              onClick={() => handleSelect(d.id)}
              onDoubleClick={() => startEditTitle(d.id, d.title)}
              className="w-full text-left px-3 py-2 pr-12 focus:outline-none"
            >
              {editingTitleId === d.id ? (
                <input
                  type="text"
                  value={editTitleValue}
                  onChange={(e) => setEditTitleValue(e.target.value)}
                  onKeyDown={async (e) => {
                    if (e.key === "Enter") {
                      await handleSaveTitle(d.id);
                    } else if (e.key === "Escape") {
                      setEditingTitleId(null);
                    }
                  }}
                  onBlur={async () => {
                    await handleSaveTitle(d.id);
                  }}
                  className="bg-bg border border-accent rounded px-1 py-0.5 text-xs text-foreground focus:outline-none w-full font-sans"
                  autoFocus
                  onClick={(e) => e.stopPropagation()}
                />
              ) : (
                <>
                  <div className={`text-sm truncate ${d.id === activeDraftId ? "text-accent font-medium" : "font-medium"}`}>
                    {d.title}
                  </div>
                  <div className="text-xs font-mono text-muted mt-0.5 lowercase">
                    {d.status}
                    {d.threads && d.threads.length > 0 && (
                      <span className="text-[10px] text-zinc-500 dark:text-zinc-400 ml-1.5 font-sans normal-case">
                        ({d.threads.map((t) => t.platform).join(", ")})
                      </span>
                    )}
                  </div>
                </>
              )}
            </button>

            {/* Hover Actions Menu */}
            {editingTitleId !== d.id && (
              <div className="absolute right-2 top-1/2 -translate-y-1/2 hidden group-hover:flex items-center gap-1.5 bg-panel/95 py-0.5 px-1 rounded border border-border/80 shadow-sm">
                {d.status === "Archived" ? (
                  <>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        unarchiveDraft(d.id);
                      }}
                      title="Restore Draft"
                      className="text-muted hover:text-foreground"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5">
                        <path fillRule="evenodd" d="M15.312 11.424a5.5 5.5 0 0 1-9.201 2.466l-.012-.013c-.026-.027-.049-.056-.07-.086l-2.01-2.68A.75.75 0 0 1 4.62 10.2l2.01 2.68a3.5 3.5 0 0 0 5.61-1.39.75.75 0 0 1 1.458.261ZM3 9a6 6 0 0 1 10.9-3.4l.01.01c.026.028.05.057.072.088l2.01 2.68a.75.75 0 1 1-1.2.9l-2.01-2.68A4.5 4.5 0 0 0 4.5 9a.75.75 0 0 1-1.5 0Z" clipRule="evenodd" />
                      </svg>
                    </button>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDelete(d.id);
                      }}
                      title="Delete Draft"
                      className="text-red-400 hover:text-red-300"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5">
                        <path fillRule="evenodd" d="M8.75 1A2.75 2.75 0 0 0 6 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 1 0 .23 1.482l.149-.022.841 10.518A2.75 2.75 0 0 0 7.596 19h4.807a2.75 2.75 0 0 0 2.742-2.53l.841-10.52.149.023a.75.75 0 0 0 .23-1.482A41.03 41.03 0 0 0 14 4.193V3.75A2.75 2.75 0 0 0 11.25 1h-2.5ZM7.5 3.75A1.25 1.25 0 0 1 8.75 2.5h2.5A1.25 1.25 0 0 1 12.5 3.75v.44c-.76-.053-1.524-.09-2.292-.109A17.962 17.962 0 0 0 8.75 4.13v-.38Z" clipRule="evenodd" />
                      </svg>
                    </button>
                  </>
                ) : (
                  <>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        startEditTitle(d.id, d.title);
                      }}
                      title="Rename Draft"
                      className="text-muted hover:text-foreground"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5">
                        <path d="m5.433 13.917 1.262-3.155A4 4 0 0 1 7.58 9.42l6.92-6.918a2.121 2.121 0 0 1 3 3l-6.92 6.918c-.313.313-.679.56-1.081.727l-3.155 1.262A.5.5 0 0 1 5.83 14.17a.5.5 0 0 1-.397-.253ZM16.082 3.92a.621.621 0 0 0-.878-.002L8.52 10.602c-.156.156-.34.28-.541.363l-1.848.74.74-1.848c.083-.201.207-.385.363-.541l6.685-6.685a.621.621 0 0 0-.002-.878v.878Z" />
                        <path d="M15 15h.01a.75.75 0 1 1 0 1.5H3a.75.75 0 0 1 0-1.5h12Z" />
                      </svg>
                    </button>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        archiveDraft(d.id);
                      }}
                      title="Archive Draft"
                      className="text-muted hover:text-foreground"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5">
                        <path d="M2 3a1 1 0 0 1 1-1h14a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1V3Z" />
                        <path fillRule="evenodd" d="M3 7.75A.75.75 0 0 1 3.75 7h12.5a.75.75 0 0 1 .75.75v8.5a2.25 2.25 0 0 1-2.25 2.25H5.25A2.25 2.25 0 0 1 3 16.25v-8.5Zm5 3a.75.75 0 0 0 0 1.5h4a.75.75 0 0 0 0-1.5H8Z" clipRule="evenodd" />
                      </svg>
                    </button>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        handleDelete(d.id);
                      }}
                      title="Delete Draft"
                      className="text-red-400 hover:text-red-300"
                    >
                      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5">
                        <path fillRule="evenodd" d="M8.75 1A2.75 2.75 0 0 0 6 3.75v.443c-.795.077-1.584.176-2.365.298a.75.75 0 1 0 .23 1.482l.149-.022.841 10.518A2.75 2.75 0 0 0 7.596 19h4.807a2.75 2.75 0 0 0 2.742-2.53l.841-10.52.149.023a.75.75 0 0 0 .23-1.482A41.03 41.03 0 0 0 14 4.193V3.75A2.75 2.75 0 0 0 11.25 1h-2.5ZM7.5 3.75A1.25 1.25 0 0 1 8.75 2.5h2.5A1.25 1.25 0 0 1 12.5 3.75v.44c-.76-.053-1.524-.09-2.292-.109A17.962 17.962 0 0 0 8.75 4.13v-.38Z" clipRule="evenodd" />
                      </svg>
                    </button>
                  </>
                )}
              </div>
            )}
          </div>
        ))}
        {visibleDrafts.length === 0 && (
          <div className="px-3 py-4 text-xs text-muted text-center">
            {showArchived ? "No archived drafts" : "No drafts yet"}
          </div>
        )}
      </div>
      <div className="px-3 py-2 border-t border-border flex items-center justify-between shrink-0">
        <button
          onClick={() => setSettingsOpen(true)}
          className="text-xs font-mono text-muted hover:text-foreground focus:outline-none"
        >
          settings
        </button>
        <button
          onClick={() => setShowArchived(!showArchived)}
          className={`text-[10px] font-mono border px-1.5 py-0.5 rounded transition-all focus:outline-none ${
            showArchived
              ? "bg-accent/15 border-accent text-accent"
              : "border-border text-muted hover:text-foreground"
          }`}
        >
          {showArchived ? "show active" : "show archived"}
        </button>
      </div>
      <SettingsModal isOpen={settingsOpen} onClose={() => setSettingsOpen(false)} />
    </div>
  );
}