import { useCallback, useEffect, useMemo, useState } from "react";
import {
  listProviders,
  createProvider,
  updateProvider,
  deleteProvider,
  testProvider,
  type LlmProviderDto
} from "../../api/providers";

export function useProvidersManager() {
  const [providers, setProviders] = useState<LlmProviderDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);

  const [name, setName] = useState("");
  const [providerType, setProviderType] = useState("OpenRouter");
  const [baseUrl, setBaseUrl] = useState("https://openrouter.ai/api/v1");
  const [apiKey, setApiKey] = useState("");
  const [model, setModel] = useState("");
  const [contextWindowTokens, setContextWindowTokens] = useState("");
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);

  const openRouterModels = [
    "anthropic/claude-3.5-sonnet",
    "meta-llama/llama-3-8b-instruct:free",
    "google/gemini-pro",
    "openai/gpt-4o-mini",
    "openai/gpt-4o"
  ];

  const ollamaModels = ["llama3.1", "llama3", "mistral", "phi3", "gemma2"];

  const suggestedModels = useMemo(
    () => (providerType === "Ollama" ? ollamaModels : openRouterModels),
    [providerType]
  );

  const fetchProviders = useCallback(async () => {
    try {
      const data = await listProviders();
      setProviders(data);
    } catch (err: any) {
      setError(err.message || "Failed to load providers");
    }
  }, []);

  useEffect(() => {
    fetchProviders();
  }, [fetchProviders]);

  const handleTypeChange = (type: string) => {
    setProviderType(type);
    setTestResult(null);
    if (type === "Ollama") {
      setBaseUrl("http://ollama:11434/v1");
      setApiKey("");
    } else {
      setBaseUrl("https://openrouter.ai/api/v1");
    }
  };

  const handleStartEdit = (p: LlmProviderDto) => {
    setEditingId(p.id);
    setName(p.name);
    setProviderType(p.providerType);
    setBaseUrl(p.baseUrl);
    setModel(p.model);
    setContextWindowTokens(p.contextWindowTokens ? String(p.contextWindowTokens) : "");
    setApiKey("");
    setTestResult(null);
    setError(null);
  };

  const handleCancelEdit = () => {
    setEditingId(null);
    setName("");
    setApiKey("");
    setModel("");
    setContextWindowTokens("");
    if (providerType === "Ollama") {
      setBaseUrl("http://ollama:11434/v1");
    } else {
      setBaseUrl("https://openrouter.ai/api/v1");
    }
    setTestResult(null);
    setError(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !baseUrl.trim() || !model.trim()) {
      setError("Name, Base URL, and Model are required.");
      return;
    }

    setLoading(true);
    setError(null);

    try {
      if (editingId) {
        await updateProvider(editingId, {
          name: name.trim(),
          providerType,
          baseUrl: baseUrl.trim(),
          apiKey: providerType === "Ollama" ? "" : apiKey.trim() || undefined,
          model: model.trim(),
          contextWindowTokens: contextWindowTokens.trim() ? Number(contextWindowTokens.trim()) : undefined
        });
        setEditingId(null);
      } else {
        await createProvider({
          name: name.trim(),
          providerType,
          baseUrl: baseUrl.trim(),
          apiKey: providerType === "Ollama" ? "" : apiKey.trim(),
          model: model.trim(),
          contextWindowTokens: contextWindowTokens.trim() ? Number(contextWindowTokens.trim()) : undefined
        });
      }

      setName("");
      setApiKey("");
      setModel("");
      setContextWindowTokens("");
      if (providerType === "Ollama") {
        setBaseUrl("http://ollama:11434/v1");
      } else {
        setBaseUrl("https://openrouter.ai/api/v1");
      }
      setTestResult(null);

      await fetchProviders();
    } catch (err: any) {
      setError(err.message || "Failed to save provider");
    } finally {
      setLoading(false);
    }
  };

  const handleTestConnection = async () => {
    if (!baseUrl.trim() || !model.trim()) {
      setError("Base URL and Model are required to test connection.");
      return;
    }

    setTesting(true);
    setError(null);
    setTestResult(null);

    try {
      const res = await testProvider({
        providerType,
        baseUrl: baseUrl.trim(),
        apiKey: providerType === "Ollama" ? "" : apiKey.trim(),
        model: model.trim(),
        contextWindowTokens: contextWindowTokens.trim() ? Number(contextWindowTokens.trim()) : undefined
      });

      if (res.success) {
        const contextMsg = res.contextWindowTokens
          ? ` Inferred context window: ${res.contextWindowTokens.toLocaleString()} tokens.`
          : "";
        setTestResult({ success: true, message: `Connection successful!${contextMsg}` });
      } else {
        setTestResult({ success: false, message: res.error || "Connection failed." });
      }
    } catch (err: any) {
      setTestResult({ success: false, message: err.message || "Connection failed." });
    } finally {
      setTesting(false);
    }
  };

  const handleToggleActive = async (provider: LlmProviderDto) => {
    setError(null);
    try {
      await updateProvider(provider.id, { isActive: !provider.isActive });
      await fetchProviders();
    } catch (err: any) {
      setError(err.message || "Failed to update provider status");
    }
  };

  const handleSetDefault = async (provider: LlmProviderDto) => {
    setError(null);
    try {
      await updateProvider(provider.id, { isDefault: true });
      await fetchProviders();
    } catch (err: any) {
      setError(err.message || "Failed to set default provider");
    }
  };

  const handleDelete = async (id: string) => {
    setError(null);
    try {
      await deleteProvider(id);
      await fetchProviders();
    } catch (err: any) {
      setError(err.message || "Failed to delete provider");
    }
  };

  return {
    providers,
    loading,
    error,
    editingId,
    name,
    providerType,
    baseUrl,
    apiKey,
    model,
    contextWindowTokens,
    testing,
    testResult,
    suggestedModels,
    setName,
    setBaseUrl,
    setApiKey,
    setModel,
    setContextWindowTokens,
    setError,
    setTestResult,
    handleTypeChange,
    handleStartEdit,
    handleCancelEdit,
    handleSubmit,
    handleTestConnection,
    handleToggleActive,
    handleSetDefault,
    handleDelete
  };
}
