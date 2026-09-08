import React, { useEffect, useRef } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faCheck, faPlus } from "@fortawesome/free-solid-svg-icons";
import type { CalendarEventDto } from "../../api/schedules";
import { PLATFORM_BADGE_COLORS, PLATFORM_ICONS } from "../../config/platformBadges";
import {
  getWeekDays,
  formatDateKey,
  formatTime,
  isToday
} from "./calendarUtils";

export interface WeekViewProps {
  currentDate: Date;
  events: CalendarEventDto[];
  onSelectDate: (date: Date) => void;
  onEventClick?: (event: CalendarEventDto) => void;
  onOpenSlotClick?: (event: CalendarEventDto) => void;
  onAddSlot?: (day: Date, hour: number) => void;
}

const HOURS = Array.from({ length: 24 }, (_, i) => i);
const WEEKDAY_NAMES = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

export const WeekView: React.FC<WeekViewProps> = ({
  currentDate,
  events,
  onSelectDate,
  onEventClick,
  onOpenSlotClick,
  onAddSlot
}) => {
  const weekDays = getWeekDays(currentDate);
  const scrollContainerRef = useRef<HTMLDivElement>(null);

  // Index events by "YYYY-MM-DD" -> hour (0..23) -> CalendarEventDto[]
  const eventMap: Record<string, Record<number, CalendarEventDto[]>> = {};
  for (const day of weekDays) {
    const key = formatDateKey(day);
    eventMap[key] = {};
    for (let h = 0; h < 24; h++) {
      eventMap[key][h] = [];
    }
  }

  for (const evt of events) {
    const d = new Date(evt.timestampUtc);
    const key = formatDateKey(d);
    const hour = d.getHours();
    if (eventMap[key] && eventMap[key][hour]) {
      eventMap[key][hour].push(evt);
    }
  }

  // Scroll to 8 AM on initial load
  useEffect(() => {
    if (scrollContainerRef.current) {
      const rowHeight = 64; // h-16 = 64px
      scrollContainerRef.current.scrollTop = 8 * rowHeight;
    }
  }, []);

  return (
    <div className="flex flex-col h-full overflow-hidden border border-border rounded-lg bg-card select-none">
      {/* Week Header */}
      <div className="grid grid-cols-[3.5rem_repeat(7,1fr)] border-b border-border bg-card/70 shrink-0">
        <div className="p-2 border-r border-border text-center text-xs font-mono text-muted/60">
          Time
        </div>
        {weekDays.map((day, idx) => {
          const dayToday = isToday(day);
          return (
            <button
              key={formatDateKey(day)}
              data-testid={`week-header-${formatDateKey(day)}`}
              onClick={() => onSelectDate(day)}
              className={`p-2 text-center border-r border-border last:border-r-0 hover:bg-zinc-800/30 transition flex flex-col items-center justify-center gap-0.5 ${
                dayToday ? "bg-accent/5 font-semibold" : ""
              }`}
            >
              <span className="text-[11px] font-mono text-muted">
                {WEEKDAY_NAMES[idx]}
              </span>
              <span
                className={`text-xs font-mono rounded-full px-1.5 py-0.5 ${
                  dayToday ? "bg-accent text-white font-bold" : "text-foreground font-medium"
                }`}
              >
                {day.getDate()}
              </span>
            </button>
          );
        })}
      </div>

      {/* 24-Hour Grid Scroll Area */}
      <div
        ref={scrollContainerRef}
        className="flex-1 overflow-y-auto grid grid-cols-[3.5rem_repeat(7,1fr)]"
      >
        {HOURS.map((hour) => {
          const hourLabel = `${String(hour).padStart(2, "0")}:00`;

          return (
            <React.Fragment key={hour}>
              {/* Hour Label */}
              <div className="h-16 border-t border-r border-border/50 pr-2 pt-0.5 text-right text-[10px] font-mono text-muted/60 select-none">
                {hourLabel}
              </div>

              {/* 7 Day Cells for this Hour */}
              {weekDays.map((day) => {
                const dayKey = formatDateKey(day);
                const cellEvents = eventMap[dayKey]?.[hour] || [];

                return (
                  <div
                    key={`${dayKey}-${hour}`}
                    data-testid={`week-cell-${dayKey}-${hour}`}
                    className="group relative h-16 border-t border-r border-border/40 last:border-r-0 p-1 overflow-y-auto hover:bg-zinc-800/30 transition flex flex-col gap-1"
                  >
                    {onAddSlot && (
                      <button
                        type="button"
                        data-testid={`add-slot-btn-${dayKey}-${hour}`}
                        onClick={(e) => {
                          e.stopPropagation();
                          onAddSlot(day, hour);
                        }}
                        className="opacity-0 group-hover:opacity-100 absolute top-1 right-1 w-5 h-5 rounded bg-accent/20 hover:bg-accent text-accent hover:text-white transition-opacity flex items-center justify-center text-[10px] font-mono leading-none z-10 shadow-sm"
                        title={`Add slot at ${hourLabel}`}
                        aria-label={`Add slot at ${hourLabel}`}
                      >
                        <FontAwesomeIcon icon={faPlus} className="text-[9px]" />
                      </button>
                    )}
                    {cellEvents.map((evt) => {
                      const timeStr = formatTime(evt.timestampUtc);

                      if (evt.type === "PublishedPost") {
                        return (
                          <div
                            key={evt.id}
                            data-testid={`week-event-published-${evt.id}`}
                            onClick={(e) => {
                              e.stopPropagation();
                              onEventClick?.(evt);
                            }}
                            className="p-1 rounded text-xs font-mono bg-green-500/10 text-green-400 border border-green-500/20 cursor-pointer hover:opacity-90 transition shrink-0"
                          >
                            <div className="flex items-center justify-between text-[10px] font-semibold">
                              <span className="flex items-center gap-1">
                                <FontAwesomeIcon icon={faCheck} className="text-[9px]" />
                                <span>{timeStr}</span>
                              </span>
                              <span className="text-[9px] opacity-80">Published</span>
                            </div>
                            <div className="font-sans font-medium text-[11px] truncate mt-0.5">
                              {evt.title || "Published Post"}
                            </div>
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
                            data-testid={`week-event-scheduled-${evt.id}`}
                            onClick={(e) => {
                              e.stopPropagation();
                              onEventClick?.(evt);
                            }}
                            className={`p-1 rounded text-xs font-mono border cursor-pointer hover:opacity-90 transition shrink-0 ${badgeClass}`}
                          >
                            <div className="flex items-center justify-between gap-1 text-[10px] font-semibold">
                              <span className="flex items-center gap-1">
                                {iconDef && (
                                  <FontAwesomeIcon icon={iconDef} className="text-[9px]" />
                                )}
                                <span>{timeStr}</span>
                              </span>
                              {evt.totalCapacity && (
                                <span className="opacity-80">
                                  {(evt.subSlotIndex ?? 0) + 1}/{evt.totalCapacity}
                                </span>
                              )}
                            </div>
                            <div className="font-sans font-medium text-[11px] truncate mt-0.5">
                              {evt.title || "Scheduled Post"}
                            </div>
                          </div>
                        );
                      }

                      return (
                        <div
                          key={evt.id}
                          data-testid={`week-event-open-${evt.id}`}
                          onClick={(e) => {
                            e.stopPropagation();
                            onOpenSlotClick ? onOpenSlotClick(evt) : onEventClick?.(evt);
                          }}
                          className="p-1 rounded text-[10px] font-mono border border-dashed border-border/70 text-muted/80 bg-zinc-900/40 hover:border-accent/50 hover:text-accent transition cursor-pointer shrink-0"
                        >
                          <div className="flex items-center justify-between font-semibold">
                            <span>{timeStr}</span>
                            <span>Open</span>
                          </div>
                          <div className="text-[9px] text-muted/60 mt-0.5">
                            + Queue Draft
                          </div>
                        </div>
                      );
                    })}
                  </div>
                );
              })}
            </React.Fragment>
          );
        })}
      </div>
    </div>
  );
};
