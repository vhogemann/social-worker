import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlus } from "@fortawesome/free-solid-svg-icons";

export interface DraftListHeaderProps {
  showArchived: boolean;
  onOpenCreateModal: () => void;
}

export const DraftListHeader: React.FC<DraftListHeaderProps> = ({
  showArchived,
  onOpenCreateModal,
}) => {
  return (
    <div className="px-3 py-2 border-b border-border flex items-center justify-between">
      <span className="text-xs font-mono text-muted uppercase tracking-wider">
        {showArchived ? "archived" : "drafts"}
      </span>
      {!showArchived && (
        <button
          onClick={onOpenCreateModal}
          className="text-xs font-mono text-accent hover:opacity-80 flex items-center gap-1 focus:outline-none"
        >
          <FontAwesomeIcon icon={faPlus} className="w-3 h-3" />
          <span>new</span>
        </button>
      )}
    </div>
  );
};
