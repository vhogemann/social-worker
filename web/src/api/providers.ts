import { apiFetch } from "./client";

export interface LlmProviderDto {
  id: string;
  name: string;
  providerType: string;
  baseUrl: string;
  apiKeySet: boolean;
  model: string;
  isDefault: boolean;
  isActive: boolean;
  supportsVision: boolean;
  supportsTools: boolean;
}

export interface CreateProviderRequest {
  name: string;
  providerType: string;
  baseUrl: string;
  apiKey: string;
  model: string;
}

export interface UpdateProviderRequest {
  name?: string;
  providerType?: string;
  baseUrl?: string;
  apiKey?: string;
  model?: string;
  isDefault?: boolean;
  isActive?: boolean;
}

export interface AvailableProviderDto {
  id: string;
  name: string;
  providerType: string;
  model: string;
}

export async function listProviders(): Promise<LlmProviderDto[]> {
  const res = await apiFetch("/api/providers");
  if (!res.ok) {
    throw new Error("Failed to load providers");
  }
  return res.json();
}

export async function listAvailableProviders(): Promise<AvailableProviderDto[]> {
  const res = await apiFetch("/api/providers/available");
  if (!res.ok) {
    throw new Error("Failed to load available providers");
  }
  return res.json();
}

export async function createProvider(req: CreateProviderRequest): Promise<LlmProviderDto> {
  const res = await apiFetch("/api/providers", {
    method: "POST",
    body: JSON.stringify(req)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || "Failed to create provider");
  }
  return res.json();
}

export async function updateProvider(id: string, req: UpdateProviderRequest): Promise<LlmProviderDto> {
  const res = await apiFetch(`/api/providers/${id}`, {
    method: "PATCH",
    body: JSON.stringify(req)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || "Failed to update provider");
  }
  return res.json();
}

export async function deleteProvider(id: string): Promise<void> {
  const res = await apiFetch(`/api/providers/${id}`, {
    method: "DELETE"
  });
  if (!res.ok) {
    throw new Error("Failed to delete provider");
  }
}

export interface TestProviderRequest {
  providerType: string;
  baseUrl: string;
  apiKey: string;
  model: string;
}

export interface TestProviderResponse {
  success: boolean;
  error?: string | null;
}

export async function testProvider(req: TestProviderRequest): Promise<TestProviderResponse> {
  const res = await apiFetch("/api/providers/test", {
    method: "POST",
    body: JSON.stringify(req)
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({}));
    throw new Error(err.error || "Failed to test provider");
  }
  return res.json();
}
