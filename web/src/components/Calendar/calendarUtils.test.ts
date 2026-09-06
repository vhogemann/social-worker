import { describe, it, expect } from "vitest";
import {
  formatDateKey,
  formatTime,
  isSameDay,
  isToday,
  getWeekDays,
  getMonthGrid,
  formatDateRange,
  calculateDateBounds,
  groupEventsByDate
} from "./calendarUtils";
import type { CalendarEventDto } from "../../api/schedules";

describe("calendarUtils", () => {
  it("formatDateKey returns YYYY-MM-DD format", () => {
    const d = new Date(2026, 8, 7); // September 7, 2026
    expect(formatDateKey(d)).toBe("2026-09-07");
  });

  it("isSameDay checks equality of year, month, date", () => {
    const d1 = new Date(2026, 8, 7, 10, 0);
    const d2 = new Date(2026, 8, 7, 18, 30);
    const d3 = new Date(2026, 8, 8, 10, 0);
    expect(isSameDay(d1, d2)).toBe(true);
    expect(isSameDay(d1, d3)).toBe(false);
  });

  it("isToday checks against current date", () => {
    expect(isToday(new Date())).toBe(true);
    expect(isToday(new Date(2020, 0, 1))).toBe(false);
  });

  it("getWeekDays returns 7 days starting from Monday", () => {
    const wednesday = new Date(2026, 8, 9); // Wednesday Sep 9, 2026
    const week = getWeekDays(wednesday);
    expect(week).toHaveLength(7);
    expect(week[0].getDay()).toBe(1); // Monday
    expect(week[0].getDate()).toBe(7); // Sep 7
    expect(week[6].getDay()).toBe(0); // Sunday
    expect(week[6].getDate()).toBe(13); // Sep 13
  });

  it("getMonthGrid returns cells starting on Monday and filling complete weeks", () => {
    const grid = getMonthGrid(2026, 8); // September 2026
    expect(grid.length).toBeGreaterThanOrEqual(35);
    expect(grid.length % 7).toBe(0);
    expect(grid[0].date.getDay()).toBe(1); // Starts on Monday
    const currentMonthCells = grid.filter((c) => c.isCurrentMonth);
    expect(currentMonthCells).toHaveLength(30); // Sep has 30 days
  });

  it("formatDateRange formats correctly for month, week, day", () => {
    const anchor = new Date(2026, 8, 7);
    expect(formatDateRange("month", anchor)).toBe("September 2026");
    expect(formatDateRange("week", anchor)).toBe("Sep 7 – 13, 2026");
    expect(formatDateRange("day", anchor)).toContain("September 7, 2026");
  });

  it("calculateDateBounds returns ISO strings for range", () => {
    const anchor = new Date(2026, 8, 7);
    const dayBounds = calculateDateBounds("day", anchor);
    expect(dayBounds.startUtc).toBeDefined();
    expect(dayBounds.endUtc).toBeDefined();

    const weekBounds = calculateDateBounds("week", anchor);
    expect(new Date(weekBounds.startUtc).getTime()).toBeLessThan(new Date(weekBounds.endUtc).getTime());
  });

  it("groupEventsByDate groups events by local date key", () => {
    const events: CalendarEventDto[] = [
      {
        id: "1",
        type: "ScheduledPost",
        timestampUtc: "2026-09-07T09:00:00Z",
        title: "Post 1"
      },
      {
        id: "2",
        type: "OpenSlot",
        timestampUtc: "2026-09-07T10:00:00Z"
      },
      {
        id: "3",
        type: "PublishedPost",
        timestampUtc: "2026-09-08T14:00:00Z",
        title: "Post 2"
      }
    ];

    const groups = groupEventsByDate(events);
    const keys = Object.keys(groups);
    expect(keys.length).toBe(2);
  });
});
