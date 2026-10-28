import React, { useState, useEffect, useRef } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faBars,
  faChevronDown,
  faCalendarAlt,
  faPaperPlane,
  faCheck,
  faArrowUpRightFromSquare,
} from "@fortawesome/free-solid-svg-icons";
import { Button } from "../common/Button";
import { fetchSchedules, type ScheduleSummaryDto } from "../../api/schedules";

export interface DraftPreviewHeaderProps {
  title: string;
  targetPlatform?: string | null;
  isCanonical: boolean;
  isSent: boolean;
  isPartiallySent?: boolean;
  canPublish: boolean;
  isPublishing: boolean;
  onOpenMenu: () => void;
  onOpenAdaptModal: () => void;
  onPublish: () => void;
  onAddToSchedule?: (scheduleId: string) => void;
  scheduleStatus?: {
    scheduleName: string;
    position?: number;
  } | null;
  publishedUrl?: string | null;
}

export const DraftPreviewHeader: React.FC<DraftPreviewHeaderProps> = ({
  title,
  targetPlatform,
  isCanonical,
  isSent,
  isPartiallySent,
  canPublish,
  isPublishing,
  onOpenMenu,
  onOpenAdaptModal,
  onPublish,
  onAddToSchedule,
  scheduleStatus,
  publishedUrl,
}) => {
  const [schedules, setSchedules] = useState<ScheduleSummaryDto[]>([]);
  const [isMenuOpen, setIsMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    let mounted = true;
    fetchSchedules()
      .then((list) => {
        if (mounted) setSchedules(list);
      })
      .catch(() => {
        if (mounted) setSchedules([]);
      });
    return () => {
      mounted = false;
    };
  }, []);

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setIsMenuOpen(false);
      }
    };
    if (isMenuOpen) {
      window.addEventListener("mousedown", handleClickOutside);
    }
    return () => window.removeEventListener("mousedown", handleClickOutside);
  }, [isMenuOpen]);

  return (
    <div
      className="px-3 py-2 border-b border-border bg-panel flex items-center justify-between text-xs font-mono text-muted select-none gap-2"
      data-testid="draft-preview-header"
    >
      <div className="flex items-center gap-2 min-w-0">
        <button
          type="button"
          onClick={onOpenMenu}
          className="p-1.5 -ml-1 text-muted hover:text-foreground hover:bg-border/60 rounded-md transition cursor-pointer shrink-0"
          data-testid="draft-hamburger-btn"
          aria-label="Open draft menu"
          title="Open draft menu"
        >
          <FontAwesomeIcon icon={faBars} className="w-4 h-4" />
        </button>

        <span className="font-semibold text-foreground text-xs font-sans truncate max-w-[140px] sm:max-w-xs">
          {title || "Untitled Draft"}
        </span>

        {targetPlatform && (
          <span className="text-[10px] font-sans font-medium normal-case text-muted bg-border/60 px-1.5 py-0.5 rounded shrink-0">
            {targetPlatform}
          </span>
        )}

        {scheduleStatus && (
          <span
            data-testid="draft-schedule-status-pill"
            className="text-[10px] font-mono normal-case bg-blue-500/15 text-blue-400 border border-blue-500/30 px-2 py-0.5 rounded hidden sm:flex items-center gap-1 shrink-0"
          >
            <FontAwesomeIcon icon={faCalendarAlt} className="text-[9px]" />
            <span>
              In Schedule: {scheduleStatus.scheduleName}
              {scheduleStatus.position ? ` (#${scheduleStatus.position})` : ""}
            </span>
          </span>
        )}
      </div>

      <div className="flex items-center gap-2 shrink-0">
        {isSent ? (
          <div className="flex items-center gap-2">
            <span
              data-testid="published-badge"
              className="text-[10px] font-mono normal-case bg-emerald-500/15 text-emerald-400 border border-emerald-500/30 px-2 py-0.5 rounded flex items-center gap-1 font-medium"
            >
              <FontAwesomeIcon icon={faCheck} className="text-[9px]" />
              <span>Published</span>
            </span>

            {publishedUrl && (
              <a
                href={publishedUrl}
                target="_blank"
                rel="noopener noreferrer"
                data-testid="published-post-link"
                className="inline-flex items-center gap-1.5 px-2 py-1 rounded text-xs font-sans font-medium text-emerald-400 bg-emerald-500/10 hover:bg-emerald-500/20 border border-emerald-500/30 transition normal-case"
              >
                <span>View</span>
                <FontAwesomeIcon icon={faArrowUpRightFromSquare} className="text-[10px]" />
              </a>
            )}
          </div>
        ) : (
          <>
            {isCanonical && (
              <Button
                variant="secondary"
                size="xs"
                onClick={onOpenAdaptModal}
                className="uppercase tracking-wider font-semibold text-[11px]"
              >
                Adapt
              </Button>
            )}

            {canPublish && (
              <div className="relative inline-flex rounded overflow-visible" ref={menuRef}>
                <Button
                  variant="primary"
                  size="xs"
                  onClick={onPublish}
                  loading={isPublishing}
                  className="uppercase tracking-wider font-semibold rounded-r-none border-r border-accent-foreground/20 text-[11px]"
                  data-testid="publish-primary-btn"
                >
                  {isPublishing ? "Publishing..." : isPartiallySent ? "Resume Publish" : "Publish"}
                </Button>

                <button
                  type="button"
                  data-testid="publish-split-trigger"
                  disabled={isPublishing}
                  onClick={() => setIsMenuOpen((prev) => !prev)}
                  className="px-2 py-1 bg-accent text-white hover:bg-accent/90 border-l border-white/20 rounded-r text-xs flex items-center justify-center transition disabled:opacity-50 cursor-pointer"
                  title="Publishing options"
                >
                  <FontAwesomeIcon icon={faChevronDown} className="text-[10px]" />
                </button>

                {isMenuOpen && (
                  <div
                    data-testid="publish-split-menu"
                    className="absolute right-0 top-full mt-1 w-56 rounded-lg bg-panel border border-border shadow-2xl py-1 z-50 text-left normal-case"
                  >
                    <button
                      type="button"
                      data-testid="publish-now-option"
                      onClick={() => {
                        setIsMenuOpen(false);
                        onPublish();
                      }}
                      className="w-full px-3 py-2 text-xs text-foreground hover:bg-zinc-800 flex items-center gap-2 transition cursor-pointer"
                    >
                      <FontAwesomeIcon icon={faPaperPlane} className="text-muted text-xs" />
                      <div>
                        <div className="font-semibold">Publish Now</div>
                        <div className="text-[10px] text-muted">Post thread immediately</div>
                      </div>
                    </button>

                    {schedules.length > 0 && onAddToSchedule && (
                      <>
                        <div className="border-t border-border my-1" />
                        <div className="px-3 py-1 text-[10px] font-mono text-muted uppercase tracking-wider flex items-center gap-1.5">
                          <FontAwesomeIcon icon={faCalendarAlt} className="text-[9px]" />
                          <span>Add to Schedule</span>
                        </div>

                        {schedules.map((sched) => (
                          <button
                            key={sched.id}
                            type="button"
                            data-testid={`add-to-schedule-${sched.id}`}
                            onClick={() => {
                              setIsMenuOpen(false);
                              onAddToSchedule(sched.id);
                            }}
                            className="w-full px-3 py-1.5 text-xs text-foreground hover:bg-zinc-800 flex items-center justify-between transition cursor-pointer"
                          >
                            <span className="font-medium truncate">{sched.name}</span>
                            <span className="text-[10px] font-mono text-muted/70 shrink-0">
                              {sched.timezone}
                            </span>
                          </button>
                        ))}
                      </>
                    )}
                  </div>
                )}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
};
