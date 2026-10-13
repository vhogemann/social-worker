import React, { useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faPenToSquare,
  faBoxArchive,
  faArrowRotateLeft,
  faTrashCan,
  faBolt,
  faLayerGroup,
  faClock,
} from "@fortawesome/free-solid-svg-icons";
import { PLATFORM_BADGE_COLORS, PLATFORM_ICONS } from "../../config/platformBadges";
import { Badge } from "../common/Badge";
import type { DraftDto } from "../../api/drafts";

export interface DraftCardProps {
  draft: DraftDto;
  onSelect: (id: string, workflowId?: string | null) => void;
  onSelectVariant?: (canonicalId: string, variantId: string, workflowId?: string | null) => void;
  onArchive: (id: string) => void;
  onUnarchive: (id: string) => void;
  onDelete: (id: string) => void;
  onRename: (id: string, newTitle: string) => void;
}

export const DraftCard: React.FC<DraftCardProps> = ({
  draft,
  onSelect,
  onSelectVariant,
  onArchive,
  onUnarchive,
  onDelete,
  onRename,
}) => {
  const [isEditing, setIsEditing] = useState(false);
  const [editTitle, setEditTitle] = useState(draft.title);

  const handleSaveTitle = () => {
    setIsEditing(false);
    if (editTitle.trim() && editTitle.trim() !== draft.title) {
      onRename(draft.id, editTitle.trim());
    } else {
      setEditTitle(draft.title);
    }
  };

  const formattedDate = new Date(draft.updatedAt).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
  });

  const variantCount = draft.variants?.length || 0;

  const handleVariantClick = (e: React.MouseEvent, variantId: string) => {
    e.stopPropagation();
    if (onSelectVariant) {
      onSelectVariant(draft.id, variantId, draft.automationWorkflowId);
    } else {
      onSelect(variantId, draft.automationWorkflowId);
    }
  };

  return (
    <div
      onClick={() => onSelect(draft.id, draft.automationWorkflowId)}
      className="bg-panel border border-border rounded-xl p-5 flex flex-col justify-between transition hover:border-zinc-600 hover:shadow-md cursor-pointer group relative"
      data-testid="draft-card"
    >
      <div className="flex flex-col gap-3">
        <div className="flex justify-between items-start gap-2">
          {isEditing ? (
            <input
              type="text"
              value={editTitle}
              autoFocus
              onClick={(e) => e.stopPropagation()}
              onChange={(e) => setEditTitle(e.target.value)}
              onBlur={handleSaveTitle}
              onKeyDown={(e) => {
                if (e.key === "Enter") handleSaveTitle();
                if (e.key === "Escape") {
                  setIsEditing(false);
                  setEditTitle(draft.title);
                }
              }}
              className="text-sm font-bold bg-bg border border-accent rounded px-2 py-0.5 w-full text-foreground focus:outline-none"
            />
          ) : (
            <h3 className="text-sm font-bold text-foreground line-clamp-2 group-hover:text-accent transition">
              {draft.title}
            </h3>
          )}

          <div className="flex items-center gap-1.5 shrink-0" onClick={(e) => e.stopPropagation()}>
            <Badge
              variant={
                draft.status === "Published" || draft.status === "Sent"
                  ? "success"
                  : draft.status === "Archived"
                  ? "neutral"
                  : draft.status === "Scheduled" || draft.status === "Discarded"
                  ? "warning"
                  : "accent"
              }
              className={draft.status === "Discarded" ? "border-amber-500/40 text-amber-300 bg-amber-500/10" : undefined}
            >
              {draft.status === "Discarded" && draft.relevanceScore != null
                ? `Discarded (${draft.relevanceScore}/10)`
                : draft.status}
            </Badge>
          </div>
        </div>

        {draft.status === "Discarded" && draft.discardReason && (
          <p className="text-xs text-muted/90 line-clamp-2 italic bg-bg/60 p-2 rounded border border-border/60">
            "{draft.discardReason}"
          </p>
        )}

        {draft.content && draft.status !== "Discarded" && (
          <p className="text-xs text-muted line-clamp-3 leading-relaxed">
            {draft.content}
          </p>
        )}

        {draft.automationWorkflowTitle && (
          <div className="flex items-center gap-1.5 text-[11px] text-accent/90 font-medium">
            <FontAwesomeIcon icon={faBolt} className="w-3 h-3 text-accent" />
            <span className="truncate">{draft.automationWorkflowTitle}</span>
          </div>
        )}

        {/* Clickable Variant Badges */}
        {draft.variants && draft.variants.length > 0 && (
          <div className="flex flex-wrap items-center gap-1.5 mt-1" onClick={(e) => e.stopPropagation()}>
            {draft.variants.map((v) => (
              <button
                key={v.id}
                onClick={(e) => handleVariantClick(e, v.id)}
                className={`inline-flex items-center gap-1 text-[10px] px-2 py-0.5 rounded font-sans font-medium hover:opacity-80 transition cursor-pointer ${
                  PLATFORM_BADGE_COLORS[v.targetPlatform || ""] || "text-muted bg-border/40"
                }`}
                title={`Open ${v.targetPlatform} variant`}
                data-testid={`card-variant-${v.id}`}
              >
                {v.targetPlatform && PLATFORM_ICONS[v.targetPlatform] && (
                  <FontAwesomeIcon icon={PLATFORM_ICONS[v.targetPlatform]} className="w-2.5 h-2.5" />
                )}
                <span>{v.targetPlatform || v.title}</span>
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="flex justify-between items-center mt-5 pt-3 border-t border-border/40 text-xs text-muted">
        <div className="flex items-center gap-2">
          {draft.targetPlatform && (
            <span
              className={`inline-flex items-center gap-1 text-[11px] px-1.5 py-0.5 rounded font-sans font-medium ${
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
            </span>
          )}

          {variantCount > 0 && (
            <span className="inline-flex items-center gap-1 text-[10px] text-muted font-mono bg-border/40 px-1.5 py-0.5 rounded">
              <FontAwesomeIcon icon={faLayerGroup} className="w-2.5 h-2.5" />
              {variantCount} {variantCount === 1 ? "variant" : "variants"}
            </span>
          )}
        </div>

        <div className="flex items-center gap-2">
          <div className="flex items-center gap-1 text-[11px]">
            <FontAwesomeIcon icon={faClock} className="w-2.5 h-2.5 text-muted/80" />
            <span>{formattedDate}</span>
          </div>

          <div
            className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity ml-1"
            onClick={(e) => e.stopPropagation()}
          >
            {draft.status === "Archived" ? (
              <>
                <button
                  onClick={() => onUnarchive(draft.id)}
                  title="Restore Draft"
                  className="text-muted hover:text-foreground p-1 rounded hover:bg-border/60 transition"
                >
                  <FontAwesomeIcon icon={faArrowRotateLeft} className="w-3.5 h-3.5" />
                </button>
                <button
                  onClick={() => onDelete(draft.id)}
                  title="Delete Draft"
                  className="text-red-400 hover:text-red-300 p-1 rounded hover:bg-border/60 transition"
                >
                  <FontAwesomeIcon icon={faTrashCan} className="w-3.5 h-3.5" />
                </button>
              </>
            ) : (
              <>
                <button
                  onClick={() => setIsEditing(true)}
                  title="Rename Draft"
                  className="text-muted hover:text-foreground p-1 rounded hover:bg-border/60 transition"
                >
                  <FontAwesomeIcon icon={faPenToSquare} className="w-3.5 h-3.5" />
                </button>
                <button
                  onClick={() => onArchive(draft.id)}
                  title="Archive Draft"
                  className="text-muted hover:text-foreground p-1 rounded hover:bg-border/60 transition"
                >
                  <FontAwesomeIcon icon={faBoxArchive} className="w-3.5 h-3.5" />
                </button>
                <button
                  onClick={() => onDelete(draft.id)}
                  title="Delete Draft"
                  className="text-red-400 hover:text-red-300 p-1 rounded hover:bg-border/60 transition"
                >
                  <FontAwesomeIcon icon={faTrashCan} className="w-3.5 h-3.5" />
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
