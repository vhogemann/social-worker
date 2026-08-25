import { useState } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faPlus,
  faMicrochip,
  faPen,
  faTrashCan,
  faVial,
  faStar,
  faPowerOff,
  faCheck,
} from "@fortawesome/free-solid-svg-icons";
import { useProvidersManager } from "./useProvidersManager";
import { Modal } from "../common/Modal";
import { SettingsSection } from "./common/SettingsSection";
import { SettingsEmptyState } from "./common/SettingsEmptyState";
import { SettingsAlert } from "./common/SettingsAlert";

export function ProvidersTab() {
  const {
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
    handleDelete,
  } = useProvidersManager();

  const [isModalOpen, setIsModalOpen] = useState(false);

  const confirmDelete = async (id: string) => {
    if (!confirm("Are you sure you want to delete this provider?")) return;
    await handleDelete(id);
  };

  const handleOpenAdd = () => {
    handleCancelEdit();
    setIsModalOpen(true);
  };

  const handleOpenEdit = (p: Parameters<typeof handleStartEdit>[0]) => {
    handleStartEdit(p);
    setIsModalOpen(true);
  };

  const handleCloseModal = () => {
    if (loading || testing) return;
    setIsModalOpen(false);
    handleCancelEdit();
  };

  const onFormSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const success = await handleSubmit(e);
    if (success) {
      setIsModalOpen(false);
    }
  };

  return (
    <div className="space-y-6 font-sans">
      {error && !isModalOpen && (
        <SettingsAlert type="error" message={error} onDismiss={() => setError(null)} />
      )}

      <SettingsSection
        title="Configured LLM Providers"
        description="Manage AI models and endpoints used for drafting, brainstorming, and workflow automation."
        action={
          <button
            type="button"
            onClick={handleOpenAdd}
            className="inline-flex items-center gap-1.5 bg-accent text-bg font-medium text-xs py-1.5 px-3.5 rounded hover:opacity-90 transition-opacity font-mono uppercase tracking-wider shadow-sm"
          >
            <FontAwesomeIcon icon={faPlus} className="w-3 h-3" />
            <span>Add Provider</span>
          </button>
        }
      >
        {loading && providers.length === 0 ? (
          <div className="p-6 text-center text-xs text-muted font-mono">
            Loading LLM providers...
          </div>
        ) : providers.length === 0 ? (
          <SettingsEmptyState
            icon={faMicrochip}
            title="No LLM providers configured"
            description="Add an OpenRouter or local Ollama provider to power AI text generation and thread workflows."
            actionLabel="+ Add Provider"
            onAction={handleOpenAdd}
          />
        ) : (
          <div className="space-y-2.5">
            {providers.map((p) => (
              <div
                key={p.id}
                className="p-4 border border-border bg-bg/20 rounded-lg hover:border-border/80 transition-colors flex flex-col md:flex-row md:items-center justify-between gap-4"
              >
                <div className="space-y-1.5 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-sm font-medium text-foreground">{p.name}</span>
                    {p.isDefault && (
                      <span className="text-[10px] font-mono uppercase tracking-wider bg-accent/15 text-accent border border-accent/30 px-1.5 py-0.5 rounded inline-flex items-center gap-1">
                        <FontAwesomeIcon icon={faStar} className="w-2.5 h-2.5" />
                        <span>Default</span>
                      </span>
                    )}
                    <span
                      className={`text-[10px] font-mono uppercase tracking-wider px-1.5 py-0.5 rounded border ${
                        p.isActive
                          ? "bg-green-950/30 border-green-800/40 text-green-400"
                          : "bg-border/20 border-border text-muted"
                      }`}
                    >
                      {p.isActive ? "active" : "inactive"}
                    </span>
                    <span className="text-[10px] font-mono uppercase tracking-wider px-1.5 py-0.5 rounded bg-panel border border-border text-muted">
                      {p.providerType}
                    </span>
                  </div>

                  <div className="text-xs text-muted font-mono flex items-center gap-3 flex-wrap">
                    <span className="text-foreground/90 font-medium">{p.model}</span>
                    <span>•</span>
                    <span className="truncate max-w-xs">{p.baseUrl}</span>
                    {p.contextWindowTokens && (
                      <>
                        <span>•</span>
                        <span>{p.contextWindowTokens.toLocaleString()} tokens</span>
                      </>
                    )}
                  </div>
                </div>

                <div className="flex items-center gap-1.5 shrink-0 flex-wrap">
                  {!p.isDefault && (
                    <button
                      onClick={() => handleSetDefault(p)}
                      title="Set as system default provider"
                      className="px-2.5 py-1 text-[11px] font-mono border border-border text-muted hover:text-foreground hover:bg-border/20 rounded uppercase tracking-wider transition-colors inline-flex items-center gap-1"
                    >
                      <FontAwesomeIcon icon={faCheck} className="w-2.5 h-2.5" />
                      <span>make default</span>
                    </button>
                  )}
                  <button
                    onClick={() => handleToggleActive(p)}
                    disabled={p.isDefault}
                    className={`px-2.5 py-1 text-[11px] font-mono border rounded uppercase tracking-wider transition-colors disabled:opacity-50 inline-flex items-center gap-1 ${
                      p.isActive
                        ? "border-amber-900/30 text-amber-400 hover:bg-amber-950/20"
                        : "border-green-900/30 text-green-400 hover:bg-green-950/20"
                    }`}
                  >
                    <FontAwesomeIcon icon={faPowerOff} className="w-2.5 h-2.5" />
                    <span>{p.isActive ? "deactivate" : "activate"}</span>
                  </button>
                  <button
                    onClick={() => handleOpenEdit(p)}
                    className="px-2.5 py-1 text-[11px] font-mono border border-border text-muted hover:text-foreground hover:bg-border/20 rounded uppercase tracking-wider transition-colors inline-flex items-center gap-1"
                  >
                    <FontAwesomeIcon icon={faPen} className="w-2.5 h-2.5" />
                    <span>edit</span>
                  </button>
                  <button
                    onClick={() => { void confirmDelete(p.id); }}
                    disabled={p.isDefault}
                    className="px-2.5 py-1 text-[11px] font-mono border border-red-900/30 text-red-400 hover:text-red-300 hover:bg-red-950/20 rounded uppercase tracking-wider transition-colors disabled:opacity-50 inline-flex items-center gap-1"
                  >
                    <FontAwesomeIcon icon={faTrashCan} className="w-2.5 h-2.5" />
                    <span>delete</span>
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </SettingsSection>

      {/* Provider Modal */}
      <Modal
        isOpen={isModalOpen}
        onClose={handleCloseModal}
        title={editingId ? `Edit AI Provider: ${providers.find((p) => p.id === editingId)?.name || ""}` : "Add AI Provider"}
        description="Configure an OpenAI-compatible API endpoint, local Ollama instance, or OpenRouter model."
        maxWidth="lg"
      >
        {error && (
          <SettingsAlert type="error" message={error} onDismiss={() => setError(null)} />
        )}

        <form onSubmit={onFormSubmit} className="space-y-4">
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
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                Provider Type
              </label>
              <select
                value={providerType}
                onChange={(e) => handleTypeChange(e.target.value)}
                disabled={loading}
                className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
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
                onChange={(e) => {
                  setModel(e.target.value);
                  setTestResult(null);
                }}
                disabled={loading}
                placeholder="e.g. anthropic/claude-3.5-sonnet"
                list="models-list"
                className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
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
              placeholder="Leave blank to infer automatically"
              min="1"
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
            />
          </div>

          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
              Base URL
            </label>
            <input
              type="text"
              value={baseUrl}
              onChange={(e) => {
                setBaseUrl(e.target.value);
                setTestResult(null);
              }}
              disabled={loading}
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-mono"
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
                onChange={(e) => {
                  setApiKey(e.target.value);
                  setTestResult(null);
                }}
                disabled={loading}
                placeholder={editingId ? "Leave blank to keep existing key" : "Write-only API key"}
                className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
              />
            </div>
          )}

          {testResult && (
            <SettingsAlert
              type={testResult.success ? "success" : "error"}
              message={testResult.message}
              onDismiss={() => setTestResult(null)}
            />
          )}

          <div className="flex items-center justify-between gap-3 pt-3 border-t border-border flex-wrap">
            <button
              type="button"
              onClick={handleTestConnection}
              disabled={loading || testing}
              className="border border-border text-foreground font-medium text-xs py-2 px-3.5 rounded-lg hover:bg-border/20 transition-colors disabled:opacity-50 font-mono uppercase tracking-wider inline-flex items-center gap-1.5"
            >
              <FontAwesomeIcon icon={faVial} className="w-3 h-3 text-accent" />
              <span>{testing ? "testing..." : "test connection"}</span>
            </button>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={handleCloseModal}
                disabled={loading || testing}
                className="border border-border text-foreground font-medium text-xs py-2 px-4 rounded-lg hover:bg-border/20 transition-colors disabled:opacity-50 font-mono uppercase tracking-wider"
              >
                cancel
              </button>
              <button
                type="submit"
                disabled={loading || testing}
                className="bg-accent text-bg font-medium text-xs py-2 px-4 rounded-lg hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider shadow-sm"
              >
                {loading ? "saving..." : editingId ? "save changes" : "add provider"}
              </button>
            </div>
          </div>
        </form>
      </Modal>
    </div>
  );
}
