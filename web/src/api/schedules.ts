import { apiFetch } from "./client";

export type SlotRecurrenceType = "Daily" | "Weekly" | "Monthly" | "Yearly" | "Custom";

export type ScheduleQueueItemStatus = "Queued" | "Dispatched" | "Succeeded" | "Failed" | "Paused" | "Expired";

export type CalendarEventType = "OpenSlot" | "ScheduledPost" | "PublishedPost";

export interface ScheduleSummaryDto {
  id: string;
  name: string;
  timezone: string;
  isActive: boolean;
  slotRuleCount: number;
  queueItemCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface ScheduleSlotRuleDto {
  id: string;
  scheduleId: string;
  recurrenceType: SlotRecurrenceType;
  cronExpression: string;
  startTime: string;
  endTime: string;
  postCapacity: number;
  daysOfWeek?: number[] | null;
  dayOfMonth?: number | null;
  monthOfYear?: number | null;
  createdAt: string;
}

export interface ScheduleQueueItemDto {
  id: string;
  scheduleId: string;
  draftId: string;
  draftTitle: string;
  platformThreadId: string;
  platform: string;
  position: number;
  priorityScore?: number | null;
  effectivePriorityScore?: number | null;
  status: ScheduleQueueItemStatus;
  assignedSlotUtc?: string | null;
  lastError?: string | null;
  createdAt: string;
}

export interface ScheduleDetailDto {
  id: string;
  name: string;
  timezone: string;
  isActive: boolean;
  slotRules: ScheduleSlotRuleDto[];
  queueItems: ScheduleQueueItemDto[];
  createdAt: string;
  updatedAt: string;
}

export interface CalendarEventDto {
  id: string;
  type: CalendarEventType;
  timestampUtc: string;
  scheduleId?: string | null;
  scheduleName?: string | null;
  draftId?: string | null;
  title?: string | null;
  platformThreadId?: string | null;
  platform?: string | null;
  remoteUrl?: string | null;
  subSlotIndex?: number | null;
  totalCapacity?: number | null;
}

export interface CreateScheduleRequest {
  name: string;
  timezone?: string;
}

export interface UpdateScheduleRequest {
  name: string;
  timezone?: string;
  isActive: boolean;
}

export interface CreateSlotRuleRequest {
  recurrenceType: SlotRecurrenceType;
  cronExpression?: string;
  startTime: string;
  endTime: string;
  postCapacity?: number;
  daysOfWeek?: number[];
  dayOfMonth?: number;
  monthOfYear?: number;
}

export interface EnqueueItemRequest {
  draftId: string;
  platformThreadId?: string;
  targetPosition?: number;
  priorityScore?: number;
}

export interface ReorderQueueRequest {
  itemIdsInOrder: string[];
}

export interface PauseQueueItemRequest {
  isPaused: boolean;
}

export async function fetchSchedules(): Promise<ScheduleSummaryDto[]> {
  const res = await apiFetch("/api/schedules");
  if (!res.ok) throw new Error(`fetchSchedules failed: ${res.status}`);
  return res.json();
}

export async function fetchSchedule(id: string): Promise<ScheduleDetailDto> {
  const res = await apiFetch(`/api/schedules/${id}`);
  if (!res.ok) throw new Error(`fetchSchedule failed: ${res.status}`);
  return res.json();
}

export async function createSchedule(req: CreateScheduleRequest): Promise<ScheduleSummaryDto> {
  const res = await apiFetch("/api/schedules", {
    method: "POST",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `createSchedule failed: ${res.status}`);
  }
  return res.json();
}

export async function updateSchedule(id: string, req: UpdateScheduleRequest): Promise<ScheduleSummaryDto> {
  const res = await apiFetch(`/api/schedules/${id}`, {
    method: "PUT",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `updateSchedule failed: ${res.status}`);
  }
  return res.json();
}

export async function deleteSchedule(id: string): Promise<void> {
  const res = await apiFetch(`/api/schedules/${id}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error(`deleteSchedule failed: ${res.status}`);
}

export async function fetchSlotRules(scheduleId: string): Promise<ScheduleSlotRuleDto[]> {
  const res = await apiFetch(`/api/schedules/${scheduleId}/slots`);
  if (!res.ok) throw new Error(`fetchSlotRules failed: ${res.status}`);
  return res.json();
}

export async function createSlotRule(scheduleId: string, req: CreateSlotRuleRequest): Promise<ScheduleSlotRuleDto> {
  const res = await apiFetch(`/api/schedules/${scheduleId}/slots`, {
    method: "POST",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `createSlotRule failed: ${res.status}`);
  }
  return res.json();
}

export async function deleteSlotRule(scheduleId: string, slotId: string): Promise<void> {
  const res = await apiFetch(`/api/schedules/${scheduleId}/slots/${slotId}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error(`deleteSlotRule failed: ${res.status}`);
}

export async function fetchScheduleQueue(scheduleId: string): Promise<ScheduleQueueItemDto[]> {
  const res = await apiFetch(`/api/schedules/${scheduleId}/queue`);
  if (!res.ok) throw new Error(`fetchScheduleQueue failed: ${res.status}`);
  return res.json();
}

export async function enqueueQueueItem(scheduleId: string, req: EnqueueItemRequest): Promise<ScheduleQueueItemDto> {
  const res = await apiFetch(`/api/schedules/${scheduleId}/queue`, {
    method: "POST",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `enqueueQueueItem failed: ${res.status}`);
  }
  return res.json();
}

export async function reorderQueue(scheduleId: string, itemIdsInOrder: string[]): Promise<ScheduleQueueItemDto[]> {
  const res = await apiFetch(`/api/schedules/${scheduleId}/queue/reorder`, {
    method: "PUT",
    body: JSON.stringify({ itemIdsInOrder }),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `reorderQueue failed: ${res.status}`);
  }
  return res.json();
}

export async function removeQueueItem(scheduleId: string, itemId: string): Promise<void> {
  const res = await apiFetch(`/api/schedules/${scheduleId}/queue/${itemId}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error(`removeQueueItem failed: ${res.status}`);
}

export async function pauseQueueItem(scheduleId: string, itemId: string, isPaused: boolean): Promise<ScheduleQueueItemDto> {
  const res = await apiFetch(`/api/schedules/${scheduleId}/queue/${itemId}/pause`, {
    method: "POST",
    body: JSON.stringify({ isPaused }),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `pauseQueueItem failed: ${res.status}`);
  }
  return res.json();
}

export async function fetchCalendarFeed(start?: string, end?: string, scheduleId?: string): Promise<CalendarEventDto[]> {
  const params = new URLSearchParams();
  if (start) params.set("start", start);
  if (end) params.set("end", end);
  if (scheduleId) params.set("scheduleId", scheduleId);

  const res = await apiFetch(`/api/calendar?${params.toString()}`);
  if (!res.ok) throw new Error(`fetchCalendarFeed failed: ${res.status}`);
  return res.json();
}
