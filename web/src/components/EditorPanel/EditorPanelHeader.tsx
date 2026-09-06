import React, { useState, useEffect, useRef } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faChevronDown, faCalendarAlt, faPaperPlane } from "@fortawesome/free-solid-svg-icons";
import { EditorModeSwitcher } from "./EditorModeSwitcher";
import { Button } from "../common/Button";
import { fetchSchedules, type ScheduleSummaryDto } from "../../api/schedules";

export interface EditorPanelHeaderProps {
  targetPlatform?: string | null;
  mode: "edit" | "preview";
  onChangeMode: (mode: "edit" | "preview") => void;
  isCanonical: boolean;
  isSent: boolean;
  canPublish: boolean;
  isPublishing: boolean;
  onOpenAdaptModal: () => void;
  onPublish: () => void;
  onAddToSchedule?: (scheduleId: string) => void;
  scheduleStatus?: {
    scheduleName: string;
    position?: number;
  } | null;
}

export const EditorPanelHeader: React.FC<EditorPanelHeaderProps> = ({
  targetPlatform,
  mode,
  onChangeMode,
  isCanonical,
  isSent,
  canPublish,
  isPublishing,
  onOpenAdaptModal,
  onPublish,
  onAddToSchedule,
  scheduleStatus,
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
    <div className="px-3 py-2 border-b border-border flex items-center justify-between text-xs font-mono text-muted uppercase tracking-wider select-none">
      <span className="flex items-center gap-2">
        <span>editor</span>
        {targetPlatform && (
          <span className="text-[10px] font-sans font-medium normal-case text-muted bg-border/60 px-1.5 py-0.5 rounded">
            {targetPlatform}
          </span>
        )}
        {scheduleStatus && (
          <span
            data-testid="draft-schedule-status-pill"
            className="text-[10px] font-mono normal-case bg-blue-500/15 text-blue-400 border border-blue-500/30 px-2 py-0.5 rounded flex items-center gap-1"
          >
            <FontAwesomeIcon icon={faCalendarAlt} className="text-[9px]" />
            <span>
              In Schedule: {scheduleStatus.scheduleName}
              {scheduleStatus.position ? ` (#${scheduleStatus.position})` : ""}
            </span>
          </span>
        )}
      </span>

      <EditorModeSwitcher mode={mode} onChangeMode={onChangeMode} />

      <div className="flex items-center gap-2 ml-auto">
        {isCanonical && !isSent && (
          <Button
            variant="secondary"
            size="xs"
            onClick={onOpenAdaptModal}
            className="uppercase tracking-wider font-semibold"
          >
            Adapt
          </Button>
        )}

        {canPublish && !isSent && (
          <div className="relative inline-flex rounded overflow-visible" ref={menuRef}>
            {/* Primary Publish Button */}
            <Button
              variant="primary"
              size="xs"
              onClick={onPublish}
              loading={isPublishing}
              className="uppercase tracking-wider font-semibold rounded-r-none border-r border-accent-foreground/20"
              data-testid="publish-primary-btn"
            >
              {isPublishing ? "Publishing..." : "Publish"}
            </Button>

            {/* Split Dropdown Trigger */}
            <button
              type="button"
              data-testid="publish-split-trigger"
              disabled={isPublishing}
              onClick={() => setIsMenuOpen((prev) => !prev)}
              className="px-2 py-1 bg-accent text-white hover:bg-accent/90 border-l border-white/20 rounded-r text-xs flex items-center justify-center transition disabled:opacity-50"
              title="Publishing options"
            >
              <FontAwesomeIcon icon={faChevronDown} className="text-[10px]" />
            </button>

            {/* Dropdown Menu */}
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
                  className="w-full px-3 py-2 text-xs text-foreground hover:bg-zinc-800 flex items-center gap-2 transition"
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
                        className="w-full px-3 py-1.5 text-xs text-foreground hover:bg-zinc-800 flex items-center justify-between transition"
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
      </div>
    </div>
  );
};
