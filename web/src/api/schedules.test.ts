import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import {
  fetchSchedules,
  fetchSchedule,
  createSchedule,
  updateSchedule,
  deleteSchedule,
  fetchSlotRules,
  createSlotRule,
  deleteSlotRule,
  fetchScheduleQueue,
  enqueueQueueItem,
  reorderQueue,
  removeQueueItem,
  pauseQueueItem,
  fetchCalendarFeed,
  type ScheduleSummaryDto,
  type ScheduleDetailDto,
  type ScheduleSlotRuleDto,
  type ScheduleQueueItemDto,
  type CalendarEventDto
} from "./schedules";

vi.mock("./client", () => ({
  apiFetch: vi.fn(),
}));

import { apiFetch } from "./client";
const mockApiFetch = apiFetch as Mock;

const makeSchedule = (id = "s1"): ScheduleSummaryDto => ({
  id,
  name: "Daily Buffer",
  timezone: "UTC",
  isActive: true,
  slotRuleCount: 1,
  queueItemCount: 2,
  createdAt: "2026-09-01T00:00:00Z",
  updatedAt: "2026-09-01T00:00:00Z"
});

describe("schedules API", () => {
  beforeEach(() => {
    mockApiFetch.mockReset();
  });

  it("fetchSchedules returns schedule array", async () => {
    mockApiFetch.mockResolvedValueOnce(
      new Response(JSON.stringify([makeSchedule()]), { status: 200 })
    );
    const result = await fetchSchedules();
    expect(result).toHaveLength(1);
    expect(result[0].name).toBe("Daily Buffer");
    expect(mockApiFetch).toHaveBeenCalledWith("/api/schedules");
  });

  it("fetchSchedule returns detail with slots and queue", async () => {
    const detail: ScheduleDetailDto = {
      ...makeSchedule(),
      slotRules: [],
      queueItems: []
    };
    mockApiFetch.mockResolvedValueOnce(
      new Response(JSON.stringify(detail), { status: 200 })
    );
    const result = await fetchSchedule("s1");
    expect(result.id).toBe("s1");
    expect(mockApiFetch).toHaveBeenCalledWith("/api/schedules/s1");
  });

  it("createSchedule sends POST and returns new schedule", async () => {
    mockApiFetch.mockResolvedValueOnce(
      new Response(JSON.stringify(makeSchedule("s2")), { status: 201 })
    );
    const result = await createSchedule({ name: "Evening Slot", timezone: "America/New_York" });
    expect(result.id).toBe("s2");
    expect(mockApiFetch).toHaveBeenCalledWith("/api/schedules", {
      method: "POST",
      body: JSON.stringify({ name: "Evening Slot", timezone: "America/New_York" })
    });
  });

  it("updateSchedule sends PUT and returns updated schedule", async () => {
    mockApiFetch.mockResolvedValueOnce(
      new Response(JSON.stringify(makeSchedule("s1")), { status: 200 })
    );
    const result = await updateSchedule("s1", { name: "Updated Buffer", isActive: false });
    expect(result.id).toBe("s1");
    expect(mockApiFetch).toHaveBeenCalledWith("/api/schedules/s1", {
      method: "PUT",
      body: JSON.stringify({ name: "Updated Buffer", isActive: false })
    });
  });

  it("deleteSchedule sends DELETE", async () => {
    mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
    await deleteSchedule("s1");
    expect(mockApiFetch).toHaveBeenCalledWith("/api/schedules/s1", { method: "DELETE" });
  });

  it("fetchSlotRules returns slot rules", async () => {
    const slotRule: ScheduleSlotRuleDto = {
      id: "r1",
      scheduleId: "s1",
      recurrenceType: "Daily",
      cronExpression: "0 9 * * *",
      startTime: "09:00:00",
      endTime: "10:00:00",
      postCapacity: 4,
      createdAt: "2026-09-01T00:00:00Z"
    };
    mockApiFetch.mockResolvedValueOnce(
      new Response(JSON.stringify([slotRule]), { status: 200 })
    );
    const result = await fetchSlotRules("s1");
    expect(result).toHaveLength(1);
    expect(result[0].postCapacity).toBe(4);
  });

  it("createSlotRule sends POST", async () => {
    const created: ScheduleSlotRuleDto = {
      id: "r2",
      scheduleId: "s1",
      recurrenceType: "Weekly",
      cronExpression: "0 9 * * 1-5",
      startTime: "09:00:00",
      endTime: "10:00:00",
      postCapacity: 2,
      createdAt: "2026-09-01T00:00:00Z"
    };
    mockApiFetch.mockResolvedValueOnce(
      new Response(JSON.stringify(created), { status: 201 })
    );
    const result = await createSlotRule("s1", {
      recurrenceType: "Weekly",
      startTime: "09:00:00",
      endTime: "10:00:00",
      postCapacity: 2
    });
    expect(result.id).toBe("r2");
  });

  it("deleteSlotRule sends DELETE", async () => {
    mockApiFetch.mockResolvedValueOnce(new Response(null, { status: 204 }));
    await deleteSlotRule("s1", "r1");
    expect(mockApiFetch).toHaveBeenCalledWith("/api/schedules/s1/slots/r1", { method: "DELETE" });
  });

  it("fetchScheduleQueue returns queue items", async () => {
    const queueItem: ScheduleQueueItemDto = {
      id: "q1",
      scheduleId: "s1",
      draftId: "d1",
      draftTitle: "Test Draft",
      platformThreadId: "t1",
      platform: "Bluesky",
      position: 0,
      status: "Queued",
      createdAt: "2026-09-01T00:00:00Z"
    };
    mockApiFetch.mockResolvedValueOnce(
      new Response(JSON.stringify([queueItem]), { status: 200 })
    );
    const result = await fetchScheduleQueue("s1");
    expect(result).toHaveLength(1);
    expect(result[0].draftTitle).toBe("Test Draft");
  });

  it("reorderQueue sends PUT with item IDs array", async () => {
    mockApiFetch.mockResolvedValueOnce(
      new Response(JSON.stringify([]), { status: 200 })
    );
    await reorderQueue("s1", ["q2", "q1"]);
    expect(mockApiFetch).toHaveBeenCalledWith("/api/schedules/s1/queue/reorder", {
      method: "PUT",
      body: JSON.stringify({ itemIdsInOrder: ["q2", "q1"] })
    });
  });

  it("pauseQueueItem sends POST with isPaused boolean", async () => {
    mockApiFetch.mockResolvedValueOnce(
      new Response(JSON.stringify({ id: "q1", status: "Paused" }), { status: 200 })
    );
    const res = await pauseQueueItem("s1", "q1", true);
    expect(res.status).toBe("Paused");
    expect(mockApiFetch).toHaveBeenCalledWith("/api/schedules/s1/queue/q1/pause", {
      method: "POST",
      body: JSON.stringify({ isPaused: true })
    });
  });

  it("fetchCalendarFeed queries /api/calendar with ISO bounds", async () => {
    const calEvent: CalendarEventDto = {
      id: "e1",
      type: "OpenSlot",
      timestampUtc: "2026-09-07T09:00:00Z"
    };
    mockApiFetch.mockResolvedValueOnce(
      new Response(JSON.stringify([calEvent]), { status: 200 })
    );
    const result = await fetchCalendarFeed("2026-09-07T00:00:00Z", "2026-09-07T23:59:59Z", "s1");
    expect(result).toHaveLength(1);
    expect(mockApiFetch).toHaveBeenCalledWith(
      "/api/calendar?start=2026-09-07T00%3A00%3A00Z&end=2026-09-07T23%3A59%3A59Z&scheduleId=s1"
    );
  });
});
