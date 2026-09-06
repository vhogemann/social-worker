import React, { useEffect, useState, useCallback } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faChevronLeft,
  faChevronRight,
  faCalendarAlt,
  faRotateRight,
  faSpinner,
  faSlidersH
} from "@fortawesome/free-solid-svg-icons";
import {
  fetchSchedules,
  fetchCalendarFeed,
  type ScheduleSummaryDto,
  type CalendarEventDto
} from "../../api/schedules";
import { Button } from "../common/Button";
import { Badge } from "../common/Badge";
import { AlertBanner } from "../common/AlertBanner";
import { MonthView } from "./MonthView";
import { WeekView } from "./WeekView";
import { DayView } from "./DayView";
import {
  formatDateRange,
  calculateDateBounds
} from "./calendarUtils";

export type CalendarViewMode = "month" | "week" | "day";

export interface CalendarPageProps {
  initialView?: CalendarViewMode;
  initialDate?: Date;
  onOpenScheduleManager?: () => void;
  onOpenDraftPicker?: (slotEvent: CalendarEventDto) => void;
}

export const CalendarPage: React.FC<CalendarPageProps> = ({
  initialView = "week",
  initialDate,
  onOpenScheduleManager,
  onOpenDraftPicker
}) => {
  const [schedules, setSchedules] = useState<ScheduleSummaryDto[]>([]);
  const [selectedScheduleId, setSelectedScheduleId] = useState<string>("");
  const [view, setView] = useState<CalendarViewMode>(initialView);
  const [currentDate, setCurrentDate] = useState<Date>(initialDate || new Date());
  const [events, setEvents] = useState<CalendarEventDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load schedule list once
  useEffect(() => {
    let mounted = true;
    fetchSchedules()
      .then((data) => {
        if (!mounted) return;
        setSchedules(data);
        if (data.length > 0 && !selectedScheduleId) {
          setSelectedScheduleId(data[0].id);
        }
      })
      .catch((err) => {
        if (!mounted) return;
        setError(err?.message || "Failed to load schedules.");
      });
    return () => {
      mounted = false;
    };
  }, []);

  // Load calendar feed when date, view, or schedule selection changes
  const loadFeed = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const bounds = calculateDateBounds(view, currentDate);
      const scheduleIdParam = selectedScheduleId || undefined;
      const data = await fetchCalendarFeed(bounds.startUtc, bounds.endUtc, scheduleIdParam);
      setEvents(data);
    } catch (err: any) {
      setError(err?.message || "Failed to load calendar events.");
    } finally {
      setLoading(false);
    }
  }, [view, currentDate, selectedScheduleId]);

  useEffect(() => {
    loadFeed();
  }, [loadFeed]);

  // Date Navigation handlers
  const handlePrev = () => {
    const d = new Date(currentDate);
    if (view === "month") {
      d.setMonth(d.getMonth() - 1);
    } else if (view === "week") {
      d.setDate(d.getDate() - 7);
    } else {
      d.setDate(d.getDate() - 1);
    }
    setCurrentDate(d);
  };

  const handleNext = () => {
    const d = new Date(currentDate);
    if (view === "month") {
      d.setMonth(d.getMonth() + 1);
    } else if (view === "week") {
      d.setDate(d.getDate() + 7);
    } else {
      d.setDate(d.getDate() + 1);
    }
    setCurrentDate(d);
  };

  const handleToday = () => {
    setCurrentDate(new Date());
  };

  const handleSelectDate = (date: Date) => {
    setCurrentDate(date);
    setView("day");
  };

  const selectedSchedule = schedules.find((s) => s.id === selectedScheduleId);
  const activeTimezone = selectedSchedule?.timezone || Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";

  return (
    <div className="flex-1 flex flex-col min-h-0 bg-background overflow-hidden p-4">
      {/* Top Header Controls */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-3 mb-4 shrink-0">
        {/* Title & Schedule selector */}
        <div className="flex items-center gap-3 flex-wrap">
          <div className="flex items-center gap-2">
            <FontAwesomeIcon icon={faCalendarAlt} className="text-accent text-lg" />
            <h1 className="text-base font-semibold text-foreground">Content Calendar</h1>
          </div>

          <div className="flex items-center gap-2">
            <select
              data-testid="schedule-selector"
              value={selectedScheduleId}
              onChange={(e) => setSelectedScheduleId(e.target.value)}
              className="bg-card border border-border rounded px-2.5 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
            >
              <option value="">All Schedules</option>
              {schedules.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>

            <Badge variant="neutral" size="xs">
              {activeTimezone}
            </Badge>
          </div>
        </div>

        {/* Date Navigator (Prev, Today, Next + Label) */}
        <div className="flex items-center gap-2">
          <div className="flex items-center bg-card border border-border rounded overflow-hidden">
            <button
              data-testid="cal-nav-prev"
              onClick={handlePrev}
              className="px-2 py-1 text-xs text-muted hover:text-foreground hover:bg-zinc-800 transition"
              title="Previous period"
            >
              <FontAwesomeIcon icon={faChevronLeft} />
            </button>
            <button
              data-testid="cal-nav-today"
              onClick={handleToday}
              className="px-3 py-1 text-xs font-mono font-medium text-foreground hover:bg-zinc-800 border-x border-border transition"
            >
              Today
            </button>
            <button
              data-testid="cal-nav-next"
              onClick={handleNext}
              className="px-2 py-1 text-xs text-muted hover:text-foreground hover:bg-zinc-800 transition"
              title="Next period"
            >
              <FontAwesomeIcon icon={faChevronRight} />
            </button>
          </div>

          <span
            data-testid="cal-period-label"
            className="text-xs sm:text-sm font-semibold text-foreground min-w-[140px] text-center"
          >
            {formatDateRange(view, currentDate)}
          </span>
        </div>

        {/* View Switcher & Actions */}
        <div className="flex items-center gap-2">
          <div className="flex bg-card border border-border rounded p-0.5">
            {(["day", "week", "month"] as const).map((mode) => (
              <button
                key={mode}
                data-testid={`view-toggle-${mode}`}
                onClick={() => setView(mode)}
                className={`px-3 py-1 text-xs font-mono capitalize rounded transition ${
                  view === mode
                    ? "bg-accent text-white font-semibold"
                    : "text-muted hover:text-foreground"
                }`}
              >
                {mode}
              </button>
            ))}
          </div>

          <Button
            size="sm"
            variant="secondary"
            icon={loading ? faSpinner : faRotateRight}
            onClick={loadFeed}
            disabled={loading}
            title="Refresh feed"
          />

          {onOpenScheduleManager && (
            <Button
              size="sm"
              variant="secondary"
              icon={faSlidersH}
              onClick={onOpenScheduleManager}
            >
              Manage
            </Button>
          )}
        </div>
      </div>

      {error && (
        <div className="mb-3 shrink-0">
          <AlertBanner variant="error" message={error} />
        </div>
      )}

      {/* Main Calendar View Area */}
      <div className="flex-1 min-h-0 relative">
        {loading && (
          <div className="absolute inset-0 bg-background/50 backdrop-blur-[1px] z-10 flex items-center justify-center pointer-events-none">
            <div className="flex items-center gap-2 bg-card border border-border px-3 py-2 rounded-lg text-xs font-mono text-muted">
              <FontAwesomeIcon icon={faSpinner} className="animate-spin text-accent" />
              <span>Loading calendar...</span>
            </div>
          </div>
        )}

        {view === "month" && (
          <MonthView
            currentDate={currentDate}
            events={events}
            onSelectDate={handleSelectDate}
          />
        )}

        {view === "week" && (
          <WeekView
            currentDate={currentDate}
            events={events}
            onSelectDate={handleSelectDate}
            onOpenSlotClick={onOpenDraftPicker}
          />
        )}

        {view === "day" && (
          <DayView
            currentDate={currentDate}
            events={events}
            onOpenSlotClick={onOpenDraftPicker}
          />
        )}
      </div>
    </div>
  );
};
