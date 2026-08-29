import { apiFetch } from "./client";

export interface BackgroundTaskDto {
  id: string;
  taskType: string;
  payloadJson: string;
  status: "Pending" | "Running" | "Completed" | "Failed" | "Cancelled";
  priority: number;
  attemptCount: number;
  maxAttempts: number;
  nextAttemptAt: string;
  lockedUntil?: string | null;
  lockedByWorker?: string | null;
  lastError?: string | null;
  deduplicationKey?: string | null;
  createdAt: string;
  updatedAt: string;
  completedAt?: string | null;
}

export interface BackgroundTaskStatsDto {
  totalPending: number;
  totalRunning: number;
  totalCompleted: number;
  totalFailed: number;
  totalCancelled: number;
  countsByTaskType: Record<string, number>;
}

export interface BackgroundTasksResponse {
  items: BackgroundTaskDto[];
  total: number;
  page: number;
  pageSize: number;
}

export async function fetchBackgroundTasks(
  taskType?: string,
  status?: string,
  page = 1,
  pageSize = 50
): Promise<BackgroundTasksResponse> {
  const params = new URLSearchParams();
  if (taskType) params.set("taskType", taskType);
  if (status) params.set("status", status);
  params.set("page", String(page));
  params.set("pageSize", String(pageSize));

  const res = await apiFetch(`/api/background-tasks?${params.toString()}`);
  if (!res.ok) throw new Error(`fetchBackgroundTasks failed: ${res.status}`);
  return res.json();
}

export async function fetchBackgroundTaskStats(): Promise<BackgroundTaskStatsDto> {
  const res = await apiFetch("/api/background-tasks/stats");
  if (!res.ok) throw new Error(`fetchBackgroundTaskStats failed: ${res.status}`);
  return res.json();
}

export async function retryBackgroundTask(id: string): Promise<void> {
  const res = await apiFetch(`/api/background-tasks/${id}/retry`, {
    method: "POST",
  });
  if (!res.ok) throw new Error(`retryBackgroundTask failed: ${res.status}`);
}

export async function cancelBackgroundTask(id: string): Promise<void> {
  const res = await apiFetch(`/api/background-tasks/${id}/cancel`, {
    method: "POST",
  });
  if (!res.ok) throw new Error(`cancelBackgroundTask failed: ${res.status}`);
}

export async function deleteBackgroundTask(id: string): Promise<void> {
  const res = await apiFetch(`/api/background-tasks/${id}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error(`deleteBackgroundTask failed: ${res.status}`);
}
