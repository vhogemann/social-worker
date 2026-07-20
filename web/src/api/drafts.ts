import { apiFetch } from "./client";

export interface PostDto {
  id: string;
  platformThreadId: string;
  segmentIndex: number;
  platform: string;
  remoteId?: string;
  url?: string;
}

export interface PlatformThreadDto {
  id: string;
  draftId: string;
  platform: string;
  stage: string;
  content: string | null;
  posts: PostDto[];
}

export interface SourceDto {
  id: string;
  draftId: string;
  kind: string;
  reference: string;
  title: string | null;
  summary?: string | null;
  processingStatus?: string;
  youtubeVideoId?: string | null;
  addedAt: string;
}

export interface SourceDetailDto {
  id: string;
  draftId: string;
  kind: string;
  reference: string;
  title: string | null;
  content: string | null;
  summary?: string | null;
  processingStatus?: string;
  youtubeVideoId?: string | null;
  addedAt: string;
}

export interface SourceSearchItemDto {
  id: string;
  kind: string;
  reference: string;
  title: string | null;
  summary: string | null;
  processingStatus: string;
  youtubeVideoId: string | null;
  addedAt: string;
}

export interface SourceSearchResultDto {
  items: SourceSearchItemDto[];
  total: number;
  page: number;
  pageSize: number;
}

export interface SourceStatusDto {
  sourceId: string;
  processingStatus: string;
  summary: string | null;
  youtubeVideoId: string | null;
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
  targetPlatform: string | null;
  canonicalDraftId: string | null;
  threads: PlatformThreadDto[];
  blueskyReplyTarget?: {
    replyRootUri: string;
    replyRootCid: string;
    replyParentUri: string;
    replyParentCid: string;
    replyParentUrl?: string | null;
    replyParentAuthor?: string | null;
    replyParentText?: string | null;
    replyParentAvatarUrl?: string | null;
  } | null;
  sources?: SourceDto[];
  mediaAssets?: MediaAssetDto[];
  chatHistory?: string | null;
  chatSummary?: string | null;
  lastSummarizedMessageCount?: number;
  createdAt: string;
  updatedAt: string;
}

export async function fetchDrafts(): Promise<DraftDto[]> {
  const res = await apiFetch("/api/drafts");
  if (!res.ok) throw new Error(`fetchDrafts failed: ${res.status}`);
  return res.json();
}

export async function createDraft(title?: string, content?: string, targetPlatform?: string): Promise<DraftDto> {
  const res = await apiFetch("/api/drafts", {
    method: "POST",
    body: JSON.stringify({ title, content, targetPlatform }),
  });
  if (!res.ok) throw new Error(`createDraft failed: ${res.status}`);
  return res.json();
}

export async function createReplyDraftFromBlueskyPostUrl(url: string, title?: string, content?: string): Promise<DraftDto> {
  const res = await apiFetch("/api/drafts/reply-from-url", {
    method: "POST",
    body: JSON.stringify({ url, title, content }),
  });
  if (!res.ok) {
    const message = await res.text().catch(() => "");
    throw new Error(message || `createReplyDraftFromBlueskyPostUrl failed: ${res.status}`);
  }
  return res.json();
}

export async function fetchDraft(id: string): Promise<DraftDto> {
  const res = await apiFetch(`/api/drafts/${id}`);
  if (!res.ok) throw new Error(`fetchDraft failed: ${res.status}`);
  return res.json();
}

export async function patchDraft(
  id: string,
  data: {
    title?: string;
    content?: string;
    status?: string;
    chatHistory?: string;
    chatSummary?: string;
    lastSummarizedMessageCount?: number;
  }
): Promise<DraftDto> {
  const res = await apiFetch(`/api/drafts/${id}`, {
    method: "PATCH",
    body: JSON.stringify(data),
  });
  if (!res.ok) throw new Error(`patchDraft failed: ${res.status}`);
  return res.json();
}

export async function patchDraftBlueskyReplyTargetFromUrl(id: string, url: string): Promise<DraftDto> {
  const res = await apiFetch(`/api/drafts/${id}/bluesky-reply-target/from-url`, {
    method: "PATCH",
    body: JSON.stringify({ url }),
  });
  if (!res.ok) {
    const message = await res.text().catch(() => "");
    throw new Error(message || `patchDraftBlueskyReplyTargetFromUrl failed: ${res.status}`);
  }
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

export interface PublishResult {
  success: boolean;
  errorMessage?: string;
  authUrl?: string;
  posts: {
    segmentIndex: number;
    remoteId: string;
    url: string;
  }[];
}

export async function publishPlatformThread(
  draftId: string,
  threadId: string
): Promise<PublishResult> {
  const res = await apiFetch(`/api/drafts/${draftId}/threads/${threadId}/publish`, {
    method: "POST",
  });
  if (!res.ok) {
    const contentType = res.headers.get("content-type") ?? "";
    if (contentType.includes("application/json")) {
      const data = await res.json().catch(() => null);
      if (typeof data === "string" && data.trim().length > 0) {
        throw new Error(data);
      }

      if (data && typeof data === "object") {
        const payload = data as Record<string, unknown>;
        const message = payload.error ?? payload.message;
        if (typeof message === "string" && message.trim().length > 0) {
          throw new Error(message);
        }
      }

      throw new Error(`publishPlatformThread failed: ${res.status}`);
    }

    const message = await res.text().catch(() => "");
    throw new Error(message || `publishPlatformThread failed: ${res.status}`);
  }
  return res.json();
}

export async function fetchSources(draftId: string): Promise<SourceDto[]> {
  const res = await apiFetch(`/api/drafts/${draftId}/sources`);
  if (!res.ok) throw new Error(`fetchSources failed: ${res.status}`);
  return res.json();
}

export async function fetchSourceDetail(draftId: string, sourceId: string): Promise<SourceDetailDto> {
  const res = await apiFetch(`/api/drafts/${draftId}/sources/${sourceId}`);
  if (!res.ok) throw new Error(`fetchSourceDetail failed: ${res.status}`);
  return res.json();
}

export async function fetchSourceById(sourceId: string): Promise<SourceDetailDto> {
  const res = await apiFetch(`/api/sources/${sourceId}`);
  if (!res.ok) throw new Error(`fetchSourceById failed: ${res.status}`);
  return res.json();
}

export async function searchSources(
  query: string,
  page = 1,
  pageSize = 20,
  kind?: string,
  addedAfter?: string,
  addedBefore?: string
): Promise<SourceSearchResultDto> {
  const params = new URLSearchParams({ query, page: `${page}`, pageSize: `${pageSize}` });
  if (kind) params.set("kind", kind);
  if (addedAfter) params.set("addedAfter", addedAfter);
  if (addedBefore) params.set("addedBefore", addedBefore);

  const res = await apiFetch(`/api/sources?${params.toString()}`);
  if (!res.ok) throw new Error(`searchSources failed: ${res.status}`);
  return res.json();
}

export async function fetchSourceStatus(sourceId: string): Promise<SourceStatusDto> {
  const res = await apiFetch(`/api/sources/${sourceId}/status`);
  if (!res.ok) throw new Error(`fetchSourceStatus failed: ${res.status}`);
  return res.json();
}

export async function retrySourceTranscription(sourceId: string): Promise<SourceStatusDto> {
  const res = await apiFetch(`/api/sources/${sourceId}/retry-transcription`, {
    method: "POST",
  });
  if (!res.ok) throw new Error(`retrySourceTranscription failed: ${res.status}`);
  return res.json();
}

export async function linkSourceToDraft(draftId: string, sourceId: string): Promise<SourceDto> {
  const res = await apiFetch(`/api/drafts/${draftId}/sources/${sourceId}/link`, {
    method: "POST",
  });
  if (!res.ok) throw new Error(`linkSourceToDraft failed: ${res.status}`);
  return res.json();
}

export async function deleteSource(draftId: string, sourceId: string): Promise<void> {
  const res = await apiFetch(`/api/drafts/${draftId}/sources/${sourceId}`, {
    method: "DELETE"
  });
  if (!res.ok) throw new Error(`deleteSource failed: ${res.status}`);
}

export async function importSourceFromUrl(
  draftId: string,
  url: string,
  title?: string,
  content?: string
): Promise<{ sourceId: string; reference: string; title: string | null; kind: string }> {
  const res = await apiFetch(`/api/drafts/${draftId}/sources/import-url`, {
    method: "POST",
    body: JSON.stringify({ url, title, content }),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => `${res.status}`);
    throw new Error(msg || `importSourceFromUrl failed: ${res.status}`);
  }
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
  if (!res.ok) {
    const msg = await res.text().catch(() => `${res.status}`);
    throw new Error(msg || `uploadMedia failed: ${res.status}`);
  }
  return res.json();
}

export async function importMediaFromUrl(
  draftId: string,
  url: string,
  altText?: string
): Promise<{ id: string; markdownTag: string }> {
  const res = await apiFetch(`/api/drafts/${draftId}/media/import-url`, {
    method: "POST",
    body: JSON.stringify({ url, altText }),
  });

  if (!res.ok) {
    const msg = await res.text().catch(() => `${res.status}`);
    throw new Error(msg || `importMediaFromUrl failed: ${res.status}`);
  }

  return res.json();
}

export async function renderCodeImage(
  draftId: string,
  code: string,
  language: string,
  theme: "Dark" | "Light" = "Dark"
): Promise<{ id: string; markdownTag: string }> {
  const res = await apiFetch(`/api/drafts/${draftId}/code-image`, {
    method: "POST",
    body: JSON.stringify({ code, language, theme }),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => `${res.status}`);
    throw new Error(msg || `renderCodeImage failed: ${res.status}`);
  }
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

export async function generateVariants(draftId: string, platforms: string[]): Promise<{ canonical: DraftDto; variants: DraftDto[] }> {
  const res = await apiFetch(`/api/drafts/${draftId}/generate-variants`, {
    method: "POST",
    body: JSON.stringify({ platforms }),
  });
  if (!res.ok) throw new Error(`generateVariants failed: ${res.status}`);
  return res.json();
}

export async function fetchDraftFamily(draftId: string): Promise<{ canonical: DraftDto; variants: DraftDto[] }> {
  const res = await apiFetch(`/api/drafts/${draftId}/family`);
  if (!res.ok) throw new Error(`fetchDraftFamily failed: ${res.status}`);
  return res.json();
}

export async function fetchVariants(draftId: string): Promise<DraftDto[]> {
  const res = await apiFetch(`/api/drafts/${draftId}/variants`);
  if (!res.ok) throw new Error(`fetchVariants failed: ${res.status}`);
  return res.json();
}

export type PlatformCapabilityDto = {
  platform: string;
  supportsReplyTarget: boolean;
};

export async function fetchPlatformCapabilities(): Promise<PlatformCapabilityDto[]> {
  const res = await apiFetch("/api/providers/platform-capabilities");
  if (!res.ok) throw new Error(`fetchPlatformCapabilities failed: ${res.status}`);
  return res.json();
}