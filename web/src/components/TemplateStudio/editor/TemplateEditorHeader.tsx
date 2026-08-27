import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTemplateEditorStore } from './templateEditorStore';
import { createTemplate, updateTemplate, previewTemplateWithImages, TemplateDto } from '../../../api/templates';

export function TemplateEditorHeader() {
  const navigate = useNavigate();
  const {
    templateId,
    name,
    description,
    width,
    height,
    setMeta,
    toSchemaJson,
    getImageFiles,
    slots,
    isDirty,
  } = useTemplateEditorStore();

  const [isSaving, setIsSaving] = useState(false);
  const [isRendering, setIsRendering] = useState(false);
  const [renderPreviewBlobUrl, setRenderPreviewBlobUrl] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isEditingName, setIsEditingName] = useState(false);

  const handleSave = async () => {
    if (!name.trim()) {
      setErrorMessage('Template name is required.');
      return;
    }

    try {
      setIsSaving(true);
      setErrorMessage(null);
      const schema = toSchemaJson();

      let saved: TemplateDto;
      if (templateId) {
        saved = await updateTemplate(templateId, {
          name: name.trim(),
          description: description?.trim() || null,
          width,
          height,
          schema,
        });
      } else {
        saved = await createTemplate({
          name: name.trim(),
          description: description?.trim() || null,
          width,
          height,
          schema,
        });
      }

      useTemplateEditorStore.setState({ isDirty: false, templateId: saved.id });
      navigate('/templates');
    } catch (err: any) {
      setErrorMessage(err?.message || 'Failed to save template');
    } finally {
      setIsSaving(false);
    }
  };

  const handleTestRender = async () => {
    try {
      setIsRendering(true);
      setErrorMessage(null);

      const schema = toSchemaJson();
      const imageFiles = getImageFiles();

      const sampleSlots: Record<string, string> = {};
      for (const slot of slots) {
        if (slot.type === 'text') {
          sampleSlots[slot.name] = slot.defaultValue || `Sample ${slot.name}`;
        }
      }

      const blob = await previewTemplateWithImages(schema, sampleSlots, imageFiles);
      if (renderPreviewBlobUrl) {
        URL.revokeObjectURL(renderPreviewBlobUrl);
      }
      setRenderPreviewBlobUrl(URL.createObjectURL(blob));
    } catch (err: any) {
      setErrorMessage(`Test render failed: ${err?.message}`);
    } finally {
      setIsRendering(false);
    }
  };

  return (
    <>
      <header className="h-14 border-b border-border bg-panel flex items-center justify-between px-6 shrink-0 text-foreground">
        {/* Left: Back button + Name */}
        <div className="flex items-center gap-4 min-w-0">
          <button
            type="button"
            onClick={() => {
              if (isDirty && !window.confirm('You have unsaved changes. Discard and leave?')) {
                return;
              }
              navigate('/templates');
            }}
            className="text-xs font-mono text-muted hover:text-foreground flex items-center gap-1 shrink-0"
          >
            ← Templates
          </button>

          <span className="text-border">|</span>

          {isEditingName ? (
            <input
              type="text"
              value={name}
              autoFocus
              onBlur={() => setIsEditingName(false)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' || e.key === 'Escape') setIsEditingName(false);
              }}
              onChange={(e) => setMeta({ name: e.target.value })}
              className="bg-bg border border-accent rounded px-2 py-1 text-sm font-semibold text-foreground focus:outline-none"
            />
          ) : (
            <div
              onClick={() => setIsEditingName(true)}
              className="cursor-pointer hover:bg-bg/50 px-2 py-1 rounded flex items-center gap-2 min-w-0"
              title="Click to rename"
            >
              <h1 className="text-sm font-semibold text-foreground truncate">
                {name || 'Untitled Template'}
              </h1>
              <span className="text-muted/50 text-xs">✎</span>
            </div>
          )}

          <span className="text-xs font-mono px-2 py-0.5 rounded bg-bg text-muted border border-border shrink-0">
            {width}x{height}
          </span>
        </div>

        {/* Right: Actions */}
        <div className="flex items-center gap-3 shrink-0">
          {errorMessage && (
            <span className="text-xs text-red-400 font-mono truncate max-w-xs" title={errorMessage}>
              {errorMessage}
            </span>
          )}

          <button
            type="button"
            onClick={handleTestRender}
            disabled={isRendering}
            className="px-3.5 py-1.5 border border-border hover:bg-bg rounded text-xs font-mono text-foreground flex items-center gap-1.5 transition-colors disabled:opacity-50"
          >
            {isRendering ? <span className="animate-spin">⟳</span> : '⟳'} Test Render
          </button>

          <button
            type="button"
            onClick={handleSave}
            disabled={isSaving || !name.trim()}
            className="px-4 py-1.5 bg-accent text-accent-foreground font-medium rounded text-xs font-mono hover:opacity-90 transition-opacity disabled:opacity-50"
          >
            {isSaving ? 'Saving...' : 'Save Template'}
          </button>
        </div>
      </header>

      {/* Render Preview Modal */}
      {renderPreviewBlobUrl && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-xs p-4">
          <div className="bg-panel border border-border rounded-xl shadow-2xl max-w-3xl w-full max-h-[90vh] flex flex-col overflow-hidden">
            <div className="px-6 py-3 border-b border-border flex items-center justify-between">
              <span className="text-xs font-mono uppercase tracking-wider text-muted font-semibold">
                Server-Authoritative Test Render ({width}x{height})
              </span>
              <button
                type="button"
                onClick={() => setRenderPreviewBlobUrl(null)}
                className="text-muted hover:text-foreground text-sm font-mono"
              >
                ✕
              </button>
            </div>

            <div className="p-6 flex-1 flex items-center justify-center bg-bg overflow-auto min-h-[360px]">
              <img
                src={renderPreviewBlobUrl}
                alt="Server preview"
                className="max-h-[600px] max-w-full object-contain rounded shadow-lg"
              />
            </div>

            <div className="px-6 py-3 border-t border-border bg-panel flex justify-end">
              <button
                type="button"
                onClick={() => setRenderPreviewBlobUrl(null)}
                className="px-4 py-1.5 bg-accent text-accent-foreground rounded text-xs font-mono font-medium hover:opacity-90"
              >
                Done
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
