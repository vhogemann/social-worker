import { apiFetch } from "./client";

export interface BrandVoicePromptDto {
  id: string;
  name: string;
  body: string;
  isDefault: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface BrandVoicePromptRequest {
  name: string;
  body: string;
  isDefault: boolean;
}

export async function listBrandVoices(): Promise<BrandVoicePromptDto[]> {
  const res = await apiFetch("/api/brand-prompts");
  if (!res.ok) throw new Error("Failed to fetch brand voices");
  return res.json();
}

export async function createBrandVoice(req: BrandVoicePromptRequest): Promise<BrandVoicePromptDto> {
  const res = await apiFetch("/api/brand-prompts", {
    method: "POST",
    body: JSON.stringify(req),
  });
  if (!res.ok) throw new Error("Failed to create brand voice");
  return res.json();
}

export async function updateBrandVoice(id: string, req: BrandVoicePromptRequest): Promise<BrandVoicePromptDto> {
  const res = await apiFetch(`/api/brand-prompts/${id}`, {
    method: "PUT",
    body: JSON.stringify(req),
  });
  if (!res.ok) throw new Error("Failed to update brand voice");
  return res.json();
}

export async function deleteBrandVoice(id: string): Promise<void> {
  const res = await apiFetch(`/api/brand-prompts/${id}`, {
    method: "DELETE",
  });
  if (!res.ok) throw new Error("Failed to delete brand voice");
}
