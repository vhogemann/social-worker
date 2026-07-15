import { useEffect, useMemo, useState } from "react";
import { useDraftStore } from "../../store/draftStore";
import { useEditorStore } from "../../store/editorStore";
import { saveCurrentChat, restoreChat, clearChat } from "../../api/chat";

export function useDraftListManager() {
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
  }, [loadDrafts]);

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

  const visibleDrafts = useMemo(
    () =>
      drafts.filter((d) => {
        if (d.status === "Deleted") return false;
        if (d.status === "Archived") return showArchived;
        return !showArchived;
      }),
    [drafts, showArchived],
  );

  const canonicalDrafts = useMemo(
    () => visibleDrafts.filter((d) => !d.canonicalDraftId),
    [visibleDrafts],
  );

  const variantDrafts = useMemo(
    () => visibleDrafts.filter((d) => d.canonicalDraftId),
    [visibleDrafts],
  );

  return {
    activeDraftId,
    archiveDraft,
    unarchiveDraft,
    settingsOpen,
    setSettingsOpen,
    editingTitleId,
    setEditingTitleId,
    editTitleValue,
    setEditTitleValue,
    showArchived,
    setShowArchived,
    createModalOpen,
    setCreateModalOpen,
    handleSelect,
    handleNew,
    handleSaveTitle,
    startEditTitle,
    handleDelete,
    canonicalDrafts,
    variantDrafts,
  };
}
