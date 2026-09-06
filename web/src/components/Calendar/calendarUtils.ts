import type { CalendarEventDto } from "../../api/schedules";

export interface CalendarGridCell {
  date: Date;
  isCurrentMonth: boolean;
  dateKey: string;
}

export function formatDateKey(d: Date): string {
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function formatTime(isoString: string): string {
  const d = new Date(isoString);
  const hours = String(d.getHours()).padStart(2, "0");
  const minutes = String(d.getMinutes()).padStart(2, "0");
  return `${hours}:${minutes}`;
}

export function isSameDay(d1: Date, d2: Date): boolean {
  return (
    d1.getFullYear() === d2.getFullYear() &&
    d1.getMonth() === d2.getMonth() &&
    d1.getDate() === d2.getDate()
  );
}

export function isToday(date: Date): boolean {
  return isSameDay(date, new Date());
}

export function getWeekDays(anchorDate: Date): Date[] {
  const current = new Date(anchorDate.getFullYear(), anchorDate.getMonth(), anchorDate.getDate());
  const day = current.getDay();
  const diffToMonday = day === 0 ? -6 : 1 - day;
  const monday = new Date(current);
  monday.setDate(current.getDate() + diffToMonday);

  const days: Date[] = [];
  for (let i = 0; i < 7; i++) {
    const d = new Date(monday);
    d.setDate(monday.getDate() + i);
    days.push(d);
  }
  return days;
}

export function getMonthGrid(year: number, month: number): CalendarGridCell[] {
  const firstDay = new Date(year, month, 1);
  const startDayOfWeek = firstDay.getDay();
  const diffToMonday = startDayOfWeek === 0 ? -6 : 1 - startDayOfWeek;

  const startDate = new Date(firstDay);
  startDate.setDate(firstDay.getDate() + diffToMonday);

  const cells: CalendarGridCell[] = [];
  const current = new Date(startDate);

  while (cells.length < 42) {
    if (cells.length >= 35 && current.getMonth() !== month && current.getDay() === 1) {
      break;
    }

    cells.push({
      date: new Date(current),
      isCurrentMonth: current.getMonth() === month,
      dateKey: formatDateKey(current)
    });

    current.setDate(current.getDate() + 1);
  }

  return cells;
}

export function formatDateRange(view: "month" | "week" | "day", anchorDate: Date): string {
  if (view === "month") {
    return anchorDate.toLocaleDateString("en-US", { month: "long", year: "numeric" });
  }
  if (view === "week") {
    const days = getWeekDays(anchorDate);
    const start = days[0];
    const end = days[6];
    const startMonth = start.toLocaleDateString("en-US", { month: "short" });
    const endMonth = end.toLocaleDateString("en-US", { month: "short" });
    const startDay = start.getDate();
    const endDay = end.getDate();
    const year = end.getFullYear();

    if (startMonth === endMonth) {
      return `${startMonth} ${startDay} – ${endDay}, ${year}`;
    }
    return `${startMonth} ${startDay} – ${endMonth} ${endDay}, ${year}`;
  }
  return anchorDate.toLocaleDateString("en-US", {
    weekday: "long",
    month: "long",
    day: "numeric",
    year: "numeric"
  });
}

export function calculateDateBounds(
  view: "month" | "week" | "day",
  anchorDate: Date
): { startUtc: string; endUtc: string } {
  if (view === "month") {
    const grid = getMonthGrid(anchorDate.getFullYear(), anchorDate.getMonth());
    const first = grid[0].date;
    const last = grid[grid.length - 1].date;
    const start = new Date(first.getFullYear(), first.getMonth(), first.getDate(), 0, 0, 0);
    const end = new Date(last.getFullYear(), last.getMonth(), last.getDate(), 23, 59, 59, 999);
    return { startUtc: start.toISOString(), endUtc: end.toISOString() };
  }
  if (view === "week") {
    const days = getWeekDays(anchorDate);
    const first = days[0];
    const last = days[6];
    const start = new Date(first.getFullYear(), first.getMonth(), first.getDate(), 0, 0, 0);
    const end = new Date(last.getFullYear(), last.getMonth(), last.getDate(), 23, 59, 59, 999);
    return { startUtc: start.toISOString(), endUtc: end.toISOString() };
  }
  const start = new Date(anchorDate.getFullYear(), anchorDate.getMonth(), anchorDate.getDate(), 0, 0, 0);
  const end = new Date(anchorDate.getFullYear(), anchorDate.getMonth(), anchorDate.getDate(), 23, 59, 59, 999);
  return { startUtc: start.toISOString(), endUtc: end.toISOString() };
}

export function groupEventsByDate(events: CalendarEventDto[]): Record<string, CalendarEventDto[]> {
  const groups: Record<string, CalendarEventDto[]> = {};
  for (const event of events) {
    const d = new Date(event.timestampUtc);
    const key = formatDateKey(d);
    if (!groups[key]) {
      groups[key] = [];
    }
    groups[key].push(event);
  }
  return groups;
}
