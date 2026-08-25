import { apiFetch } from "./client";

export interface TemplateDto {
  id: string;
  name: string;
  description?: string | null;
  width: number;
  height: number;
  schema: string;
  thumbnailMediaAssetId?: string | null;
  thumbnailUrl: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateTemplateRequest {
  name: string;
  description?: string | null;
  width: number;
  height: number;
  schema: string;
  thumbnailMediaAssetId?: string | null;
}

export interface UpdateTemplateRequest {
  name: string;
  description?: string | null;
  width: number;
  height: number;
  schema: string;
  thumbnailMediaAssetId?: string | null;
}

export interface GenerateTemplateRequest {
  prompt: string;
  width?: number;
  height?: number;
}

export interface GeneratedTemplateResponse {
  name: string;
  description?: string | null;
  width: number;
  height: number;
  schema: string;
  previewBase64?: string | null;
}

export interface PreviewTemplateRequest {
  schema: string;
  slots?: Record<string, string>;
}

export async function fetchTemplates(search?: string): Promise<TemplateDto[]> {
  const url = search ? `/api/templates?search=${encodeURIComponent(search)}` : "/api/templates";
  const res = await apiFetch(url);
  if (!res.ok) throw new Error(`fetchTemplates failed: ${res.status}`);
  return res.json();
}

export async function fetchTemplate(id: string): Promise<TemplateDto> {
  const res = await apiFetch(`/api/templates/${id}`);
  if (!res.ok) throw new Error(`fetchTemplate failed: ${res.status}`);
  return res.json();
}

export async function createTemplate(req: CreateTemplateRequest): Promise<TemplateDto> {
  const res = await apiFetch("/api/templates", {
    method: "POST",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `createTemplate failed: ${res.status}`);
  }
  return res.json();
}

export async function updateTemplate(id: string, req: UpdateTemplateRequest): Promise<TemplateDto> {
  const res = await apiFetch(`/api/templates/${id}`, {
    method: "PUT",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `updateTemplate failed: ${res.status}`);
  }
  return res.json();
}

export async function deleteTemplate(id: string): Promise<void> {
  const res = await apiFetch(`/api/templates/${id}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error(`deleteTemplate failed: ${res.status}`);
}

export async function generateTemplate(req: GenerateTemplateRequest): Promise<GeneratedTemplateResponse> {
  const res = await apiFetch("/api/templates/generate", {
    method: "POST",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `generateTemplate failed: ${res.status}`);
  }
  return res.json();
}

export async function previewTemplate(req: PreviewTemplateRequest): Promise<Blob> {
  const res = await apiFetch("/api/templates/preview", {
    method: "POST",
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `previewTemplate failed: ${res.status}`);
  }
  return res.blob();
}

export async function restoreDefaultTemplates(): Promise<TemplateDto[]> {
  const res = await apiFetch("/api/templates/restore-defaults", {
    method: "POST",
  });
  if (!res.ok) {
    const msg = await res.text().catch(() => "");
    throw new Error(msg || `restoreDefaultTemplates failed: ${res.status}`);
  }
  return res.json();
}
