import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useDraftStore } from "../../store/draftStore";

export function useDraftListManager() {
  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const loadDrafts = useDraftStore((s) => s.loadDrafts);
  const createDraft = useDraftStore((s) => s.createDraft);

  const archiveDraft = useDraftStore((s) => s.archiveDraft);
  const unarchiveDraft = useDraftStore((s) => s.unarchiveDraft);
  const deleteDraft = useDraftStore((s) => s.deleteDraft);
  const updateDraftTitle = useDraftStore((s) => s.updateDraftTitle);

  const [editingTitleId, setEditingTitleId] = useState<string | null>(null);
  const [editTitleValue, setEditTitleValue] = useState("");
  const [showArchived, setShowArchived] = useState(false);
  const [createModalOpen, setCreateModalOpen] = useState(false);

  const navigate = useNavigate();

  useEffect(() => {
    loadDrafts();
  }, [loadDrafts]);

  const handleSelect = (id: string) => {
    if (id === activeDraftId) return;
    navigate(`/draft/${id}`);
  };

  const handleNew = async (title?: string, targetPlatform?: string, targetAccountId?: string) => {
    const draft = await createDraft(title, undefined, targetPlatform, targetAccountId);
    navigate(`/draft/${draft.id}`);
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

    if (id === activeDraftId) {
      const remaining = drafts.filter((d) => d.id !== id && d.status !== "Deleted");
      await deleteDraft(id);

      if (remaining.length > 0) {
        const nextId = remaining[0].id;
        navigate(`/draft/${nextId}`);
      } else {
        navigate("/");
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
