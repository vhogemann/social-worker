import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faLink } from "@fortawesome/free-solid-svg-icons";
import { PLATFORM_BADGE_COLORS, PLATFORM_ICONS } from "../../config/platformBadges";
import type { DraftDto } from "../../api/drafts";

export interface DraftVariantItemProps {
  variant: DraftDto;
  isActive: boolean;
  onSelect: (id: string) => void;
}

export const DraftVariantItem: React.FC<DraftVariantItemProps> = ({
  variant,
  isActive,
  onSelect,
}) => {
  return (
    <div
      className={`group relative w-full hover:bg-border/40 transition-colors ${
        isActive ? "bg-border/40" : ""
      }`}
      data-testid="draft-variant-item"
    >
      {isActive && (
        <span className="absolute left-0 top-0 bottom-0 w-0.5 bg-accent" />
      )}
      <button
        onClick={() => onSelect(variant.id)}
        className="w-full text-left px-3 py-1.5 pr-12 focus:outline-none"
      >
        <div
          className={`flex items-center gap-1 text-sm truncate ${
            isActive ? "text-accent font-medium" : "text-foreground/70"
          }`}
        >
          <FontAwesomeIcon icon={faLink} className="w-3 h-3 shrink-0 text-muted" />
          <span className="truncate">{variant.title}</span>
        </div>
        <div className="flex items-center gap-1 mt-0.5 ml-4">
          {variant.targetPlatform && (
            <span
              className={`inline-flex items-center gap-1 text-[10px] px-1 py-0.5 rounded font-sans font-medium ${
                PLATFORM_BADGE_COLORS[variant.targetPlatform] || "text-muted"
              }`}
            >
              {PLATFORM_ICONS[variant.targetPlatform] && (
                <FontAwesomeIcon
                  icon={PLATFORM_ICONS[variant.targetPlatform]}
                  className="w-3 h-3"
                />
              )}
              {variant.targetPlatform}
              {variant.targetAccountHandle && (
                <span className="opacity-75 font-mono">
                  @{variant.targetAccountHandle.replace(/^@/, "")}
                </span>
              )}
            </span>
          )}
          <span className="text-[10px] text-muted font-sans">variant</span>
        </div>
      </button>
    </div>
  );
};
