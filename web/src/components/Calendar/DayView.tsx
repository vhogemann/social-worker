import React from "react";
import { useNavigate } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faCalendarAlt,
  faCheck,
  faPlus,
  faClock,
  faArrowUpRightFromSquare,
  faPenNib
} from "@fortawesome/free-solid-svg-icons";
import type { CalendarEventDto } from "../../api/schedules";
import { PLATFORM_BADGE_COLORS, PLATFORM_ICONS, getPlatformLabel } from "../../config/platformBadges";
import { Button } from "../common/Button";
import { Badge } from "../common/Badge";
import { EmptyState } from "../common/EmptyState";
import { isSameDay, formatTime } from "./calendarUtils";

export interface DayViewProps {
  currentDate: Date;
  events: CalendarEventDto[];
  onEventClick?: (event: CalendarEventDto) => void;
  onOpenSlotClick?: (event: CalendarEventDto) => void;
  onAddSlot?: (date: Date, hour?: number) => void;
}

export const DayView: React.FC<DayViewProps> = ({
  currentDate,
  events,
  onEventClick,
  onOpenSlotClick,
  onAddSlot
}) => {
  const navigate = useNavigate();

  // Filter events belonging to currentDate and sort chronologically
  const dayEvents = events
    .filter((e) => isSameDay(new Date(e.timestampUtc), currentDate))
    .sort((a, b) => new Date(a.timestampUtc).getTime() - new Date(b.timestampUtc).getTime());

  const scheduledCount = dayEvents.filter((e) => e.type === "ScheduledPost").length;
  const openCount = dayEvents.filter((e) => e.type === "OpenSlot").length;
  const publishedCount = dayEvents.filter((e) => e.type === "PublishedPost").length;
  const totalSlots = dayEvents.length;
  const filledCount = scheduledCount + publishedCount;
  const fillPercentage = totalSlots > 0 ? Math.round((filledCount / totalSlots) * 100) : 0;

  const formattedDate = currentDate.toLocaleDateString("en-US", {
    weekday: "long",
    month: "long",
    day: "numeric",
    year: "numeric"
  });

  return (
    <div className="flex flex-col h-full overflow-hidden bg-card border border-border rounded-lg select-none">
      {/* Top Banner & Stats */}
      <div className="p-4 border-b border-border bg-card/70 flex flex-col sm:flex-row sm:items-center justify-between gap-4 shrink-0">
        <div>
          <h2 className="text-base font-semibold text-foreground flex items-center gap-2">
            <FontAwesomeIcon icon={faCalendarAlt} className="text-accent" />
            <span>{formattedDate}</span>
          </h2>
          <p className="text-xs font-mono text-muted mt-0.5">
            {totalSlots === 0
              ? "No scheduled slot activity"
              : `${totalSlots} total slot${totalSlots === 1 ? "" : "s"} (${filledCount} filled, ${openCount} open)`}
          </p>
        </div>

        <div className="flex items-center gap-3 shrink-0">
          {totalSlots > 0 && (
            <div className="flex items-center gap-3 bg-zinc-900/60 px-3 py-2 rounded-lg border border-border shrink-0">
              <div className="text-right">
                <div className="text-xs font-mono font-semibold text-foreground">
                  {filledCount} / {totalSlots} filled
                </div>
                <div className="text-[10px] font-mono text-muted">{fillPercentage}% capacity</div>
              </div>
              <div className="w-20 bg-zinc-800 rounded-full h-2 overflow-hidden">
                <div
                  className="bg-accent h-2 transition-all duration-300"
                  style={{ width: `${fillPercentage}%` }}
                />
              </div>
            </div>
          )}

          {onAddSlot && (
            <Button
              size="sm"
              variant="secondary"
              icon={faPlus}
              data-testid="day-add-slot-btn"
              onClick={() => onAddSlot(currentDate)}
            >
              Add Slot
            </Button>
          )}
        </div>
      </div>

      {/* Timeline List */}
      <div className="flex-1 overflow-y-auto p-4">
        {dayEvents.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full py-12">
            <EmptyState
              icon={faCalendarAlt}
              title="No slots or posts scheduled"
              description="There are no slot rules or scheduled posts configured for this day."
            />
            {onAddSlot && (
              <div className="mt-4">
                <Button
                  size="sm"
                  variant="primary"
                  icon={faPlus}
                  data-testid="day-empty-add-slot-btn"
                  onClick={() => onAddSlot(currentDate)}
                >
                  Add Slot for This Day
                </Button>
              </div>
            )}
          </div>
        ) : (
          <div className="flex flex-col gap-3 max-w-4xl mx-auto">
            {dayEvents.map((evt) => {
              const timeStr = formatTime(evt.timestampUtc);

              if (evt.type === "PublishedPost") {
                const platformKey = evt.platform || "Bluesky";
                const iconDef = PLATFORM_ICONS[platformKey];

                return (
                  <div
                    key={evt.id}
                    data-testid={`day-event-published-${evt.id}`}
                    onClick={() => onEventClick?.(evt)}
                    className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-4 rounded-lg bg-green-500/5 border border-green-500/20 hover:border-green-500/40 transition"
                  >
                    <div className="flex items-start sm:items-center gap-3 min-w-0">
                      <div className="flex items-center gap-1.5 px-2.5 py-1 rounded bg-green-500/15 text-green-400 font-mono font-bold text-xs shrink-0">
                        <FontAwesomeIcon icon={faCheck} className="text-xs" />
                        <span>{timeStr}</span>
                      </div>
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 mb-1 flex-wrap">
                          <Badge variant="success" size="xs">
                            Published
                          </Badge>
                          <Badge variant="neutral" size="xs">
                            {iconDef && (
                              <FontAwesomeIcon icon={iconDef} className="mr-1 text-[10px]" />
                            )}
                            {getPlatformLabel(platformKey)}
                          </Badge>
                        </div>
                        <h4 className="text-sm font-semibold text-foreground truncate">
                          {evt.title || "Published Post"}
                        </h4>
                      </div>
                    </div>

                    {evt.remoteUrl && (
                      <a
                        href={evt.remoteUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        onClick={(e) => e.stopPropagation()}
                        className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded text-xs font-mono text-green-400 bg-green-500/10 hover:bg-green-500/20 border border-green-500/30 transition shrink-0"
                      >
                        <span>View Live</span>
                        <FontAwesomeIcon icon={faArrowUpRightFromSquare} className="text-[10px]" />
                      </a>
                    )}
                  </div>
                );
              }

              if (evt.type === "ScheduledPost") {
                const platformKey = evt.platform || "Bluesky";
                const badgeClass =
                  PLATFORM_BADGE_COLORS[platformKey] ||
                  "bg-accent/10 text-accent border border-accent/20";
                const iconDef = PLATFORM_ICONS[platformKey];

                return (
                  <div
                    key={evt.id}
                    data-testid={`day-event-scheduled-${evt.id}`}
                    onClick={() => onEventClick?.(evt)}
                    className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-4 rounded-lg bg-card border border-border hover:border-accent/40 transition"
                  >
                    <div className="flex items-start sm:items-center gap-3 min-w-0">
                      <div className="flex items-center gap-1.5 px-2.5 py-1 rounded bg-accent/15 text-accent font-mono font-bold text-xs shrink-0">
                        <FontAwesomeIcon icon={faClock} className="text-xs" />
                        <span>{timeStr}</span>
                      </div>

                      <div className="min-w-0">
                        <div className="flex items-center gap-2 mb-1 flex-wrap">
                          <span
                            className={`inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-mono ${badgeClass}`}
                          >
                            {iconDef && <FontAwesomeIcon icon={iconDef} className="text-[9px]" />}
                            {getPlatformLabel(platformKey)}
                          </span>

                          {evt.totalCapacity && (
                            <Badge variant="neutral" size="xs">
                              Slot {(evt.subSlotIndex ?? 0) + 1} of {evt.totalCapacity}
                            </Badge>
                          )}

                          {evt.scheduleName && (
                            <Badge variant="neutral" size="xs">
                              {evt.scheduleName}
                            </Badge>
                          )}
                        </div>

                        <h4 className="text-sm font-semibold text-foreground truncate">
                          {evt.title || "Scheduled Post"}
                        </h4>
                      </div>
                    </div>

                    <div className="flex items-center gap-2 shrink-0">
                      {evt.draftId && (
                        <Button
                          size="sm"
                          variant="secondary"
                          icon={faPenNib}
                          onClick={(e) => {
                            e.stopPropagation();
                            navigate(`/draft/${evt.draftId}`);
                          }}
                        >
                          Edit Draft
                        </Button>
                      )}
                    </div>
                  </div>
                );
              }

              // OpenSlot
              return (
                <div
                  key={evt.id}
                  data-testid={`day-event-open-${evt.id}`}
                  onClick={() =>
                    onOpenSlotClick ? onOpenSlotClick(evt) : onEventClick?.(evt)
                  }
                  className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-4 rounded-lg bg-zinc-900/30 border border-dashed border-border hover:border-accent/50 hover:bg-zinc-900/50 transition cursor-pointer"
                >
                  <div className="flex items-start sm:items-center gap-3 min-w-0">
                    <div className="flex items-center gap-1.5 px-2.5 py-1 rounded bg-zinc-800 text-muted font-mono font-bold text-xs shrink-0">
                      <FontAwesomeIcon icon={faClock} className="text-xs" />
                      <span>{timeStr}</span>
                    </div>

                    <div>
                      <div className="flex items-center gap-2 mb-0.5">
                        <Badge variant="neutral" size="xs">
                          Open Slot
                        </Badge>
                        {evt.totalCapacity && (
                          <span className="text-[11px] font-mono text-muted">
                            Slot {(evt.subSlotIndex ?? 0) + 1} of {evt.totalCapacity} available
                          </span>
                        )}
                      </div>
                      <p className="text-xs text-muted/70">
                        {evt.scheduleName ? `Belongs to ${evt.scheduleName}` : "Available posting slot"}
                      </p>
                    </div>
                  </div>

                  <Button
                    size="sm"
                    variant="primary"
                    icon={faPlus}
                    onClick={(e) => {
                      e.stopPropagation();
                      onOpenSlotClick ? onOpenSlotClick(evt) : onEventClick?.(evt);
                    }}
                  >
                    Queue Draft
                  </Button>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
};
