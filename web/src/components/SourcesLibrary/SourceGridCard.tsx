import React from "react";
import { type SourceSearchItemDto } from "../../api/drafts";
import { Badge } from "../common/Badge";
import { Button } from "../common/Button";

export interface SourceGridCardProps {
  item: SourceSearchItemDto;
  isLinked: boolean;
  isLinking: boolean;
  hasActiveDraft: boolean;
  onOpenPreview: (item: SourceSearchItemDto) => void;
  onLinkSource: (sourceId: string) => void;
}

export const SourceGridCard: React.FC<SourceGridCardProps> = ({
  item,
  isLinked,
  isLinking,
  hasActiveDraft,
  onOpenPreview,
  onLinkSource
}) => {
  return (
    <div
      onClick={() => onOpenPreview(item)}
      className="bg-panel border border-border hover:border-zinc-700 rounded-xl p-4 flex flex-col justify-between cursor-pointer transition select-none group relative"
    >
      <div>
        {/* Top bar with type tag & date */}
        <div className="flex items-center justify-between gap-2 mb-2">
          <Badge variant="accent" size="xs">
            {item.kind}
          </Badge>
          <span className="text-[10px] text-muted font-mono">
            {new Date(item.addedAt).toLocaleDateString()}
          </span>
        </div>

        <h3 className="text-xs font-bold text-foreground line-clamp-2 group-hover:text-accent transition mb-1">
          {item.title || item.reference}
        </h3>
        <p className="text-[10px] text-muted truncate font-mono mb-3">{item.reference}</p>

        {item.summary && (
          <p className="text-[11px] text-muted line-clamp-3 leading-relaxed mb-4">
            {item.summary}
          </p>
        )}
      </div>

      {/* Footer link action */}
      <div className="mt-auto pt-3 border-t border-border/40 flex items-center justify-between gap-3">
        <span className="text-[10px] text-accent font-semibold group-hover:underline">
          View detail &rarr;
        </span>

        {hasActiveDraft && (
          <Button
            variant="secondary"
            size="xs"
            onClick={(e) => {
              e.stopPropagation();
              onLinkSource(item.id);
            }}
            disabled={isLinked || isLinking}
            className="text-[10px] font-bold"
          >
            {isLinked ? "Linked" : isLinking ? "Linking..." : "Link to Draft"}
          </Button>
        )}
      </div>
    </div>
  );
};
