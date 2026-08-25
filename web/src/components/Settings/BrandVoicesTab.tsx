import { useState, useEffect } from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import {
  faPlus,
  faBullhorn,
  faPen,
  faTrashCan,
  faStar,
  faCheck,
} from "@fortawesome/free-solid-svg-icons";
import {
  type BrandVoicePromptDto,
  listBrandVoices,
  createBrandVoice,
  updateBrandVoice,
  deleteBrandVoice,
} from "../../api/brandVoices";
import { Modal } from "../common/Modal";
import { SettingsSection } from "./common/SettingsSection";
import { SettingsEmptyState } from "./common/SettingsEmptyState";
import { SettingsAlert } from "./common/SettingsAlert";

export function BrandVoicesTab() {
  const [voices, setVoices] = useState<BrandVoicePromptDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [modalError, setModalError] = useState<string | null>(null);

  // Modal & Form State
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [body, setBody] = useState("");
  const [isDefault, setIsDefault] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

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
    setModalError(null);
    setIsModalOpen(true);
  };

  const handleStartEdit = (voice: BrandVoicePromptDto) => {
    setEditingId(voice.id);
    setName(voice.name);
    setBody(voice.body);
    setIsDefault(voice.isDefault);
    setModalError(null);
    setIsModalOpen(true);
  };

  const handleCloseModal = () => {
    if (isSubmitting) return;
    setIsModalOpen(false);
    setEditingId(null);
    setName("");
    setBody("");
    setIsDefault(false);
    setModalError(null);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !body.trim()) return;

    try {
      setIsSubmitting(true);
      setModalError(null);

      if (editingId) {
        await updateBrandVoice(editingId, { name: name.trim(), body: body.trim(), isDefault });
      } else {
        await createBrandVoice({ name: name.trim(), body: body.trim(), isDefault });
      }

      await fetchVoices();
      handleCloseModal();
    } catch (err: any) {
      setModalError(err.message || "Failed to save brand voice");
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
    if (voice.isDefault) return;
    try {
      setError(null);
      await updateBrandVoice(voice.id, {
        name: voice.name,
        body: voice.body,
        isDefault: true,
      });
      await fetchVoices();
    } catch (err: any) {
      setError(err.message || "Failed to update default status");
    }
  };

  return (
    <div className="space-y-6 font-sans">
      {error && !isModalOpen && (
        <SettingsAlert type="error" message={error} onDismiss={() => setError(null)} />
      )}

      <SettingsSection
        title="Brand Voice Library"
        description="Configure reusable style guidelines and tone prompts for drafting content."
        action={
          <button
            type="button"
            onClick={handleStartAdd}
            className="inline-flex items-center gap-1.5 bg-accent text-bg font-medium text-xs py-1.5 px-3.5 rounded hover:opacity-90 transition-opacity font-mono uppercase tracking-wider shadow-sm"
          >
            <FontAwesomeIcon icon={faPlus} className="w-3 h-3" />
            <span>New Voice</span>
          </button>
        }
      >
        {loading && voices.length === 0 ? (
          <div className="text-xs text-muted font-mono py-6 text-center">Loading brand voices...</div>
        ) : voices.length === 0 ? (
          <SettingsEmptyState
            icon={faBullhorn}
            title="No brand voices created yet"
            description="Create a brand voice to guide the LLM's writing tone, terminology, and stylistic constraints."
            actionLabel="+ New Voice"
            onAction={handleStartAdd}
          />
        ) : (
          <div className="space-y-2.5">
            {voices.map((voice) => (
              <div
                key={voice.id}
                className="p-4 border border-border bg-bg/20 rounded-lg hover:border-border/80 transition-colors flex flex-col md:flex-row md:items-start justify-between gap-4"
              >
                <div className="space-y-1.5 min-w-0 flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-sm font-semibold text-foreground">{voice.name}</span>
                    {voice.isDefault && (
                      <span className="text-[10px] font-mono uppercase tracking-wider bg-accent/15 text-accent border border-accent/30 px-1.5 py-0.5 rounded inline-flex items-center gap-1">
                        <FontAwesomeIcon icon={faStar} className="w-2.5 h-2.5" />
                        <span>Default</span>
                      </span>
                    )}
                  </div>
                  <p className="text-xs text-muted font-mono leading-relaxed line-clamp-3">
                    {voice.body}
                  </p>
                </div>

                <div className="flex items-center gap-1.5 shrink-0 flex-wrap">
                  {!voice.isDefault && (
                    <button
                      onClick={() => handleToggleDefault(voice)}
                      title="Set as default brand voice"
                      className="px-2.5 py-1 text-[11px] font-mono border border-border text-muted hover:text-foreground hover:bg-border/20 rounded uppercase tracking-wider transition-colors inline-flex items-center gap-1"
                    >
                      <FontAwesomeIcon icon={faCheck} className="w-2.5 h-2.5" />
                      <span>Set Default</span>
                    </button>
                  )}
                  <button
                    onClick={() => handleStartEdit(voice)}
                    className="px-2.5 py-1 text-[11px] font-mono border border-border text-muted hover:text-foreground hover:bg-border/20 rounded uppercase tracking-wider transition-colors inline-flex items-center gap-1"
                  >
                    <FontAwesomeIcon icon={faPen} className="w-2.5 h-2.5" />
                    <span>Edit</span>
                  </button>
                  <button
                    onClick={() => handleDelete(voice.id)}
                    className="px-2.5 py-1 text-[11px] font-mono border border-red-900/30 text-red-400 hover:text-red-300 hover:bg-red-950/20 rounded uppercase tracking-wider transition-colors inline-flex items-center gap-1"
                  >
                    <FontAwesomeIcon icon={faTrashCan} className="w-2.5 h-2.5" />
                    <span>Delete</span>
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </SettingsSection>

      {/* Brand Voice Modal */}
      <Modal
        isOpen={isModalOpen}
        onClose={handleCloseModal}
        title={editingId ? "Edit Brand Voice" : "New Brand Voice"}
        description="Configure system guidelines and instructions for how the AI shapes drafts."
        maxWidth="lg"
      >
        {modalError && (
          <SettingsAlert type="error" message={modalError} onDismiss={() => setModalError(null)} />
        )}

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
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-sans"
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
              className="w-full bg-bg border border-border rounded-lg px-3 py-2 text-sm text-foreground focus:outline-none focus:border-accent disabled:opacity-50 font-mono leading-relaxed"
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

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-border">
            <button
              type="button"
              onClick={handleCloseModal}
              disabled={isSubmitting}
              className="border border-border text-foreground hover:bg-border/20 text-xs py-2 px-4 rounded-lg transition-colors font-mono uppercase tracking-wider"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting || !name.trim() || !body.trim()}
              className="bg-accent text-bg font-medium text-xs py-2 px-4 rounded-lg hover:opacity-90 transition-opacity disabled:opacity-50 font-mono uppercase tracking-wider shadow-sm"
            >
              {isSubmitting ? "Saving..." : "Save Brand Voice"}
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
