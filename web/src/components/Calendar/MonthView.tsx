import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faCheck, faPlus } from "@fortawesome/free-solid-svg-icons";
import type { CalendarEventDto } from "../../api/schedules";
import { PLATFORM_BADGE_COLORS, PLATFORM_ICONS, getPlatformLabel } from "../../config/platformBadges";
import {
  getMonthGrid,
  groupEventsByDate,
  formatTime,
  isToday
} from "./calendarUtils";

export interface MonthViewProps {
  currentDate: Date;
  events: CalendarEventDto[];
  onSelectDate: (date: Date) => void;
  onEventClick?: (event: CalendarEventDto) => void;
  onAddSlot?: (date: Date) => void;
}

const WEEKDAY_NAMES = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

export const MonthView: React.FC<MonthViewProps> = ({
  currentDate,
  events,
  onSelectDate,
  onEventClick,
  onAddSlot
}) => {
  const cells = getMonthGrid(currentDate.getFullYear(), currentDate.getMonth());
  const grouped = groupEventsByDate(events);

  return (
    <div className="flex flex-col h-full overflow-hidden select-none">
      <div className="grid grid-cols-7 border-b border-border bg-card/60 text-xs font-mono text-muted py-2 text-center">
        {WEEKDAY_NAMES.map((day) => (
          <div key={day} className="font-semibold">
            {day}
          </div>
        ))}
      </div>

      <div className="grid grid-cols-7 gap-px bg-border flex-1 auto-rows-fr overflow-y-auto">
        {cells.map((cell) => {
          const dayEvents = grouped[cell.dateKey] || [];
          const cellIsToday = isToday(cell.date);
          const maxVisible = 3;
          const visibleEvents = dayEvents.slice(0, maxVisible);
          const hiddenCount = dayEvents.length - maxVisible;

          return (
            <div
              key={cell.dateKey}
              data-testid={`month-cell-${cell.dateKey}`}
              onClick={() => onSelectDate(cell.date)}
              className={`group relative flex flex-col min-h-[105px] p-1.5 transition cursor-pointer ${
                cell.isCurrentMonth
                  ? "bg-card hover:bg-zinc-800/40"
                  : "bg-card/40 text-muted/50 hover:bg-zinc-800/20"
              } ${cellIsToday ? "ring-1 ring-inset ring-accent" : ""}`}
            >
              <div className="flex items-center justify-between mb-1">
                <span
                  className={`text-xs font-mono font-medium rounded-full flex items-center justify-center ${
                    cellIsToday
                      ? "bg-accent text-white font-bold w-5 h-5"
                      : cell.isCurrentMonth
                      ? "text-foreground"
                      : "text-muted/60"
                  }`}
                >
                  {cell.date.getDate()}
                </span>

                <div className="flex items-center gap-1">
                  {onAddSlot && (
                    <button
                      type="button"
                      data-testid={`add-slot-month-btn-${cell.dateKey}`}
                      onClick={(e) => {
                        e.stopPropagation();
                        onAddSlot(cell.date);
                      }}
                      className="opacity-0 group-hover:opacity-100 p-0.5 rounded hover:bg-accent/20 text-muted hover:text-accent transition text-[10px]"
                      title={`Add slot for ${cell.date.toLocaleDateString()}`}
                      aria-label="Add slot"
                    >
                      <FontAwesomeIcon icon={faPlus} className="text-[9px]" />
                    </button>
                  )}
                  {dayEvents.length > 0 && (
                    <span className="text-[10px] font-mono text-muted/70">
                      {dayEvents.length} {dayEvents.length === 1 ? "slot" : "slots"}
                    </span>
                  )}
                </div>
              </div>

              <div className="flex-1 flex flex-col gap-1 overflow-hidden">
                {visibleEvents.map((evt) => {
                  const timeStr = formatTime(evt.timestampUtc);

                  if (evt.type === "PublishedPost") {
                    return (
                      <div
                        key={evt.id}
                        data-testid={`event-published-${evt.id}`}
                        onClick={(e) => {
                          e.stopPropagation();
                          onEventClick ? onEventClick(evt) : onSelectDate(cell.date);
                        }}
                        className="flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-mono bg-green-500/10 text-green-400 border border-green-500/20 truncate"
                        title={`${timeStr} - Published: ${evt.title || "Post"}`}
                      >
                        <FontAwesomeIcon icon={faCheck} className="text-[9px] shrink-0" />
                        <span className="font-semibold shrink-0">{timeStr}</span>
                        <span className="truncate">{evt.title || "Published Post"}</span>
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
                        data-testid={`event-scheduled-${evt.id}`}
                        onClick={(e) => {
                          e.stopPropagation();
                          onEventClick ? onEventClick(evt) : onSelectDate(cell.date);
                        }}
                        className={`flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-mono truncate ${badgeClass}`}
                        title={`${timeStr} - ${getPlatformLabel(platformKey)}: ${evt.title || "Scheduled Post"}`}
                      >
                        {iconDef && (
                          <FontAwesomeIcon icon={iconDef} className="text-[9px] shrink-0" />
                        )}
                        <span className="font-semibold shrink-0">{timeStr}</span>
                        <span className="truncate">{evt.title || "Scheduled Post"}</span>
                      </div>
                    );
                  }

                  return (
                    <div
                      key={evt.id}
                      data-testid={`event-open-${evt.id}`}
                      onClick={(e) => {
                        e.stopPropagation();
                        onEventClick ? onEventClick(evt) : onSelectDate(cell.date);
                      }}
                      className="flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-mono border border-dashed border-border text-muted/80 bg-zinc-900/40 hover:border-accent/40 truncate"
                      title={`${timeStr} - Open Slot`}
                    >
                      <span className="font-semibold shrink-0">{timeStr}</span>
                      <span className="truncate">Open Slot</span>
                    </div>
                  );
                })}

                {hiddenCount > 0 && (
                  <div className="text-[10px] font-mono text-accent hover:underline pt-0.5 pl-0.5">
                    +{hiddenCount} more
                  </div>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
