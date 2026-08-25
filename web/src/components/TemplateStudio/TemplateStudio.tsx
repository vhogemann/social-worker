import { useState, useEffect } from "react";
import {
  fetchTemplates,
  deleteTemplate,
  restoreDefaultTemplates,
  createTemplate,
  TemplateDto,
  GeneratedTemplateResponse,
} from "../../api/templates";
import { AiTemplateGenerateModal } from "./AiTemplateGenerateModal";
import { TemplateEditorModal } from "./TemplateEditorModal";

export function TemplateStudio() {
  const [templates, setTemplates] = useState<TemplateDto[]>([]);
  const [search, setSearch] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Modals state
  const [isAiModalOpen, setIsAiModalOpen] = useState(false);
  const [isEditorModalOpen, setIsEditorModalOpen] = useState(false);
  const [editingTemplate, setEditingTemplate] = useState<(TemplateDto | GeneratedTemplateResponse) | null>(null);
  const [isRestoring, setIsRestoring] = useState(false);

  const loadTemplates = async () => {
    try {
      setIsLoading(true);
      setError(null);
      const data = await fetchTemplates(search.trim() || undefined);
      setTemplates(data);
    } catch (err: any) {
      setError(err?.message || "Failed to load templates");
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    const timer = setTimeout(() => {
      loadTemplates();
    }, 200);
    return () => clearTimeout(timer);
  }, [search]);

  const handleDelete = async (id: string, name: string) => {
    if (!window.confirm(`Are you sure you want to delete template "${name}"?`)) return;

    try {
      await deleteTemplate(id);
      setTemplates((prev) => prev.filter((t) => t.id !== id));
    } catch (err: any) {
      alert(`Delete failed: ${err.message}`);
    }
  };

  const handleDuplicate = async (t: TemplateDto) => {
    try {
      const duplicated = await createTemplate({
        name: `${t.name} (Copy)`,
        description: t.description,
        width: t.width,
        height: t.height,
        schema: t.schema,
      });
      setTemplates((prev) => [duplicated, ...prev]);
    } catch (err: any) {
      alert(`Duplicate failed: ${err.message}`);
    }
  };

  const handleRestoreDefaults = async () => {
    try {
      setIsRestoring(true);
      const restored = await restoreDefaultTemplates();
      setTemplates(restored);
    } catch (err: any) {
      alert(`Restore defaults failed: ${err.message}`);
    } finally {
      setIsRestoring(false);
    }
  };

  const handleSaved = (saved: TemplateDto) => {
    setTemplates((prev) => {
      const idx = prev.findIndex((t) => t.id === saved.id);
      if (idx >= 0) {
        const next = [...prev];
        next[idx] = saved;
        return next;
      }
      return [saved, ...prev];
    });
  };

  const handleEditInStudio = (generated: GeneratedTemplateResponse) => {
    setIsAiModalOpen(false);
    setEditingTemplate(generated);
    setIsEditorModalOpen(true);
  };

  const parseSlots = (schemaJson: string) => {
    try {
      const parsed = JSON.parse(schemaJson);
      if (Array.isArray(parsed.slots)) {
        return parsed.slots.map((s: any) => s.name || s);
      }
    } catch {
      // Ignore
    }
    return [];
  };

  return (
    <div className="flex-1 min-h-0 flex flex-col bg-bg overflow-y-auto text-foreground">
      {/* Top action bar */}
      <div className="px-8 py-6 border-b border-border flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
        <div>
          <h1 className="text-xl font-bold text-foreground">Template Studio</h1>
          <p className="text-xs text-muted mt-1">
            Declarative graphic canvas templates for social post cards, quotes, metrics, and banners.
          </p>
        </div>

        <div className="flex items-center gap-3 w-full md:w-auto">
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search templates..."
            className="bg-panel border border-border rounded px-3 py-1.5 text-xs text-foreground placeholder:text-muted/60 focus:outline-none focus:border-accent w-full md:w-56"
          />

          <button
            type="button"
            onClick={handleRestoreDefaults}
            disabled={isRestoring}
            className="px-3 py-1.5 border border-border hover:bg-panel text-muted hover:text-foreground text-xs font-mono rounded shrink-0 transition-colors"
            title="Restore missing default templates"
          >
            {isRestoring ? "Restoring..." : "Restore Defaults"}
          </button>

          <button
            type="button"
            onClick={() => setIsAiModalOpen(true)}
            className="px-3 py-1.5 bg-accent/15 border border-accent/30 text-accent hover:bg-accent/25 text-xs font-mono font-medium rounded shrink-0 flex items-center gap-1.5 transition-colors"
          >
            <span>✦</span> AI Generator
          </button>

          <button
            type="button"
            onClick={() => {
              setEditingTemplate(null);
              setIsEditorModalOpen(true);
            }}
            className="px-3 py-1.5 bg-accent text-accent-foreground font-medium rounded text-xs font-mono hover:opacity-90 shrink-0"
          >
            + New Template
          </button>
        </div>
      </div>

      {/* Main Grid View */}
      <div className="p-8 flex-1">
        {error && (
          <div className="mb-6 p-4 bg-red-500/10 border border-red-500/20 text-red-400 text-sm rounded">
            {error}
          </div>
        )}

        {isLoading ? (
          <div className="text-xs font-mono text-muted text-center py-16">
            Loading templates...
          </div>
        ) : templates.length === 0 ? (
          <div className="border border-dashed border-border rounded-xl p-16 text-center max-w-lg mx-auto bg-panel">
            <p className="text-sm text-foreground font-semibold">No templates found</p>
            <p className="text-xs text-muted mt-1 mb-4">
              Get started by creating a new template, generating one with AI, or restoring defaults.
            </p>
            <div className="flex justify-center gap-3">
              <button
                type="button"
                onClick={handleRestoreDefaults}
                className="px-3 py-1.5 border border-border text-xs font-mono rounded hover:bg-bg"
              >
                Restore Defaults
              </button>
              <button
                type="button"
                onClick={() => setIsAiModalOpen(true)}
                className="px-3 py-1.5 bg-accent text-accent-foreground text-xs font-mono rounded hover:opacity-90"
              >
                Generate with AI
              </button>
            </div>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {templates.map((t) => {
              const slots = parseSlots(t.schema);
              return (
                <div
                  key={t.id}
                  className="bg-panel border border-border rounded-xl overflow-hidden shadow-sm hover:border-accent/50 transition-all flex flex-col group"
                >
                  {/* Card Thumbnail Preview */}
                  <div className="relative aspect-video bg-black/60 flex items-center justify-center overflow-hidden border-b border-border/60">
                    <img
                      src={t.thumbnailUrl}
                      alt={t.name}
                      className="w-full h-full object-cover transition-transform group-hover:scale-105"
                      loading="lazy"
                      onError={(e) => {
                        // Fallback image if render failed
                        (e.target as HTMLElement).style.display = "none";
                      }}
                    />
                    <div className="absolute top-2 right-2 flex gap-1">
                      <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-black/70 text-zinc-300 backdrop-blur-sm border border-white/10">
                        {t.width}x{t.height}
                      </span>
                    </div>
                  </div>

                  {/* Card Info */}
                  <div className="p-4 flex-1 flex flex-col justify-between space-y-3">
                    <div>
                      <h3 className="font-semibold text-sm text-foreground">{t.name}</h3>
                      {t.description && (
                        <p className="text-xs text-muted line-clamp-2 mt-1">{t.description}</p>
                      )}
                    </div>

                    {slots.length > 0 && (
                      <div className="flex flex-wrap gap-1">
                        {slots.slice(0, 4).map((slot: string) => (
                          <span
                            key={slot}
                            className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-muted/20 text-muted"
                          >
                            {slot}
                          </span>
                        ))}
                        {slots.length > 4 && (
                          <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-muted/10 text-muted">
                            +{slots.length - 4} more
                          </span>
                        )}
                      </div>
                    )}

                    {/* Card Actions */}
                    <div className="pt-2 border-t border-border/40 flex items-center justify-between text-xs font-mono">
                      <div className="flex gap-2">
                        <button
                          type="button"
                          onClick={() => {
                            setEditingTemplate(t);
                            setIsEditorModalOpen(true);
                          }}
                          className="text-accent hover:underline"
                        >
                          Edit
                        </button>
                        <span className="text-muted/40">·</span>
                        <button
                          type="button"
                          onClick={() => handleDuplicate(t)}
                          className="text-muted hover:text-foreground"
                        >
                          Duplicate
                        </button>
                      </div>

                      <button
                        type="button"
                        onClick={() => handleDelete(t.id, t.name)}
                        className="text-muted/60 hover:text-destructive transition-colors"
                        title="Delete template"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Modals */}
      <AiTemplateGenerateModal
        isOpen={isAiModalOpen}
        onClose={() => setIsAiModalOpen(false)}
        onSaved={handleSaved}
        onEditInStudio={handleEditInStudio}
      />

      <TemplateEditorModal
        isOpen={isEditorModalOpen}
        template={editingTemplate}
        onClose={() => {
          setIsEditorModalOpen(false);
          setEditingTemplate(null);
        }}
        onSaved={handleSaved}
      />
    </div>
  );
}
