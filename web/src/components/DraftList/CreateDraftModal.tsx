import { useState } from "react";

const PLATFORMS = [
  { id: "Bluesky", label: "Bluesky", description: "Multi-post thread (300 chars/post)" },
  { id: "Twitter", label: "Twitter", description: "Multi-post thread (280 chars/post)" },
  { id: "LinkedIn", label: "LinkedIn", description: "Single long-form post (~3000 chars)" },
  { id: "Facebook", label: "Facebook", description: "Conversational, no hard limit" },
  { id: "Instagram", label: "Instagram", description: "Visual-first, 2200 char caption" },
];

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onCreate: (title?: string, targetPlatform?: string) => Promise<void>;
}

export function CreateDraftModal({ isOpen, onClose, onCreate }: Props) {
  const [title, setTitle] = useState("");
  const [platform, setPlatform] = useState("Bluesky");
  const [creating, setCreating] = useState(false);

  if (!isOpen) return null;

  const handleCreate = async () => {
    setCreating(true);
    try {
      await onCreate(title || undefined, platform);
      setTitle("");
      setPlatform("Bluesky");
      onClose();
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" data-testid="create-draft-modal-overlay">
      <div className="bg-panel border border-border rounded-lg shadow-xl w-96 max-w-full mx-3" data-testid="create-draft-modal">
        <div className="px-4 py-3 border-b border-border">
          <h2 className="text-sm font-semibold text-foreground">Create New Draft</h2>
        </div>
        <div className="px-4 py-3 space-y-3">
          <div>
            <label className="block text-xs font-mono text-muted mb-1">Title</label>
            <input
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Enter") handleCreate(); }}
              placeholder="Untitled"
              className="w-full bg-bg border border-border rounded px-2 py-1.5 text-sm text-foreground placeholder:text-muted/50 focus:outline-none focus:border-accent"
              autoFocus
            />
          </div>
          <div>
            <label className="block text-xs font-mono text-muted mb-1.5">Target Platform</label>
            <div className="space-y-1">
              {PLATFORMS.map((p) => (
                <button
                  key={p.id}
                  onClick={() => setPlatform(p.id)}
                  className={`w-full text-left px-2.5 py-1.5 rounded border text-sm transition-colors ${
                    platform === p.id
                      ? "border-accent bg-accent/10 text-foreground"
                      : "border-border text-muted hover:border-accent/50 hover:text-foreground"
                  }`}
                >
                  <span className="font-medium">{p.label}</span>
                  <span className="text-xs text-muted ml-2">— {p.description}</span>
                </button>
              ))}
            </div>
          </div>
        </div>
        <div className="px-4 py-3 border-t border-border flex items-center justify-end gap-2">
          <button
            onClick={onClose}
            className="text-xs font-mono text-muted hover:text-foreground px-3 py-1.5 rounded border border-border hover:border-accent/50 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleCreate}
            disabled={creating}
            className="text-xs font-mono text-white bg-accent hover:opacity-90 px-3 py-1.5 rounded transition-opacity disabled:opacity-50"
          >
            {creating ? "Creating..." : "Create Draft"}
          </button>
        </div>
      </div>
    </div>
  );
}