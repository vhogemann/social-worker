import { useState, useEffect } from "react";
import {
  type BrandVoicePromptDto,
  listBrandVoices,
  createBrandVoice,
  updateBrandVoice,
  deleteBrandVoice
} from "../../api/brandVoices";

export function BrandVoicesTab() {
  const [voices, setVoices] = useState<BrandVoicePromptDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form State
  const [editingId, setEditingId] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [body, setBody] = useState("");
  const [isDefault, setIsDefault] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isAdding, setIsAdding] = useState(false);

  const fetchVoices = async () => {
    try {
      setLoading(true);
      const data = await listBrandVoices();
      setVoices(data);
    } catch (err: any) {
      setError(err.message || "Failed to load brand voices");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchVoices();
  }, []);

  const handleStartAdd = () => {
    setEditingId(null);
    setName("");
    setBody("");
    setIsDefault(false);
    setIsAdding(true);
    setError(null);
  };

  const handleStartEdit = (voice: BrandVoicePromptDto) => {
    setEditingId(voice.id);
    setName(voice.name);
    setBody(voice.body);
    setIsDefault(voice.isDefault);
    setIsAdding(true);
    setError(null);
  };

  const handleCancel = () => {
    setIsAdding(false);
    setEditingId(null);
    setName("");
    setBody("");
    setIsDefault(false);
    setError(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !body.trim()) return;

    try {
      setIsSubmitting(true);
      setError(null);

      if (editingId) {
        await updateBrandVoice(editingId, { name, body, isDefault });
      } else {
        await createBrandVoice({ name, body, isDefault });
      }

      await fetchVoices();
      handleCancel();
    } catch (err: any) {
      setError(err.message || "Failed to save brand voice");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm("Are you sure you want to delete this brand voice style?")) return;
    try {
      await deleteBrandVoice(id);
      await fetchVoices();
    } catch (err: any) {
      setError(err.message || "Failed to delete brand voice");
    }
  };

  const handleToggleDefault = async (voice: BrandVoicePromptDto) => {
    if (voice.isDefault) return; // Already default
    try {
      setError(null);
      await updateBrandVoice(voice.id, {
        name: voice.name,
        body: voice.body,
        isDefault: true
      });
      await fetchVoices();
    } catch (err: any) {
      setError(err.message || "Failed to update default status");
    }
  };

  return (
    <div className="space-y-6 font-sans">
      {error && (
        <div className="p-3 bg-red-950/40 border border-red-800 text-red-400 text-xs rounded font-mono">
          {error}
        </div>
      )}

      {isAdding ? (
        <div className="bg-bg/40 border border-border p-4 rounded">
          <h3 className="text-xs font-mono uppercase tracking-wider text-muted mb-4">
            {editingId ? "Edit Brand Voice" : "New Brand Voice"}
          </h3>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                Name
              </label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                disabled={isSubmitting}
                placeholder="e.g. Friendly & Informal"
                className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50"
                required
              />
            </div>

            <div>
              <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                System Guidelines / Writing Style
              </label>
              <textarea
                value={body}
                onChange={(e) => setBody(e.target.value)}
                disabled={isSubmitting}
                rows={6}
                placeholder="e.g. Write in a relaxed, friendly manner. Use contractions. Use exclamation marks occasionally. Avoid corporate buzzwords."
                className="w-full bg-bg border border-border rounded px-3 py-1.5 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-mono"
                required
              />
            </div>

            <div className="flex items-center gap-2 py-1 select-none">
              <input
                type="checkbox"
                id="isDefault"
                checked={isDefault}
                onChange={(e) => setIsDefault(e.target.checked)}
                disabled={isSubmitting}
                className="rounded border-border text-accent focus:ring-0 focus:ring-offset-0 bg-bg cursor-pointer"
              />
              <label htmlFor="isDefault" className="text-xs font-mono uppercase tracking-wider text-muted cursor-pointer">
                Set as Default Voice
              </label>
            </div>

            <div className="flex items-center gap-2 pt-2">
              <button
                type="submit"
                disabled={isSubmitting || !name.trim() || !body.trim()}
                className="bg-accent text-bg font-medium text-xs py-1.5 px-4 rounded hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider"
              >
                {isSubmitting ? "Saving..." : "Save Brand Voice"}
              </button>
              <button
                type="button"
                onClick={handleCancel}
                disabled={isSubmitting}
                className="border border-border text-foreground hover:bg-border/20 text-xs py-1.5 px-4 rounded transition-colors font-mono uppercase tracking-wider"
              >
                Cancel
              </button>
            </div>
          </form>
        </div>
      ) : (
        <div className="space-y-4">
          <div className="flex justify-between items-center">
            <h3 className="text-xs font-mono uppercase tracking-wider text-muted">
              Brand Voice Library
            </h3>
            <button
              onClick={handleStartAdd}
              className="bg-accent text-bg font-medium text-xs py-1 px-3 rounded hover:opacity-90 transition-opacity font-mono uppercase tracking-wider"
            >
              + New Voice
            </button>
          </div>

          {loading ? (
            <div className="text-xs text-muted font-mono">Loading...</div>
          ) : voices.length === 0 ? (
            <div className="text-xs text-muted font-mono p-4 border border-border border-dashed rounded text-center">
              No brand voices created yet. Create one to steer the LLM response style!
            </div>
          ) : (
            <div className="space-y-2">
              {voices.map((voice) => (
                <div
                  key={voice.id}
                  className="flex flex-col p-4 border border-border bg-bg/20 rounded space-y-3"
                >
                  <div className="flex items-start justify-between">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="text-sm font-semibold text-foreground">{voice.name}</span>
                        {voice.isDefault && (
                          <span className="text-[9px] font-mono uppercase tracking-wider bg-accent/20 text-accent border border-accent/30 px-1.5 py-0.5 rounded">
                            Default
                          </span>
                        )}
                      </div>
                      <p className="text-xs text-muted font-mono mt-1 line-clamp-2 pr-6">
                        {voice.body}
                      </p>
                    </div>

                    <div className="flex items-center gap-2 shrink-0">
                      {!voice.isDefault && (
                        <button
                          onClick={() => handleToggleDefault(voice)}
                          className="text-[10px] font-mono uppercase tracking-wider text-muted hover:text-foreground px-2 py-1 border border-border rounded hover:bg-border/20 transition-colors"
                        >
                          Set Default
                        </button>
                      )}
                      <button
                        onClick={() => handleStartEdit(voice)}
                        className="text-[10px] font-mono uppercase tracking-wider text-muted hover:text-foreground px-2 py-1 border border-border rounded hover:bg-border/20 transition-colors"
                      >
                        Edit
                      </button>
                      <button
                        onClick={() => handleDelete(voice.id)}
                        className="text-[10px] font-mono uppercase tracking-wider text-red-400 hover:text-red-300 px-2 py-1 border border-red-900/30 bg-red-950/20 rounded transition-colors"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
