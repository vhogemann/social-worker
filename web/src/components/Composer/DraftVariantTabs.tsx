import React from "react";
import { useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faFileLines, faPlus } from "@fortawesome/free-solid-svg-icons";
import { PLATFORM_ICONS } from "../../config/platformBadges";
import { Button } from "../common/Button";
import type { DraftDto } from "../../api/drafts";

export interface DraftVariantTabsProps {
  workflowId?: string | null;
  canonicalDraftId: string;
  activeDraftId: string;
  variants?: DraftDto[];
  onOpenAdaptModal?: () => void;
}

export const DraftVariantTabs: React.FC<DraftVariantTabsProps> = ({
  workflowId,
  canonicalDraftId,
  activeDraftId,
  variants = [],
  onOpenAdaptModal,
}) => {
  const navigate = useNavigate();
  const isCanonicalActive = activeDraftId === canonicalDraftId;

  const handleSelectCanonical = () => {
    if (workflowId) {
      navigate(`/workflows/${workflowId}/draft/${canonicalDraftId}`);
    } else {
      navigate(`/draft/${canonicalDraftId}`);
    }
  };

  const handleSelectVariant = (variantId: string) => {
    if (workflowId) {
      navigate(`/workflows/${workflowId}/draft/${canonicalDraftId}/variant/${variantId}`);
    } else {
      navigate(`/draft/${canonicalDraftId}/variant/${variantId}`);
    }
  };

  return (
    <div
      className="border-b border-border bg-panel/70 px-4 py-1.5 flex items-center justify-between gap-3 text-xs shrink-0 select-none"
      data-testid="draft-variant-tabs"
    >
      <div className="flex items-center gap-1.5 overflow-x-auto no-scrollbar">
        <button
          onClick={handleSelectCanonical}
          data-testid="tab-canonical"
          className={`flex items-center gap-1.5 px-3 py-1 rounded-md text-xs font-medium transition cursor-pointer shrink-0 ${
            isCanonicalActive
              ? "bg-accent text-accent-foreground shadow-sm font-semibold"
              : "text-muted hover:text-foreground hover:bg-border/50"
          }`}
        >
          <FontAwesomeIcon icon={faFileLines} className="w-3 h-3 opacity-80" />
          <span>Canonical</span>
        </button>

        {variants.map((v) => {
          const isActive = activeDraftId === v.id;
          const platform = v.targetPlatform;

          return (
            <button
              key={v.id}
              onClick={() => handleSelectVariant(v.id)}
              data-testid={`tab-variant-${v.id}`}
              className={`flex items-center gap-1.5 px-3 py-1 rounded-md text-xs font-medium transition cursor-pointer shrink-0 ${
                isActive
                  ? "bg-accent text-accent-foreground shadow-sm font-semibold"
                  : "text-muted hover:text-foreground hover:bg-border/50"
              }`}
            >
              {platform && PLATFORM_ICONS[platform] && (
                <FontAwesomeIcon
                  icon={PLATFORM_ICONS[platform]}
                  className="w-3 h-3 opacity-90"
                />
              )}
              <span>{platform || v.title}</span>
              {v.status === "Sent" && (
                <span
                  className="w-1.5 h-1.5 rounded-full bg-emerald-500 shrink-0"
                  title="Published"
                />
              )}
            </button>
          );
        })}
      </div>

      {onOpenAdaptModal && (
        <div className="shrink-0">
          <Button
            variant="secondary"
            size="xs"
            onClick={onOpenAdaptModal}
            className="flex items-center gap-1 font-sans text-[11px] uppercase tracking-wider"
          >
            <FontAwesomeIcon icon={faPlus} className="w-2.5 h-2.5" />
            <span>Adapt</span>
          </Button>
        </div>
      )}
    </div>
  );
};
