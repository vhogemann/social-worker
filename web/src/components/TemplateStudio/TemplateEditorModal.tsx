import { useState, useEffect, useId } from "react";
import { createTemplate, updateTemplate, previewTemplate, TemplateDto, GeneratedTemplateResponse } from "../../api/templates";

interface TemplateEditorModalProps {
  isOpen: boolean;
  template: (TemplateDto | GeneratedTemplateResponse) | null;
  onClose: () => void;
  onSaved: (saved: TemplateDto) => void;
}

export function TemplateEditorModal({
  isOpen,
  template,
  onClose,
  onSaved,
}: TemplateEditorModalProps) {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [width, setWidth] = useState(1080);
  const [height, setHeight] = useState(1080);
  const [schemaJson, setSchemaJson] = useState("{\n  \"name\": \"Custom Template\",\n  \"width\": 1080,\n  \"height\": 1080,\n  \"slots\": [],\n  \"layers\": []\n}");
  const [slotInputs, setSlotInputs] = useState<Record<string, string>>({});
  const [previewBlobUrl, setPreviewBlobUrl] = useState<string | null>(null);
  const [isRendering, setIsRendering] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [renderError, setRenderError] = useState<string | null>(null);

  const nameInputId = useId();
  const descInputId = useId();
  const widthInputId = useId();
  const heightInputId = useId();
  const schemaTextareaId = useId();

  // Initialize state when template changes or modal opens
  useEffect(() => {
    if (!isOpen) return;

    if (template) {
      setName(template.name ?? "");
      setDescription(template.description ?? "");
      setWidth(template.width || 1080);
      setHeight(template.height || 1080);

      try {
        const parsed = typeof template.schema === "string" ? JSON.parse(template.schema) : template.schema;
        setSchemaJson(JSON.stringify(parsed, null, 2));
      } catch {
        setSchemaJson(template.schema ?? "");
      }
    } else {
      setName("");
      setDescription("");
      setWidth(1080);
      setHeight(1080);
      const defaultSchema = {
        name: "New Template",
        width: 1080,
        height: 1080,
        slots: [
          { name: "title", type: "text", required: true, defaultValue: "Hello World" }
        ],
        layers: [
          { type: "rect", fill: "#0f172a", x: 0, y: 0, w: "100%", h: "100%" },
          { type: "text", slot: "title", x: 100, y: 200, fontSize: 48, fontWeight: "bold", color: "#f8fafc" }
        ]
      };
      setSchemaJson(JSON.stringify(defaultSchema, null, 2));
    }
  }, [isOpen, template]);

  // Extract slots dynamically from schema to populate interactive test inputs
  useEffect(() => {
    try {
      const parsed = JSON.parse(schemaJson);
      if (Array.isArray(parsed.slots)) {
        const nextSlots: Record<string, string> = { ...slotInputs };
        for (const slot of parsed.slots) {
          if (slot.name && nextSlots[slot.name] === undefined) {
            nextSlots[slot.name] = slot.defaultValue ?? (slot.name.includes("quote") ? "The best way to predict the future is to create it." : `Sample ${slot.name}`);
          }
        }
        setSlotInputs(nextSlots);
      }
    } catch {
      // JSON is currently being edited
    }
  }, [schemaJson]);

  // Render preview
  const handleRenderPreview = async () => {
    try {
      setIsRendering(true);
      setRenderError(null);

      // Validate JSON syntax first
      JSON.parse(schemaJson);

      const blob = await previewTemplate({
        schema: schemaJson,
        slots: slotInputs,
      });

      if (previewBlobUrl) {
        URL.revokeObjectURL(previewBlobUrl);
      }
      setPreviewBlobUrl(URL.createObjectURL(blob));
    } catch (err: any) {
      setRenderError(err?.message || "Failed to render preview");
    } finally {
      setIsRendering(false);
    }
  };

  // Initial preview render on mount/open
  useEffect(() => {
    if (isOpen) {
      const timer = setTimeout(() => {
        handleRenderPreview();
      }, 300);
      return () => clearTimeout(timer);
    }
  }, [isOpen]);

  const handleFormatJson = () => {
    try {
      const parsed = JSON.parse(schemaJson);
      setSchemaJson(JSON.stringify(parsed, null, 2));
      setRenderError(null);
    } catch (err: any) {
      setRenderError(`Invalid JSON format: ${err.message}`);
    }
  };

  const handleSave = async () => {
    if (!name.trim()) {
      setError("Template name is required.");
      return;
    }

    try {
      setIsSaving(true);
      setError(null);

      // Ensure valid JSON
      const parsed = JSON.parse(schemaJson);
      parsed.name = name.trim();
      parsed.width = width;
      parsed.height = height;
      const normalizedSchema = JSON.stringify(parsed, null, 2);

      let saved: TemplateDto;
      const existingId = (template as TemplateDto)?.id;

      if (existingId) {
        saved = await updateTemplate(existingId, {
          name: name.trim(),
          description: description.trim() || null,
          width,
          height,
          schema: normalizedSchema,
        });
      } else {
        saved = await createTemplate({
          name: name.trim(),
          description: description.trim() || null,
          width,
          height,
          schema: normalizedSchema,
        });
      }

      onSaved(saved);
      onClose();
    } catch (err: any) {
      setError(err?.message || "Failed to save template");
    } finally {
      setIsSaving(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
      <div className="bg-panel border border-border rounded-lg shadow-2xl w-full max-w-6xl h-[92vh] flex flex-col overflow-hidden text-foreground">
        {/* Header */}
        <div className="px-6 py-3 border-b border-border flex items-center justify-between shrink-0 bg-panel">
          <div className="flex items-center gap-3">
            <h2 className="text-base font-semibold text-foreground">
              {(template as TemplateDto)?.id ? "Edit Template" : "Template Studio Editor"}
            </h2>
            <span className="text-xs font-mono px-2 py-0.5 rounded bg-bg text-muted border border-border">
              {width}x{height}
            </span>
          </div>
          <button
            onClick={onClose}
            className="text-muted hover:text-foreground text-sm font-mono"
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        {/* Content Body: Split View */}
        <div className="flex-1 min-h-0 flex overflow-hidden">
          {/* Left Column: Schema & Metadata */}
          <div className="w-1/2 border-r border-border p-5 flex flex-col overflow-y-auto space-y-4 bg-panel">
            {error && (
              <div className="p-3 bg-red-500/10 border border-red-500/20 text-red-400 text-xs rounded shrink-0">
                {error}
              </div>
            )}

            <div className="grid grid-cols-2 gap-3 shrink-0">
              <div>
                <label htmlFor={nameInputId} className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                  Template Name *
                </label>
                <input
                  id={nameInputId}
                  type="text"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="e.g. Modern Quote Card"
                  className="w-full bg-bg border border-border rounded px-3 py-1.5 text-xs text-foreground focus:outline-none focus:border-accent"
                />
              </div>

              <div>
                <label htmlFor={descInputId} className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                  Description
                </label>
                <input
                  id={descInputId}
                  type="text"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="e.g. High contrast square quote"
                  className="w-full bg-bg border border-border rounded px-3 py-1.5 text-xs text-foreground focus:outline-none focus:border-accent"
                />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3 shrink-0">
              <div>
                <label htmlFor={widthInputId} className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                  Width (px)
                </label>
                <input
                  id={widthInputId}
                  type="number"
                  value={width}
                  onChange={(e) => setWidth(parseInt(e.target.value) || 1080)}
                  className="w-full bg-bg border border-border rounded px-3 py-1.5 text-xs text-foreground font-mono focus:outline-none focus:border-accent"
                />
              </div>

              <div>
                <label htmlFor={heightInputId} className="block text-xs font-mono uppercase tracking-wider text-muted mb-1">
                  Height (px)
                </label>
                <input
                  id={heightInputId}
                  type="number"
                  value={height}
                  onChange={(e) => setHeight(parseInt(e.target.value) || 1080)}
                  className="w-full bg-bg border border-border rounded px-3 py-1.5 text-xs text-foreground font-mono focus:outline-none focus:border-accent"
                />
              </div>
            </div>

            {/* JSON Layer Tree Editor */}
            <div className="flex-1 min-h-[300px] flex flex-col">
              <div className="flex items-center justify-between mb-1 shrink-0">
                <label htmlFor={schemaTextareaId} className="text-xs font-mono uppercase tracking-wider text-muted">
                  Declarative Schema JSON (Layers & Slots)
                </label>
                <button
                  type="button"
                  onClick={handleFormatJson}
                  className="text-xs font-mono text-accent hover:underline"
                >
                  Format JSON
                </button>
              </div>
              <textarea
                id={schemaTextareaId}
                value={schemaJson}
                onChange={(e) => setSchemaJson(e.target.value)}
                rows={16}
                className="flex-1 w-full bg-bg border border-border rounded p-3 font-mono text-xs text-foreground focus:outline-none focus:border-accent resize-none leading-relaxed"
                spellCheck={false}
              />
            </div>
          </div>

          {/* Right Column: Live Render Canvas & Slot Test Values */}
          <div className="w-1/2 p-5 flex flex-col overflow-y-auto bg-panel-light space-y-4">
            <div className="flex items-center justify-between shrink-0">
              <span className="text-xs font-mono uppercase tracking-wider text-muted">
                Live Test Render
              </span>
              <button
                type="button"
                onClick={handleRenderPreview}
                disabled={isRendering}
                className="px-3 py-1 bg-panel hover:bg-bg border border-border text-foreground text-xs font-mono rounded flex items-center gap-1.5 disabled:opacity-50"
              >
                {isRendering ? <span className="animate-spin">⟳</span> : "⟳"} Refresh Preview
              </button>
            </div>

            {renderError && (
              <div className="p-3 bg-red-500/10 border border-red-500/20 text-red-400 text-xs font-mono rounded shrink-0">
                {renderError}
              </div>
            )}

            {/* Preview Viewport */}
            <div className="flex-1 flex items-center justify-center bg-bg border border-border rounded-lg p-3 min-h-[320px] overflow-hidden">
              {previewBlobUrl ? (
                <img
                  src={previewBlobUrl}
                  alt="Template live preview"
                  className="max-h-[380px] max-w-full object-contain rounded shadow-lg"
                />
              ) : (
                <div className="text-xs font-mono text-muted text-center">
                  {isRendering ? "Rendering canvas..." : "Click Refresh Preview to test render"}
                </div>
              )}
            </div>

            {/* Interactive Slot Value Test Inputs */}
            {Object.keys(slotInputs).length > 0 && (
              <div className="border border-border bg-panel rounded-lg p-3 space-y-2 shrink-0">
                <span className="text-xs font-mono uppercase text-muted block mb-1 font-semibold">
                  Test Slot Values
                </span>
                <div className="space-y-2 max-h-48 overflow-y-auto pr-1">
                  {Object.entries(slotInputs).map(([slotKey, slotVal]) => (
                    <div key={slotKey} className="flex items-center gap-2">
                      <span className="text-xs font-mono text-muted w-28 truncate shrink-0" title={slotKey}>
                        {slotKey}:
                      </span>
                      <input
                        type="text"
                        value={slotVal}
                        onChange={(e) => setSlotInputs({ ...slotInputs, [slotKey]: e.target.value })}
                        className="flex-1 bg-bg border border-border rounded px-2 py-1 text-xs text-foreground font-mono focus:outline-none focus:border-accent"
                      />
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-3 border-t border-border bg-panel flex items-center justify-between shrink-0">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 text-xs font-mono text-muted hover:text-foreground"
          >
            Cancel
          </button>
          <div className="flex gap-2">
            <button
              type="button"
              onClick={handleRenderPreview}
              disabled={isRendering}
              className="px-4 py-2 border border-border hover:bg-bg rounded text-xs font-mono text-foreground"
            >
              Test Render
            </button>
            <button
              type="button"
              onClick={handleSave}
              disabled={isSaving || !name.trim()}
              className="px-5 py-2 bg-accent text-accent-foreground font-medium rounded text-xs font-mono hover:opacity-90 disabled:opacity-50"
            >
              {isSaving ? "Saving..." : "Save Template"}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
