import { apiFetch } from "./client";

export interface PlatformThreadDto {
  id: string;
  draftId: string;
  platform: string;
  stage: string;
  content: string | null;
}

export interface SourceDto {
  id: string;
  draftId: string;
  kind: string;
  reference: string;
  title: string | null;
  addedAt: string;
}

export interface MediaAssetDto {
  id: string;
  draftId: string;
  fileName: string;
  mimeType: string;
  altText: string | null;
  filePath: string;
  sizeBytes: number;
  width: number;
  height: number;
  createdAt: string;
}

export interface DraftDto {
  id: string;
  title: string;
  status: string;
  content: string | null;
  threads: PlatformThreadDto[];
  sources?: SourceDto[];
  mediaAssets?: MediaAssetDto[];
  createdAt: string;
  updatedAt: string;
}

export async function fetchDrafts(): Promise<DraftDto[]> {
  const res = await apiFetch("/api/drafts");
  if (!res.ok) throw new Error(`fetchDrafts failed: ${res.status}`);
  return res.json();
}

export async function createDraft(title?: string, content?: string): Promise<DraftDto> {
  const res = await apiFetch("/api/drafts", {
    method: "POST",
    body: JSON.stringify({ title, content }),
  });
  if (!res.ok) throw new Error(`createDraft failed: ${res.status}`);
  return res.json();
}

export async function fetchDraft(id: string): Promise<DraftDto> {
  const res = await apiFetch(`/api/drafts/${id}`);
  if (!res.ok) throw new Error(`fetchDraft failed: ${res.status}`);
  return res.json();
}

export async function patchDraft(
  id: string,
  data: { title?: string; content?: string; status?: string }
): Promise<DraftDto> {
  const res = await apiFetch(`/api/drafts/${id}`, {
    method: "PATCH",
    body: JSON.stringify(data),
  });
  if (!res.ok) throw new Error(`patchDraft failed: ${res.status}`);
  return res.json();
}

export async function fetchPlatformThreads(draftId: string): Promise<PlatformThreadDto[]> {
  const res = await apiFetch(`/api/drafts/${draftId}/threads`);
  if (!res.ok) throw new Error(`fetchPlatformThreads failed: ${res.status}`);
  return res.json();
}

export async function createPlatformThread(draftId: string, platform: string): Promise<PlatformThreadDto> {
  const res = await apiFetch(`/api/drafts/${draftId}/threads`, {
    method: "POST",
    body: JSON.stringify({ platform }),
  });
  if (!res.ok) throw new Error(`createPlatformThread failed: ${res.status}`);
  return res.json();
}

export async function patchPlatformThread(
  draftId: string,
  threadId: string,
  data: { stage?: string; content?: string }
): Promise<PlatformThreadDto> {
  const res = await apiFetch(`/api/drafts/${draftId}/threads/${threadId}`, {
    method: "PATCH",
    body: JSON.stringify(data),
  });
  if (!res.ok) throw new Error(`patchPlatformThread failed: ${res.status}`);
  return res.json();
}

export async function fetchSources(draftId: string): Promise<SourceDto[]> {
  const res = await apiFetch(`/api/drafts/${draftId}/sources`);
  if (!res.ok) throw new Error(`fetchSources failed: ${res.status}`);
  return res.json();
}

export async function uploadFile(draftId: string, file: File): Promise<{ sourceId: string; markdownLink: string }> {
  const formData = new FormData();
  formData.append("file", file);

  const res = await apiFetch(`/api/drafts/${draftId}/files`, {
    method: "POST",
    body: formData,
  });
  if (!res.ok) throw new Error(`uploadFile failed: ${res.status}`);
  return res.json();
}

export async function uploadMedia(draftId: string, file: File): Promise<{ id: string; markdownTag: string }> {
  const formData = new FormData();
  formData.append("file", file);

  const res = await apiFetch(`/api/drafts/${draftId}/media`, {
    method: "POST",
    body: formData,
  });
  if (!res.ok) throw new Error(`uploadMedia failed: ${res.status}`);
  return res.json();
}

export async function patchMediaAsset(id: string, data: { altText: string }): Promise<MediaAssetDto> {
  const res = await apiFetch(`/api/media/${id}`, {
    method: "PATCH",
    body: JSON.stringify(data),
  });
  if (!res.ok) throw new Error(`patchMediaAsset failed: ${res.status}`);
  return res.json();
}

export async function deleteMediaAsset(id: string): Promise<{ success: boolean }> {
  const res = await apiFetch(`/api/media/${id}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error(`deleteMediaAsset failed: ${res.status}`);
  return res.json();
}