import { useEffect, useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlus, faTrashCan, faBoxArchive, faPenToSquare, faArrowRotateLeft, faLink, faGear } from "@fortawesome/free-solid-svg-icons";
import { faBluesky, faXTwitter, faLinkedin, faFacebook, faInstagram } from "@fortawesome/free-brands-svg-icons";
import { useDraftStore } from "../../store/draftStore";
import { useEditorStore } from "../../store/editorStore";
import { saveCurrentChat, restoreChat, clearChat } from "../../api/chat";
import { SettingsModal } from "../Settings/SettingsModal";
import { CreateDraftModal } from "./CreateDraftModal";

const PLATFORM_BADGE_COLORS: Record<string, string> = {
  Bluesky: "bg-sky-500/15 text-sky-600 dark:text-sky-400",
  Twitter: "bg-blue-400/15 text-blue-600 dark:text-blue-400",
  LinkedIn: "bg-blue-700/15 text-blue-800 dark:text-blue-300",
  Facebook: "bg-indigo-500/15 text-indigo-600 dark:text-indigo-400",
  Instagram: "bg-pink-500/15 text-pink-600 dark:text-pink-400",
};

const PLATFORM_ICONS: Record<string, any> = {
  Bluesky: faBluesky,
  Twitter: faXTwitter,
  LinkedIn: faLinkedin,
  Facebook: faFacebook,
  Instagram: faInstagram,
};

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
  const [createModalOpen, setCreateModalOpen] = useState(false);

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

  const handleNew = async (title?: string, targetPlatform?: string) => {
    if (activeDraftId) {
      saveCurrentChat(activeDraftId);
      await saveDraftContent(activeDraftId, useEditorStore.getState().doc);
    }
    const draft = await createDraft(title, undefined, targetPlatform);
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
        setCreateModalOpen(true);
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

  const canonicalDrafts = visibleDrafts.filter((d) => !d.canonicalDraftId);
  const variantDrafts = visibleDrafts.filter((d) => d.canonicalDraftId);

  return (
    <div className="w-56 flex flex-col h-full bg-panel border-r border-border shrink-0">
      <div className="px-3 py-2 border-b border-border flex items-center justify-between">
        <span className="text-xs font-mono text-muted uppercase tracking-wider">
          {showArchived ? "archived" : "drafts"}
        </span>
        {!showArchived && (
          <button
            onClick={() => setCreateModalOpen(true)}
            className="text-xs font-mono text-accent hover:opacity-80 flex items-center gap-1"
          >
            <FontAwesomeIcon icon={faPlus} className="w-3 h-3" />
            new
          </button>
        )}
      </div>
      <div className="flex-1 overflow-y-auto divide-y divide-border">
        {canonicalDrafts.map((d) => {
          const variants = variantDrafts.filter((v) => v.canonicalDraftId === d.id);
          return (
            <div key={d.id}>
              <div
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
                      <div className="flex items-center gap-1 mt-0.5">
                        <span className="text-xs font-mono text-muted lowercase">{d.status}</span>
                        {d.targetPlatform && (
                          <span className={`inline-flex items-center gap-1 text-[10px] px-1 py-0.5 rounded font-sans font-medium ${PLATFORM_BADGE_COLORS[d.targetPlatform] || "text-muted"}`}>
                            {PLATFORM_ICONS[d.targetPlatform] && <FontAwesomeIcon icon={PLATFORM_ICONS[d.targetPlatform]} className="w-3 h-3" />}
                            {d.targetPlatform}
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
                        <button onClick={(e) => { e.stopPropagation(); unarchiveDraft(d.id); }} title="Restore Draft" className="text-muted hover:text-foreground">
                          <FontAwesomeIcon icon={faArrowRotateLeft} className="w-3.5 h-3.5" />
                        </button>
                        <button onClick={(e) => { e.stopPropagation(); handleDelete(d.id); }} title="Delete Draft" className="text-red-400 hover:text-red-300">
                          <FontAwesomeIcon icon={faTrashCan} className="w-3.5 h-3.5" />
                        </button>
                      </>
                    ) : (
                      <>
                        <button onClick={(e) => { e.stopPropagation(); startEditTitle(d.id, d.title); }} title="Rename Draft" className="text-muted hover:text-foreground">
                          <FontAwesomeIcon icon={faPenToSquare} className="w-3.5 h-3.5" />
                        </button>
                        <button onClick={(e) => { e.stopPropagation(); archiveDraft(d.id); }} title="Archive Draft" className="text-muted hover:text-foreground">
                          <FontAwesomeIcon icon={faBoxArchive} className="w-3.5 h-3.5" />
                        </button>
                        <button onClick={(e) => { e.stopPropagation(); handleDelete(d.id); }} title="Delete Draft" className="text-red-400 hover:text-red-300">
                          <FontAwesomeIcon icon={faTrashCan} className="w-3.5 h-3.5" />
                        </button>
                      </>
                    )}
                  </div>
                )}
              </div>
              {variants.length > 0 && (
                <div className="ml-3 border-l border-border/50">
                  {variants.map((v) => (
                    <div
                      key={v.id}
                      className={`group relative w-full hover:bg-border/40 transition-colors ${
                        v.id === activeDraftId ? "bg-border/40" : ""
                      }`}
                    >
                      {v.id === activeDraftId && (
                        <span className="absolute left-0 top-0 bottom-0 w-0.5 bg-accent" />
                      )}
                      <button
                        onClick={() => handleSelect(v.id)}
                        className="w-full text-left px-3 py-1.5 pr-12 focus:outline-none"
                      >
                        <div className={`flex items-center gap-1 text-sm truncate ${v.id === activeDraftId ? "text-accent" : "text-foreground/70"}`}>
                          <FontAwesomeIcon icon={faLink} className="w-3 h-3 shrink-0 text-muted" />
                          <span className="truncate">{v.title}</span>
                        </div>
                        <div className="flex items-center gap-1 mt-0.5 ml-4">
                          {v.targetPlatform && (
                            <span className={`inline-flex items-center gap-1 text-[10px] px-1 py-0.5 rounded font-sans font-medium ${PLATFORM_BADGE_COLORS[v.targetPlatform] || "text-muted"}`}>
                              {PLATFORM_ICONS[v.targetPlatform] && <FontAwesomeIcon icon={PLATFORM_ICONS[v.targetPlatform]} className="w-3 h-3" />}
                              {v.targetPlatform}
                            </span>
                          )}
                          <span className="text-[10px] text-muted font-sans">variant</span>
                        </div>
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          );
        })}
        {canonicalDrafts.length === 0 && (
          <div className="px-3 py-4 text-xs text-muted text-center">
            {showArchived ? "No archived drafts" : "No drafts yet"}
          </div>
        )}
      </div>
      <div className="px-3 py-2 border-t border-border flex items-center justify-between shrink-0">
        <button
          onClick={() => setSettingsOpen(true)}
          className="text-xs font-mono text-muted hover:text-foreground focus:outline-none flex items-center gap-1"
        >
          <FontAwesomeIcon icon={faGear} className="w-3 h-3" />
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
      <CreateDraftModal
        isOpen={createModalOpen}
        onClose={() => setCreateModalOpen(false)}
        onCreate={handleNew}
      />
      <SettingsModal isOpen={settingsOpen} onClose={() => setSettingsOpen(false)} />
    </div>
  );
}