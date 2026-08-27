import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faTrashCan,
  faBoxArchive,
  faPenToSquare,
  faArrowRotateLeft,
} from "@fortawesome/free-solid-svg-icons";
import { PLATFORM_BADGE_COLORS, PLATFORM_ICONS } from "../../config/platformBadges";
import type { DraftDto } from "../../api/drafts";

export interface DraftListItemProps {
  draft: DraftDto;
  isActive: boolean;
  isEditing: boolean;
  editTitleValue: string;
  onSelect: (id: string) => void;
  onStartEditTitle: (id: string, title: string) => void;
  onChangeEditTitle: (val: string) => void;
  onSaveTitle: (id: string) => void;
  onCancelEditTitle: () => void;
  onArchive: (id: string) => void;
  onUnarchive: (id: string) => void;
  onDelete: (id: string) => void;
}

export const DraftListItem: React.FC<DraftListItemProps> = ({
  draft,
  isActive,
  isEditing,
  editTitleValue,
  onSelect,
  onStartEditTitle,
  onChangeEditTitle,
  onSaveTitle,
  onCancelEditTitle,
  onArchive,
  onUnarchive,
  onDelete,
}) => {
  return (
    <div
      className={`group relative w-full hover:bg-border/40 transition-colors ${
        isActive ? "bg-border/40" : ""
      }`}
      data-testid="draft-list-item"
    >
      {isActive && (
        <span className="absolute left-0 top-0 bottom-0 w-0.5 bg-accent" />
      )}
      <button
        onClick={() => onSelect(draft.id)}
        onDoubleClick={() => onStartEditTitle(draft.id, draft.title)}
        className="w-full text-left px-3 py-2 pr-12 focus:outline-none"
      >
        {isEditing ? (
          <input
            type="text"
            value={editTitleValue}
            onChange={(e) => onChangeEditTitle(e.target.value)}
            onKeyDown={async (e) => {
              if (e.key === "Enter") {
                onSaveTitle(draft.id);
              } else if (e.key === "Escape") {
                onCancelEditTitle();
              }
            }}
            onBlur={() => onSaveTitle(draft.id)}
            className="bg-bg border border-accent rounded px-1 py-0.5 text-xs text-foreground focus:outline-none w-full font-sans"
            autoFocus
            onClick={(e) => e.stopPropagation()}
          />
        ) : (
          <>
            <div
              className={`text-sm truncate ${
                isActive ? "text-accent font-medium" : "font-medium"
              }`}
            >
              {draft.title}
            </div>
            <div className="flex items-center gap-1 mt-0.5">
              <span className="text-xs font-mono text-muted lowercase">
                {draft.status}
              </span>
              {draft.targetPlatform && (
                <span
                  className={`inline-flex items-center gap-1 text-[10px] px-1 py-0.5 rounded font-sans font-medium ${
                    PLATFORM_BADGE_COLORS[draft.targetPlatform] || "text-muted"
                  }`}
                >
                  {PLATFORM_ICONS[draft.targetPlatform] && (
                    <FontAwesomeIcon
                      icon={PLATFORM_ICONS[draft.targetPlatform]}
                      className="w-3 h-3"
                    />
                  )}
                  {draft.targetPlatform}
                  {draft.targetAccountHandle && (
                    <span className="opacity-75 font-mono">
                      @{draft.targetAccountHandle.replace(/^@/, "")}
                    </span>
                  )}
                </span>
              )}
            </div>
          </>
        )}
      </button>
      {!isEditing && (
        <div className="absolute right-2 top-1/2 -translate-y-1/2 hidden group-hover:flex items-center gap-1.5 bg-panel/95 py-0.5 px-1 rounded border border-border/80 shadow-sm">
          {draft.status === "Archived" ? (
            <>
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onUnarchive(draft.id);
                }}
                title="Restore Draft"
                className="text-muted hover:text-foreground p-0.5"
              >
                <FontAwesomeIcon icon={faArrowRotateLeft} className="w-3.5 h-3.5" />
              </button>
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onDelete(draft.id);
                }}
                title="Delete Draft"
                className="text-red-400 hover:text-red-300 p-0.5"
              >
                <FontAwesomeIcon icon={faTrashCan} className="w-3.5 h-3.5" />
              </button>
            </>
          ) : (
            <>
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onStartEditTitle(draft.id, draft.title);
                }}
                title="Rename Draft"
                className="text-muted hover:text-foreground p-0.5"
              >
                <FontAwesomeIcon icon={faPenToSquare} className="w-3.5 h-3.5" />
              </button>
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onArchive(draft.id);
                }}
                title="Archive Draft"
                className="text-muted hover:text-foreground p-0.5"
              >
                <FontAwesomeIcon icon={faBoxArchive} className="w-3.5 h-3.5" />
              </button>
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onDelete(draft.id);
                }}
                title="Delete Draft"
                className="text-red-400 hover:text-red-300 p-0.5"
              >
                <FontAwesomeIcon icon={faTrashCan} className="w-3.5 h-3.5" />
              </button>
            </>
          )}
        </div>
      )}
    </div>
  );
};
