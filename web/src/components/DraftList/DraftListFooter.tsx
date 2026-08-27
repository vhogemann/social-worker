import React from "react";

export interface DraftListFooterProps {
  showArchived: boolean;
  onToggleShowArchived: () => void;
}

export const DraftListFooter: React.FC<DraftListFooterProps> = ({
  showArchived,
  onToggleShowArchived,
}) => {
  return (
    <div className="px-3 py-2 border-t border-border flex items-center justify-end shrink-0">
      <button
        onClick={onToggleShowArchived}
        className={`text-[10px] font-mono border px-1.5 py-0.5 rounded transition-all focus:outline-none ${
          showArchived
            ? "bg-accent/15 border-accent text-accent"
            : "border-border text-muted hover:text-foreground"
        }`}
      >
        {showArchived ? "show active" : "show archived"}
      </button>
    </div>
  );
};
