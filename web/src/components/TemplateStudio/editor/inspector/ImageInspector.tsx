import { useState } from 'react';
import { EditorImageLayer } from '../schemaCodec';
import { useTemplateEditorStore } from '../templateEditorStore';
import { uploadTemplateAsset } from '../../../../api/templates';

interface ImageInspectorProps {
  layer: EditorImageLayer;
}

export function ImageInspector({ layer: initialLayer }: ImageInspectorProps) {
  const layer = useTemplateEditorStore(
    (s) => (s.layers.find((l) => l.id === initialLayer.id) as EditorImageLayer) || initialLayer
  );

  const {
    updateLayer,
    slots,
    setImageSlotFile,
    setImageSlotUrl,
    setFixedImageAsset,
    clearImageSlot,
  } = useTemplateEditorStore();

  const imageSlots = slots.filter((s) => s.type === 'image');
  const [urlInput, setUrlInput] = useState('');
  const [isUploading, setIsUploading] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  // If layer has no slot bound or has src, it's in Fixed Image mode; otherwise Dynamic Slot mode
  const isFixedMode = !layer.slot || !!layer.src;

  const handleModeChange = (mode: 'slot' | 'fixed') => {
    setUploadError(null);
    if (mode === 'slot') {
      const defaultSlot = imageSlots[0]?.name || 'image';
      updateLayer(layer.id, { slot: defaultSlot, src: undefined });
    } else {
      updateLayer(layer.id, { slot: undefined });
    }
  };

  const handleFixedFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    try {
      setIsUploading(true);
      setUploadError(null);
      const res = await uploadTemplateAsset(file);
      setFixedImageAsset(layer.id, res.url);
    } catch (err: any) {
      setUploadError(err?.message || 'Failed to upload image asset');
    } finally {
      setIsUploading(false);
      e.target.value = '';
    }
  };

  const handleSampleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setImageSlotFile(layer.id, file);
    }
  };

  const handleApplyUrl = () => {
    if (urlInput.trim()) {
      if (isFixedMode) {
        setFixedImageAsset(layer.id, urlInput.trim());
      } else {
        setImageSlotUrl(layer.id, urlInput.trim());
      }
      setUrlInput('');
    }
  };

  const currentPreview = layer.previewUrl || layer.src;

  return (
    <div className="space-y-4">
      {/* Source Type Selector */}
      <div>
        <label className="text-[10px] font-mono uppercase tracking-wider text-muted block mb-1.5 font-semibold">
          Image Source Type
        </label>
        <div className="grid grid-cols-2 gap-1 p-0.5 bg-bg rounded border border-border">
          <button
            type="button"
            onClick={() => handleModeChange('fixed')}
            className={`py-1.5 px-2 text-xs font-mono rounded transition-colors ${
              isFixedMode
                ? 'bg-accent text-bg font-medium shadow-xs'
                : 'text-muted hover:text-foreground'
            }`}
          >
            Fixed Asset / Logo
          </button>
          <button
            type="button"
            onClick={() => handleModeChange('slot')}
            className={`py-1.5 px-2 text-xs font-mono rounded transition-colors ${
              !isFixedMode
                ? 'bg-accent text-bg font-medium shadow-xs'
                : 'text-muted hover:text-foreground'
            }`}
          >
            Dynamic Slot
          </button>
        </div>
      </div>

      {/* Dynamic Slot Config */}
      {!isFixedMode && (
        <div className="space-y-2">
          <label className="text-[10px] font-mono uppercase tracking-wider text-muted block">
            Bind to Slot
          </label>
          <select
            aria-label="Bind to Slot"
            value={layer.slot || ''}
            onChange={(e) => updateLayer(layer.id, { slot: e.target.value || undefined, src: undefined })}
            className="w-full bg-bg border border-border rounded px-2 py-1.5 text-xs text-foreground focus:outline-none focus:border-accent"
          >
            {imageSlots.map((s) => (
              <option key={s.name} value={s.name}>
                {s.name} {s.required ? '*' : ''}
              </option>
            ))}
            {imageSlots.length === 0 && <option value="image">image (default)</option>}
          </select>
        </div>
      )}

      {/* Fixed Asset Upload & Management */}
      {isFixedMode ? (
        <div className="border border-border bg-bg/50 rounded-lg p-3 space-y-3">
          <div className="flex items-center justify-between">
            <span className="text-[10px] font-mono uppercase tracking-wider text-muted block font-semibold">
              Fixed Image / Watermark
            </span>
            {layer.src && (
              <span className="text-[10px] font-mono text-emerald-400">Attached</span>
            )}
          </div>

          {currentPreview ? (
            <div className="space-y-2">
              <div className="relative aspect-video w-full bg-black/60 rounded border border-border overflow-hidden flex items-center justify-center">
                <img
                  src={currentPreview}
                  alt="Fixed image preview"
                  className="w-full h-full object-contain"
                />
              </div>
              <div className="flex gap-2">
                <label className="flex-1 cursor-pointer text-center py-1 px-2 border border-border hover:bg-panel rounded text-xs font-mono text-muted hover:text-foreground transition-colors">
                  <span>{isUploading ? 'Uploading...' : 'Replace Image'}</span>
                  <input
                    type="file"
                    accept="image/*"
                    disabled={isUploading}
                    onChange={handleFixedFileUpload}
                    className="hidden"
                  />
                </label>
                <button
                  type="button"
                  onClick={() => clearImageSlot(layer.id)}
                  className="px-3 py-1 text-xs font-mono text-red-400 hover:text-red-300 border border-red-500/20 hover:bg-red-500/10 rounded"
                >
                  Remove
                </button>
              </div>
            </div>
          ) : (
            <div className="space-y-2">
              <div>
                <label className="block w-full cursor-pointer text-center py-3 px-3 border border-dashed border-border hover:border-accent rounded text-xs font-mono text-muted hover:text-foreground bg-bg transition-colors">
                  <span>{isUploading ? 'Uploading to Server...' : '+ Upload Fixed Image / Logo'}</span>
                  <input
                    type="file"
                    accept="image/*"
                    disabled={isUploading}
                    onChange={handleFixedFileUpload}
                    className="hidden"
                  />
                </label>
              </div>

              <div className="flex gap-1">
                <input
                  type="text"
                  placeholder="Or paste image URL..."
                  value={urlInput}
                  onChange={(e) => setUrlInput(e.target.value)}
                  className="flex-1 bg-bg border border-border rounded px-2 py-1 text-xs text-foreground placeholder:text-muted/60 focus:outline-none focus:border-accent"
                />
                <button
                  type="button"
                  onClick={handleApplyUrl}
                  disabled={!urlInput.trim()}
                  className="px-2.5 py-1 bg-panel border border-border hover:bg-bg rounded text-xs font-mono text-foreground disabled:opacity-40"
                >
                  Set
                </button>
              </div>
            </div>
          )}

          {uploadError && (
            <p className="text-[11px] font-mono text-red-400 bg-red-500/10 p-2 rounded border border-red-500/20">
              {uploadError}
            </p>
          )}
        </div>
      ) : (
        /* In-Editor Preview / Sample for Dynamic Slot */
        <div className="border border-border bg-bg/50 rounded-lg p-3 space-y-3">
          <span className="text-[10px] font-mono uppercase tracking-wider text-muted block font-semibold">
            Editor Sample Preview (Design Only)
          </span>

          {currentPreview ? (
            <div className="space-y-2">
              <div className="relative aspect-video w-full bg-black/60 rounded border border-border overflow-hidden flex items-center justify-center">
                <img
                  src={currentPreview}
                  alt="Sample preview"
                  className="w-full h-full object-contain"
                />
              </div>
              <button
                type="button"
                onClick={() => clearImageSlot(layer.id)}
                className="w-full py-1 text-xs font-mono text-red-400 hover:text-red-300 border border-red-500/20 hover:bg-red-500/10 rounded"
              >
                Remove Sample Image
              </button>
            </div>
          ) : (
            <div className="space-y-2">
              <div>
                <label className="block w-full cursor-pointer text-center py-2 px-3 border border-dashed border-border hover:border-accent rounded text-xs font-mono text-muted hover:text-foreground bg-bg transition-colors">
                  <span>Upload Sample Mock File</span>
                  <input
                    type="file"
                    accept="image/*"
                    onChange={handleSampleFileUpload}
                    className="hidden"
                  />
                </label>
              </div>

              <div className="flex gap-1">
                <input
                  type="text"
                  placeholder="Or paste mock URL..."
                  value={urlInput}
                  onChange={(e) => setUrlInput(e.target.value)}
                  className="flex-1 bg-bg border border-border rounded px-2 py-1 text-xs text-foreground placeholder:text-muted/60 focus:outline-none focus:border-accent"
                />
                <button
                  type="button"
                  onClick={handleApplyUrl}
                  disabled={!urlInput.trim()}
                  className="px-2.5 py-1 bg-panel border border-border hover:bg-bg rounded text-xs font-mono text-foreground disabled:opacity-40"
                >
                  Set
                </button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Fit Mode & Corner Radius */}
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Fit Mode</label>
          <select
            value={layer.fit || 'cover'}
            onChange={(e) => updateLayer(layer.id, { fit: e.target.value as any })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs text-foreground focus:outline-none focus:border-accent"
          >
            <option value="cover">Cover (Fill & Crop)</option>
            <option value="contain">Contain (Fit inside)</option>
            <option value="fill">Fill (Stretch)</option>
          </select>
        </div>

        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Corner Radius</label>
          <input
            type="number"
            min={0}
            max={500}
            value={layer.borderRadius || 0}
            onChange={(e) => updateLayer(layer.id, { borderRadius: parseInt(e.target.value) || 0 })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          />
        </div>
      </div>

      {/* Opacity */}
      <div>
        <label className="text-[10px] font-mono text-muted block mb-0.5">
          Opacity ({Math.round((layer.opacity ?? 1) * 100)}%)
        </label>
        <input
          type="range"
          min={0}
          max={1}
          step={0.05}
          value={layer.opacity ?? 1}
          onChange={(e) => updateLayer(layer.id, { opacity: parseFloat(e.target.value) })}
          className="w-full accent-accent"
        />
      </div>
    </div>
  );
}
