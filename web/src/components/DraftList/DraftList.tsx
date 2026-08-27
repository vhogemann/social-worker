import React, { useMemo } from "react";
import { CreateDraftModal } from "./CreateDraftModal";
import { useDraftListManager } from "./useDraftListManager";
import { DraftListHeader } from "./DraftListHeader";
import { DraftListItem } from "./DraftListItem";
import { DraftVariantItem } from "./DraftVariantItem";
import { DraftListFooter } from "./DraftListFooter";

export const DraftList: React.FC = () => {
  const {
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
  } = useDraftListManager();

  const variantsByCanonical = useMemo(() => {
    const grouped = new Map<string, typeof variantDrafts>();
    for (const variant of variantDrafts) {
      if (!variant.canonicalDraftId) continue;
      const current = grouped.get(variant.canonicalDraftId) || [];
      grouped.set(variant.canonicalDraftId, [...current, variant]);
    }
    return grouped;
  }, [variantDrafts]);

  return (
    <div className="w-56 flex flex-col h-full bg-panel border-r border-border shrink-0" data-testid="draft-list">
      <DraftListHeader
        showArchived={showArchived}
        onOpenCreateModal={() => setCreateModalOpen(true)}
      />

      <div className="flex-1 overflow-y-auto divide-y divide-border">
        {canonicalDrafts.map((d) => {
          const variants = variantsByCanonical.get(d.id) || [];
          return (
            <div key={d.id}>
              <DraftListItem
                draft={d}
                isActive={d.id === activeDraftId}
                isEditing={editingTitleId === d.id}
                editTitleValue={editTitleValue}
                onSelect={handleSelect}
                onStartEditTitle={startEditTitle}
                onChangeEditTitle={setEditTitleValue}
                onSaveTitle={handleSaveTitle}
                onCancelEditTitle={() => setEditingTitleId(null)}
                onArchive={archiveDraft}
                onUnarchive={unarchiveDraft}
                onDelete={handleDelete}
              />
              {variants.length > 0 && (
                <div className="ml-3 border-l border-border/50">
                  {variants.map((v) => (
                    <DraftVariantItem
                      key={v.id}
                      variant={v}
                      isActive={v.id === activeDraftId}
                      onSelect={handleSelect}
                    />
                  ))}
                </div>
              )}
            </div>
          );
        })}
        {canonicalDrafts.length === 0 && (
          <div className="px-3 py-4 text-xs text-muted text-center font-mono">
            {showArchived ? "No archived drafts" : "No drafts yet"}
          </div>
        )}
      </div>

      <DraftListFooter
        showArchived={showArchived}
        onToggleShowArchived={() => setShowArchived(!showArchived)}
      />

      <CreateDraftModal
        isOpen={createModalOpen}
        onClose={() => setCreateModalOpen(false)}
        onCreate={handleNew}
      />
    </div>
  );
};