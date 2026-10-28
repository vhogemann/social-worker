import React, { useState, useEffect } from "react";
import { useDraftStore } from "../../store/draftStore";
import { fetchTemplates, type TemplateDto } from "../../api/templates";

interface GenerateImageModalProps {
  isOpen: boolean;
  onClose: () => void;
  draftId: string;
  onInserted?: (markdownTag: string) => void;
}

interface TemplateSlotInfo {
  name: string;
  type: string;
  description?: string;
  defaultValue?: string;
}

const ASPECT_RATIOS = [
  { id: "1:1", label: "Square (1:1)", desc: "1080x1080 • Posts & Threads" },
  { id: "16:9", label: "Landscape (16:9)", desc: "1920x1080 • Twitter / Web" },
  { id: "4:5", label: "Portrait (4:5)", desc: "1080x1350 • Instagram" },
  { id: "9:16", label: "Vertical (9:16)", desc: "1080x1920 • Stories & Reels" },
];

const POPULAR_MODELS = [
  { id: "black-forest-labs/flux-3-image-klein", label: "FLUX 3 Image Klein (Default • Fast)" },
  { id: "google/gemini-2.5-flash-image", label: "Gemini 2.5 Flash Image (Google • High Quality)" },
  { id: "bytedance-seed/seedream-5-0-flash", label: "Seedream 5.0 Flash (ByteDance • Artistic)" },
  { id: "black-forest-labs/flux.2-pro", label: "FLUX.2 Pro (Frontier Studio Quality)" },
  { id: "custom", label: "Custom Model..." },
];

export const GenerateImageModal: React.FC<GenerateImageModalProps> = ({
  isOpen,
  onClose,
  draftId,
  onInserted,
}) => {
  const generateMediaImage = useDraftStore((s) => s.generateMediaImage);

  const [prompt, setPrompt] = useState("");
  const [aspectRatio, setAspectRatio] = useState("1:1");
  const [selectedModel, setSelectedModel] = useState("black-forest-labs/flux-3-image-klein");
  const [customModel, setCustomModel] = useState("");
  const [altText, setAltText] = useState("");

  const [templates, setTemplates] = useState<TemplateDto[]>([]);
  const [selectedTemplateId, setSelectedTemplateId] = useState<string>("");
  const [templateSlots, setTemplateSlots] = useState<Record<string, string>>({});
  const [slotDefinitions, setSlotDefinitions] = useState<TemplateSlotInfo[]>([]);

  const [generating, setGenerating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [generatedResult, setGeneratedResult] = useState<{
    mediaAssetId: string;
    markdownTag: string;
    url: string;
  } | null>(null);

  useEffect(() => {
    if (isOpen) {
      void fetchTemplates()
        .then((items) => setTemplates(items))
        .catch(() => setTemplates([]));
    } else {
      setError(null);
      setGeneratedResult(null);
    }
  }, [isOpen]);

  useEffect(() => {
    if (!selectedTemplateId) {
      setSlotDefinitions([]);
      setTemplateSlots({});
      return;
    }

    const tpl = templates.find((t) => t.id === selectedTemplateId);
    if (!tpl) return;

    try {
      const parsed = JSON.parse(tpl.schema);
      if (Array.isArray(parsed.slots)) {
        const textSlots: TemplateSlotInfo[] = parsed.slots.filter(
          (s: any) => s && s.type !== "image"
        );
        setSlotDefinitions(textSlots);

        const initialValues: Record<string, string> = {};
        for (const s of textSlots) {
          initialValues[s.name] = s.defaultValue || "";
        }
        setTemplateSlots(initialValues);
      }
    } catch {
      setSlotDefinitions([]);
      setTemplateSlots({});
    }
  }, [selectedTemplateId, templates]);

  if (!isOpen) return null;

  const handleGenerate = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!prompt.trim() || generating) return;

    setGenerating(true);
    setError(null);
    setGeneratedResult(null);

    const resolvedModel = selectedModel === "custom" ? customModel.trim() : selectedModel;

    try {
      const res = await generateMediaImage({
        draftId,
        prompt: prompt.trim(),
        aspectRatio,
        model: resolvedModel || undefined,
        templateId: selectedTemplateId || undefined,
        templateSlots: selectedTemplateId ? templateSlots : undefined,
        altText: altText.trim() || undefined,
      });

      setGeneratedResult(res);
    } catch (err: any) {
      setError(err?.message || "Failed to generate image.");
    } finally {
      setGenerating(false);
    }
  };

  const handleInsert = () => {
    if (generatedResult?.markdownTag) {
      if (onInserted) {
        onInserted(generatedResult.markdownTag);
      } else {
        window.dispatchEvent(
          new CustomEvent("editor-insert", { detail: generatedResult.markdownTag })
        );
      }
      onClose();
    }
  };

  return (
    <div
      className="fixed inset-0 bg-bg/80 backdrop-blur-sm z-50 flex items-center justify-center p-4 animate-fade-in"
      data-testid="generate-image-modal-overlay"
    >
      <div
        className="bg-panel border border-border w-full max-w-2xl max-h-[90vh] rounded-2xl shadow-2xl overflow-hidden flex flex-col font-sans"
        data-testid="generate-image-modal"
      >
        <div className="px-6 py-4 border-b border-border/60 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              strokeWidth={1.75}
              stroke="currentColor"
              className="w-5 h-5 text-accent"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M9.813 15.904 9 18.75l-.813-2.846a4.5 4.5 0 0 0-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 0 0 3.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 0 0 3.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 0 0-3.09 3.09ZM18.259 8.715 18 9.75l-.259-1.035a3.375 3.375 0 0 0-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 0 0 2.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 0 0 2.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 0 0-2.456 2.456ZM16.894 20.567 16.5 21.75l-.394-1.183a2.25 2.25 0 0 0-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 0 0 1.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 0 0 1.423 1.423l1.183.394-1.183.394a2.25 2.25 0 0 0-1.423 1.423Z"
              />
            </svg>
            <h3 className="text-sm font-semibold text-foreground">
              Generate Visual with AI
            </h3>
          </div>
          <button
            onClick={onClose}
            className="text-muted hover:text-foreground text-sm p-1 rounded-lg transition"
            aria-label="Close modal"
          >
            ✕
          </button>
        </div>

        <form onSubmit={handleGenerate} className="flex-1 overflow-y-auto p-6 space-y-4">
          {error && (
            <div className="bg-red-500/10 border border-red-500/20 text-red-400 text-xs px-3 py-2 rounded-xl">
              {error}
            </div>
          )}

          <div>
            <label className="block text-xs font-semibold text-foreground mb-1.5">
              Image Prompt <span className="text-accent">*</span>
            </label>
            <textarea
              value={prompt}
              onChange={(e) => setPrompt(e.target.value)}
              placeholder="E.g. A futuristic glass workstation overlooking a cyberpunk metropolis, volumetric lighting, photorealistic 8k..."
              rows={3}
              required
              disabled={generating}
              className="w-full bg-bg border border-border rounded-xl px-3 py-2 text-xs text-foreground placeholder:text-muted/60 focus:outline-none focus:border-accent transition resize-none"
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-foreground mb-1.5">
                Aspect Ratio
              </label>
              <select
                value={aspectRatio}
                onChange={(e) => setAspectRatio(e.target.value)}
                disabled={generating}
                className="w-full bg-bg border border-border rounded-xl px-3 py-2 text-xs text-foreground focus:outline-none focus:border-accent transition"
              >
                {ASPECT_RATIOS.map((ar) => (
                  <option key={ar.id} value={ar.id}>
                    {ar.label}
                  </option>
                ))}
              </select>
            </div>

            <div>
              <label className="block text-xs font-semibold text-foreground mb-1.5">
                Model (OpenRouter)
              </label>
              <select
                value={selectedModel}
                onChange={(e) => setSelectedModel(e.target.value)}
                disabled={generating}
                className="w-full bg-bg border border-border rounded-xl px-3 py-2 text-xs text-foreground focus:outline-none focus:border-accent transition"
              >
                {POPULAR_MODELS.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.label}
                  </option>
                ))}
              </select>
              {selectedModel === "custom" && (
                <input
                  type="text"
                  value={customModel}
                  onChange={(e) => setCustomModel(e.target.value)}
                  placeholder="e.g. google/gemini-2.5-flash-image"
                  disabled={generating}
                  className="w-full mt-2 bg-bg border border-border rounded-xl px-3 py-2 text-xs text-foreground focus:outline-none focus:border-accent transition"
                />
              )}
            </div>
          </div>

          <div>
            <label className="block text-xs font-semibold text-foreground mb-1.5">
              Hybrid Template Overlay (Optional)
            </label>
            <select
              value={selectedTemplateId}
              onChange={(e) => setSelectedTemplateId(e.target.value)}
              disabled={generating}
              className="w-full bg-bg border border-border rounded-xl px-3 py-2 text-xs text-foreground focus:outline-none focus:border-accent transition"
            >
              <option value="">None (Pure AI Visual Art)</option>
              {templates.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name} ({t.width}x{t.height})
                </option>
              ))}
            </select>
            <p className="text-[11px] text-muted mt-1">
              Composites the generated image into an image-engine template with vector layout and crisp typography.
            </p>
          </div>

          {slotDefinitions.length > 0 && (
            <div className="bg-bg/40 border border-border/80 rounded-xl p-3 space-y-2.5">
              <span className="text-[11px] font-mono uppercase text-muted tracking-wider block">
                Template Slots
              </span>
              {slotDefinitions.map((slot) => (
                <div key={slot.name}>
                  <label className="block text-[11px] text-foreground font-mono mb-1">
                    {slot.name}
                  </label>
                  <input
                    type="text"
                    value={templateSlots[slot.name] || ""}
                    onChange={(e) =>
                      setTemplateSlots((prev) => ({ ...prev, [slot.name]: e.target.value }))
                    }
                    placeholder={slot.description || slot.name}
                    disabled={generating}
                    className="w-full bg-bg border border-border rounded-lg px-2.5 py-1.5 text-xs text-foreground focus:outline-none focus:border-accent transition"
                  />
                </div>
              ))}
            </div>
          )}

          <div>
            <label className="block text-xs font-semibold text-foreground mb-1.5">
              Alt Text (Accessibility)
            </label>
            <input
              type="text"
              value={altText}
              onChange={(e) => setAltText(e.target.value)}
              placeholder="Descriptive text for screen readers..."
              disabled={generating}
              className="w-full bg-bg border border-border rounded-xl px-3 py-2 text-xs text-foreground placeholder:text-muted/60 focus:outline-none focus:border-accent transition"
            />
          </div>

          {generatedResult && (
            <div className="pt-2 border-t border-border/60">
              <div className="text-xs font-semibold text-foreground mb-2 flex items-center justify-between">
                <span>Generated Output</span>
                <span className="text-[10px] font-mono text-muted">
                  media://{generatedResult.mediaAssetId}
                </span>
              </div>
              <div className="rounded-xl overflow-hidden border border-border bg-bg/60 flex items-center justify-center p-2">
                <img
                  src={generatedResult.url}
                  alt={altText || prompt}
                  className="max-h-60 max-w-full rounded-lg object-contain shadow-md"
                />
              </div>
            </div>
          )}

          <div className="pt-4 border-t border-border/60 flex items-center justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              disabled={generating}
              className="px-4 py-2 bg-border/40 hover:bg-border border border-border rounded-xl text-xs font-semibold text-muted hover:text-foreground transition"
            >
              Cancel
            </button>

            {generatedResult ? (
              <button
                type="button"
                onClick={handleInsert}
                className="px-4 py-2 bg-accent hover:bg-accent/90 text-bg rounded-xl text-xs font-semibold transition flex items-center gap-1.5"
              >
                <span>Insert into Draft</span>
              </button>
            ) : (
              <button
                type="submit"
                disabled={generating || !prompt.trim()}
                className="px-4 py-2 bg-accent hover:bg-accent/90 text-bg rounded-xl text-xs font-semibold transition flex items-center gap-1.5 disabled:opacity-50"
              >
                {generating ? (
                  <>
                    <div className="w-3.5 h-3.5 border-2 border-bg/30 border-t-bg rounded-full animate-spin shrink-0" />
                    <span>Generating...</span>
                  </>
                ) : (
                  <span>Generate</span>
                )}
              </button>
            )}
          </div>
        </form>
      </div>
    </div>
  );
};
