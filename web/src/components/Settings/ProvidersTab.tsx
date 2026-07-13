import { useState, useEffect } from "react";
import {
  listProviders,
  createProvider,
  updateProvider,
  deleteProvider,
  testProvider,
  type LlmProviderDto
} from "../../api/providers";

export function ProvidersTab() {
  const [providers, setProviders] = useState<LlmProviderDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);

  // Form states
  const [name, setName] = useState("");
  const [providerType, setProviderType] = useState("OpenRouter");
  const [baseUrl, setBaseUrl] = useState("https://openrouter.ai/api/v1");
  const [apiKey, setApiKey] = useState("");
  const [model, setModel] = useState("");
  const [contextWindowTokens, setContextWindowTokens] = useState("");
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);

  const fetchProviders = async () => {
    try {
      const data = await listProviders();
      setProviders(data);
    } catch (err: any) {
      setError(err.message || "Failed to load providers");
    }
  };

  useEffect(() => {
    fetchProviders();
  }, []);

  const openRouterModels = [
    "anthropic/claude-3.5-sonnet",
    "meta-llama/llama-3-8b-instruct:free",
    "google/gemini-pro",
    "openai/gpt-4o-mini",
    "openai/gpt-4o"
  ];

  const ollamaModels = [
    "llama3.1",
    "llama3",
    "mistral",
    "phi3",
    "gemma2"
  ];

  const suggestedModels = providerType === "Ollama" ? ollamaModels : openRouterModels;

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
        const contextMsg = res.contextWindowTokens ? ` Inferred context window: ${res.contextWindowTokens.toLocaleString()} tokens.` : "";
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
          apiKey: providerType === "Ollama" ? "" : (apiKey.trim() || undefined),
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

      // Reset form
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
    if (!confirm("Are you sure you want to delete this provider?")) return;
    setError(null);
    try {
      await deleteProvider(id);
      await fetchProviders();
    } catch (err: any) {
      setError(err.message || "Failed to delete provider");
    }
  };

  return (
    <div className="space-y-6 font-sans">
      {error && (
        <div className="p-3 bg-red-950/40 border border-red-800 text-red-400 text-xs rounded font-mono">
          {error}
        </div>
      )}

      <div className="bg-bg/40 border border-border rounded overflow-hidden">
        <div className="px-4 py-3 border-b border-border bg-bg/20">
          <h3 className="text-xs font-mono uppercase tracking-wider text-muted">
            configured llm providers
          </h3>
        </div>
        <div className="divide-y divide-border">
          {providers.map((p) => (
            <div key={p.id} className="p-4 flex items-center justify-between hover:bg-bg/20 transition-colors">
              <div className="space-y-1">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-medium text-foreground">{p.name}</span>
                  {p.isDefault && (
                    <span className="bg-accent/15 text-accent border border-accent/30 text-[10px] font-mono px-1.5 py-0.5 rounded uppercase tracking-wider">
                      default
                    </span>
                  )}
                  {!p.isActive && (
                    <span className="bg-muted/10 text-muted border border-muted/20 text-[10px] font-mono px-1.5 py-0.5 rounded uppercase tracking-wider">
                      inactive
                    </span>
                  )}
                </div>
                <div className="text-xs text-muted font-mono space-y-0.5">
                  <div>Type: <span className="text-foreground">{p.providerType}</span></div>
                  <div className="truncate max-w-lg">Base URL: <span className="text-foreground">{p.baseUrl}</span></div>
                  <div>Model: <span className="text-foreground">{p.model}</span></div>
                  <div>Context Window: <span className="text-foreground">{p.contextWindowTokens ? p.contextWindowTokens.toLocaleString() : "unknown"}</span></div>
                  <div>Key Configured: <span className="text-foreground">{p.apiKeySet ? "Yes" : "No"}</span></div>
                  <div className="flex gap-2.5 mt-1">
                    <span className={`text-[10px] px-1.5 py-0.5 rounded font-mono uppercase tracking-wider ${p.supportsVision ? 'bg-indigo-950/30 text-indigo-400 border border-indigo-900/30' : 'bg-zinc-950/30 text-zinc-500 border border-zinc-900/30'}`}>
                      vision: {p.supportsVision ? 'yes' : 'no'}
                    </span>
                    <span className={`text-[10px] px-1.5 py-0.5 rounded font-mono uppercase tracking-wider ${p.supportsTools ? 'bg-indigo-950/30 text-indigo-400 border border-indigo-900/30' : 'bg-zinc-950/30 text-zinc-500 border border-zinc-900/30'}`}>
                      tools: {p.supportsTools ? 'yes' : 'no'}
                    </span>
                  </div>
                </div>
              </div>

              <div className="flex items-center gap-2">
                {!p.isDefault && (
                  <button
                    onClick={() => handleSetDefault(p)}
                    className="px-2.5 py-1 text-[11px] font-mono border border-border text-muted hover:text-foreground hover:bg-border/20 rounded uppercase tracking-wider transition-colors"
                  >
                    make default
                  </button>
                )}
                <button
                  onClick={() => handleToggleActive(p)}
                  disabled={p.isDefault}
                  className={`px-2.5 py-1 text-[11px] font-mono border rounded uppercase tracking-wider transition-colors disabled:opacity-50 ${
                    p.isActive
                      ? "border-amber-900/30 text-amber-400 hover:bg-amber-950/20"
                      : "border-green-900/30 text-green-400 hover:bg-green-950/20"
                  }`}
                >
                  {p.isActive ? "deactivate" : "activate"}
                </button>
                <button
                  onClick={() => handleStartEdit(p)}
                  className="px-2.5 py-1 text-[11px] font-mono border border-border text-muted hover:text-foreground hover:bg-border/20 rounded uppercase tracking-wider transition-colors"
                >
                  edit
                </button>
                <button
                  onClick={() => handleDelete(p.id)}
                  disabled={p.isDefault}
                  className="px-2.5 py-1 text-[11px] font-mono border border-red-900/30 text-red-400 hover:text-red-300 hover:bg-red-950/20 rounded uppercase tracking-wider transition-colors disabled:opacity-50"
                >
                  delete
                </button>
              </div>
            </div>
          ))}
          {providers.length === 0 && (
            <div className="p-6 text-center text-xs text-muted font-mono">
              No LLM providers configured.
            </div>
          )}
        </div>
      </div>

      <div className="bg-bg/40 border border-border p-4 rounded">
        <h3 className="text-xs font-mono uppercase tracking-wider text-muted mb-3">
          {editingId ? `edit ai provider: ${providers.find(p => p.id === editingId)?.name}` : "add new ai provider"}
        </h3>

        <form onSubmit={handleSubmit} className="space-y-4 max-w-md">
          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Provider Name
            </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              disabled={loading}
              placeholder="e.g. OpenRouter Claude"
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                Provider Type
              </label>
              <select
                value={providerType}
                onChange={(e) => handleTypeChange(e.target.value)}
                disabled={loading}
                className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
              >
                <option value="OpenRouter">OpenRouter</option>
                <option value="Ollama">Ollama</option>
              </select>
            </div>

            <div>
              <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                Model Identifier
              </label>
              <input
                type="text"
                value={model}
                onChange={(e) => { setModel(e.target.value); setTestResult(null); }}
                disabled={loading}
                placeholder="e.g. anthropic/claude-3.5-sonnet"
                list="models-list"
                className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
              />
              <datalist id="models-list">
                {suggestedModels.map((m) => (
                  <option key={m} value={m} />
                ))}
              </datalist>
            </div>
          </div>

          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Context Window Tokens Override
            </label>
            <input
              type="number"
              value={contextWindowTokens}
              onChange={(e) => setContextWindowTokens(e.target.value)}
              disabled={loading}
              placeholder="Leave blank to infer"
              min="1"
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
            />
          </div>

          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Base URL
            </label>
            <input
              type="text"
              value={baseUrl}
              onChange={(e) => { setBaseUrl(e.target.value); setTestResult(null); }}
              disabled={loading}
              className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-mono"
            />
          </div>

          {providerType !== "Ollama" && (
            <div>
              <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                API Key
              </label>
              <input
                type="password"
                value={apiKey}
                onChange={(e) => { setApiKey(e.target.value); setTestResult(null); }}
                disabled={loading}
                placeholder="Write-only API key"
                className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
              />
            </div>
          )}

          {testResult && (
            <div className={`p-3 border text-xs rounded font-mono ${
              testResult.success
                ? "bg-green-950/20 border-green-800 text-green-400"
                : "bg-red-950/20 border-red-900/40 text-red-400"
            }`}>
              {testResult.message}
            </div>
          )}

          <div className="flex items-center gap-3">
            <button
              type="submit"
              disabled={loading || testing}
              className="bg-accent text-bg font-medium text-xs py-1.5 px-4 rounded hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider"
            >
              {loading ? "saving..." : (editingId ? "save changes" : "add provider")}
            </button>
            {editingId && (
              <button
                type="button"
                onClick={handleCancelEdit}
                disabled={loading || testing}
                className="border border-border text-foreground font-medium text-xs py-1.5 px-4 rounded hover:bg-border/20 transition-colors disabled:opacity-50 font-mono uppercase tracking-wider"
              >
                cancel
              </button>
            )}
            <button
              type="button"
              onClick={handleTestConnection}
              disabled={loading || testing}
              className="border border-border text-foreground font-medium text-xs py-1.5 px-4 rounded hover:bg-border/20 transition-colors disabled:opacity-50 font-mono uppercase tracking-wider"
            >
              {testing ? "testing..." : "test connection"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
