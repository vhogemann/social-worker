import { useState } from "react";
import { useDraftStore } from "../../store/draftStore";
import { generateVariants } from "../../api/drafts";

const PLATFORMS = ["Bluesky", "Twitter", "LinkedIn", "Facebook", "Instagram"];

interface Props {
  isOpen: boolean;
  onClose: () => void;
  draftId: string | null;
}

export function AdaptVariantsModal({ isOpen, onClose, draftId }: Props) {
  const [selected, setSelected] = useState<string[]>(["Twitter", "LinkedIn"]);
  const [generating, setGenerating] = useState(false);
  const [result, setResult] = useState<string | null>(null);

  const activeDraft = useDraftStore((s) => s.drafts.find((d) => d.id === draftId));

  if (!isOpen) return null;

  const sourcePlatform = activeDraft?.targetPlatform || "Bluesky";
  const availablePlatforms = PLATFORMS.filter((p) => p !== sourcePlatform);

  const toggle = (platform: string) => {
    setSelected((s) =>
      s.includes(platform) ? s.filter((p) => p !== platform) : [...s, platform]
    );
  };

  const handleGenerate = async () => {
    if (!draftId || selected.length === 0) return;
    setGenerating(true);
    setResult(null);
    try {
      const data = await generateVariants(draftId, selected);
      const variants = data.variants || [];
      if (variants.length > 0) {
        setResult(`Created ${variants.length} variant(s): ${variants.map((v: any) => `${v.targetPlatform || v.platform}`).join(", ")}`);
        useDraftStore.getState().loadDrafts();
      } else {
        setResult("No variants were created (they may already exist).");
      }
    } catch (err: any) {
      setResult(`Error: ${err.message}`);
    } finally {
      setGenerating(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40" data-testid="adapt-variants-modal-overlay">
      <div className="bg-panel border border-border rounded-lg shadow-xl w-96 max-w-full mx-3" data-testid="adapt-variants-modal">
        <div className="px-4 py-3 border-b border-border">
          <h2 className="text-sm font-semibold text-foreground">Adapt to Other Platforms</h2>
          <p className="text-xs text-muted mt-0.5">
            From {sourcePlatform} to:
          </p>
        </div>
        <div className="px-4 py-3 space-y-1.5">
          {availablePlatforms.map((p) => (
            <label
              key={p}
              className="flex items-center gap-2.5 px-2.5 py-2 rounded border border-border cursor-pointer hover:bg-border/30 transition-colors"
            >
              <input
                type="checkbox"
                checked={selected.includes(p)}
                onChange={() => toggle(p)}
                className="accent-accent"
              />
              <span className="text-sm text-foreground">{p}</span>
            </label>
          ))}
          {result && (
            <div className="mt-2 text-xs text-muted bg-border/30 rounded px-2.5 py-2">
              {result}
            </div>
          )}
        </div>
        <div className="px-4 py-3 border-t border-border flex items-center justify-end gap-2">
          <button
            onClick={onClose}
            className="text-xs font-mono text-muted hover:text-foreground px-3 py-1.5 rounded border border-border hover:border-accent/50 transition-colors"
          >
            Close
          </button>
          <button
            onClick={handleGenerate}
            disabled={generating || selected.length === 0}
            className="text-xs font-mono text-white bg-accent hover:opacity-90 px-3 py-1.5 rounded transition-opacity disabled:opacity-50"
          >
            {generating ? "Generating..." : "Generate Variants"}
          </button>
        </div>
      </div>
    </div>
  );
}