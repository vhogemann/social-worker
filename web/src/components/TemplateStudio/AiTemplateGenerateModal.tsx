import { useState } from "react";
import { generateTemplate, createTemplate, GeneratedTemplateResponse, TemplateDto } from "../../api/templates";

interface AiTemplateGenerateModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSaved: (template: TemplateDto) => void;
  onEditInStudio: (generated: GeneratedTemplateResponse) => void;
}

export function AiTemplateGenerateModal({
  isOpen,
  onClose,
  onSaved,
  onEditInStudio,
}: AiTemplateGenerateModalProps) {
  const [prompt, setPrompt] = useState("");
  const [aspectRatio, setAspectRatio] = useState<"square" | "portrait" | "landscape">("square");
  const [isGenerating, setIsGenerating] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<GeneratedTemplateResponse | null>(null);

  if (!isOpen) return null;

  const getDimensions = () => {
    switch (aspectRatio) {
      case "portrait":
        return { width: 1080, height: 1350 };
      case "landscape":
        return { width: 1200, height: 630 };
      case "square":
      default:
        return { width: 1080, height: 1080 };
    }
  };

  const handleGenerate = async () => {
    if (!prompt.trim()) {
      setError("Please enter a description for your template.");
      return;
    }

    setIsGenerating(true);
    setError(null);

    try {
      const { width, height } = getDimensions();
      const generated = await generateTemplate({
        prompt: prompt.trim(),
        width,
        height,
      });
      setResult(generated);
    } catch (err: any) {
      setError(err?.message || "Failed to generate template");
    } finally {
      setIsGenerating(false);
    }
  };

  const handleSaveToLibrary = async () => {
    if (!result) return;
    setIsSaving(true);
    setError(null);

    try {
      const saved = await createTemplate({
        name: result.name,
        description: result.description,
        width: result.width,
        height: result.height,
        schema: result.schema,
      });
      onSaved(saved);
      onClose();
    } catch (err: any) {
      setError(err?.message || "Failed to save template");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm p-4">
      <div className="bg-panel border border-border rounded-lg shadow-xl w-full max-w-2xl overflow-hidden flex flex-col max-h-[90vh] text-foreground">
        <div className="px-6 py-4 border-b border-border flex items-center justify-between bg-panel">
          <h2 className="text-base font-semibold text-foreground flex items-center gap-2">
            <span className="text-accent font-mono">✦</span> Generate Template with AI
          </h2>
          <button
            onClick={onClose}
            className="text-muted hover:text-foreground text-sm font-mono"
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        <div className="p-6 overflow-y-auto space-y-5 flex-1 bg-panel">
          {error && (
            <div className="p-3 bg-red-500/10 border border-red-500/20 text-red-400 text-xs rounded">
              {error}
            </div>
          )}

          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-2">
              Template Description & Style
            </label>
            <textarea
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              placeholder="e.g. Modern dark quote card with neon blue accents, elegant typography, author handle at the bottom, and high contrast."
              rows={3}
              className="w-full bg-bg border border-border rounded p-3 text-xs text-foreground placeholder:text-muted/60 focus:outline-none focus:border-accent resize-none"
              disabled={isGenerating}
            />
          </div>

          <div>
            <label className="block text-xs font-mono uppercase tracking-wider text-muted mb-2">
              Aspect Ratio
            </label>
            <div className="grid grid-cols-3 gap-2">
              <button
                type="button"
                onClick={() => setAspectRatio("square")}
                className={`px-3 py-2 border rounded text-xs font-mono transition-colors ${
                  aspectRatio === "square"
                    ? "border-accent bg-accent/20 text-accent font-semibold"
                    : "border-border text-muted hover:text-foreground hover:bg-bg"
                }`}
              >
                Square (1:1 · 1080x1080)
              </button>
              <button
                type="button"
                onClick={() => setAspectRatio("portrait")}
                className={`px-3 py-2 border rounded text-xs font-mono transition-colors ${
                  aspectRatio === "portrait"
                    ? "border-accent bg-accent/20 text-accent font-semibold"
                    : "border-border text-muted hover:text-foreground hover:bg-bg"
                }`}
              >
                Portrait (4:5 · 1080x1350)
              </button>
              <button
                type="button"
                onClick={() => setAspectRatio("landscape")}
                className={`px-3 py-2 border rounded text-xs font-mono transition-colors ${
                  aspectRatio === "landscape"
                    ? "border-accent bg-accent/20 text-accent font-semibold"
                    : "border-border text-muted hover:text-foreground hover:bg-bg"
                }`}
              >
                Banner (16:9 · 1200x630)
              </button>
            </div>
          </div>

          {result && (
            <div className="border border-border bg-panel-light rounded-lg p-4 space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="font-semibold text-sm text-foreground">{result.name}</h3>
                  {result.description && (
                    <p className="text-xs text-muted mt-0.5">{result.description}</p>
                  )}
                </div>
                <span className="text-xs font-mono px-2 py-0.5 rounded bg-bg text-muted border border-border">
                  {result.width}x{result.height}
                </span>
              </div>

              {result.previewBase64 && (
                <div className="flex justify-center bg-bg rounded p-2 border border-border">
                  <img
                    src={`data:image/png;base64,${result.previewBase64}`}
                    alt={result.name}
                    className="max-h-72 object-contain rounded shadow-sm"
                  />
                </div>
              )}
            </div>
          )}
        </div>

        <div className="px-6 py-4 border-t border-border bg-panel flex items-center justify-between">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 text-xs font-mono text-muted hover:text-foreground"
          >
            Cancel
          </button>

          <div className="flex gap-2">
            {result ? (
              <>
                <button
                  type="button"
                  onClick={() => onEditInStudio(result)}
                  className="px-4 py-2 border border-border hover:bg-bg rounded text-xs font-mono text-foreground"
                >
                  Edit in Studio
                </button>
                <button
                  type="button"
                  onClick={handleSaveToLibrary}
                  disabled={isSaving}
                  className="px-4 py-2 bg-accent text-accent-foreground font-medium rounded text-xs font-mono hover:opacity-90 disabled:opacity-50"
                >
                  {isSaving ? "Saving..." : "Save to Library"}
                </button>
              </>
            ) : (
              <button
                type="button"
                onClick={handleGenerate}
                disabled={isGenerating || !prompt.trim()}
                className="px-4 py-2 bg-accent text-accent-foreground font-medium rounded text-xs font-mono hover:opacity-90 disabled:opacity-50 flex items-center gap-2"
              >
                {isGenerating ? (
                  <>
                    <span className="animate-spin">⟳</span> Generating & Verifying...
                  </>
                ) : (
                  "Generate Template"
                )}
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
