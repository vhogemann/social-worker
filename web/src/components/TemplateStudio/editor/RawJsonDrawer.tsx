import { useState, useEffect } from 'react';
import { useTemplateEditorStore } from './templateEditorStore';

interface RawJsonDrawerProps {
  isOpen: boolean;
  onClose: () => void;
}

export function RawJsonDrawer({ isOpen, onClose }: RawJsonDrawerProps) {
  const { toSchemaJson, loadFromSchema, templateId } = useTemplateEditorStore();
  const [jsonText, setJsonText] = useState('');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      setJsonText(toSchemaJson());
      setError(null);
    }
  }, [isOpen, toSchemaJson]);

  const handleFormat = () => {
    try {
      const parsed = JSON.parse(jsonText);
      setJsonText(JSON.stringify(parsed, null, 2));
      setError(null);
    } catch (err: any) {
      setError(err?.message || 'Invalid JSON syntax');
    }
  };

  const handleApply = () => {
    try {
      JSON.parse(jsonText);
      loadFromSchema(jsonText, templateId);
      setError(null);
      onClose();
    } catch (err: any) {
      setError(`Failed to apply JSON: ${err?.message}`);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-end bg-black/60 backdrop-blur-xs">
      <div className="w-full max-w-2xl h-full bg-panel border-l border-border shadow-2xl flex flex-col overflow-hidden text-foreground animate-in slide-in-from-right duration-200">
        {/* Header */}
        <div className="px-6 py-4 border-b border-border flex items-center justify-between shrink-0 bg-panel">
          <div>
            <h2 className="text-sm font-semibold text-foreground">Template Source JSON</h2>
            <p className="text-xs text-muted mt-0.5">
              Directly edit the declarative schema JSON for advanced layers and fine-tuning.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-muted hover:text-foreground text-sm font-mono"
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        {/* Editor Body */}
        <div className="flex-1 p-6 flex flex-col min-h-0 space-y-3 bg-panel-light">
          {error && (
            <div className="p-3 bg-red-500/10 border border-red-500/20 text-red-400 text-xs font-mono rounded">
              {error}
            </div>
          )}

          <div className="flex-1 flex flex-col min-h-0">
            <textarea
              value={jsonText}
              onChange={(e) => {
                setJsonText(e.target.value);
                setError(null);
              }}
              rows={24}
              className="flex-1 w-full bg-bg border border-border rounded p-4 font-mono text-xs text-foreground focus:outline-none focus:border-accent resize-none leading-relaxed"
              spellCheck={false}
            />
          </div>
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-border bg-panel flex items-center justify-between shrink-0">
          <button
            type="button"
            onClick={handleFormat}
            className="px-3 py-1.5 border border-border hover:bg-bg rounded text-xs font-mono text-muted hover:text-foreground"
          >
            Format JSON
          </button>

          <div className="flex gap-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-xs font-mono text-muted hover:text-foreground"
            >
              Cancel
            </button>
            <button
              type="button"
              onClick={handleApply}
              className="px-5 py-2 bg-accent text-accent-foreground font-medium rounded text-xs font-mono hover:opacity-90"
            >
              Apply Source
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
