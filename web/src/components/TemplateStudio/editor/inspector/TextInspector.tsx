import { EditorTextLayer } from '../schemaCodec';
import { useTemplateEditorStore } from '../templateEditorStore';

interface TextInspectorProps {
  layer: EditorTextLayer;
}

export function TextInspector({ layer }: TextInspectorProps) {
  const { updateLayer, slots } = useTemplateEditorStore();

  const textSlots = slots.filter((s) => s.type === 'text');

  return (
    <div className="space-y-4">
      {/* Slot or Static Text */}
      <div>
        <label className="text-[10px] font-mono uppercase tracking-wider text-muted block mb-1">
          Dynamic Slot Binding
        </label>
        <select
          value={layer.slot || ''}
          onChange={(e) => updateLayer(layer.id, { slot: e.target.value || undefined })}
          className="w-full bg-bg border border-border rounded px-2 py-1.5 text-xs text-foreground focus:outline-none focus:border-accent"
        >
          <option value="">(None - Static Text)</option>
          {textSlots.map((s) => (
            <option key={s.name} value={s.name}>
              {s.name} {s.required ? '*' : ''}
            </option>
          ))}
        </select>
      </div>

      <div>
        <label className="text-[10px] font-mono uppercase tracking-wider text-muted block mb-1">
          {layer.slot ? 'Preview / Fallback Text' : 'Text Content'}
        </label>
        <textarea
          rows={3}
          value={layer.text || ''}
          onChange={(e) => updateLayer(layer.id, { text: e.target.value })}
          placeholder="Enter text..."
          className="w-full bg-bg border border-border rounded p-2 text-xs text-foreground focus:outline-none focus:border-accent resize-none font-sans"
        />
      </div>

      {/* Typography */}
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Font Size (px)</label>
          <input
            type="number"
            min={8}
            max={300}
            value={layer.fontSize}
            onChange={(e) => updateLayer(layer.id, { fontSize: parseInt(e.target.value) || 32 })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          />
        </div>

        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Font Family</label>
          <select
            value={layer.fontFamily}
            onChange={(e) => updateLayer(layer.id, { fontFamily: e.target.value })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs text-foreground focus:outline-none focus:border-accent"
          >
            <optgroup label="Sans-Serif (Modern & Clean)">
              <option value="Inter, sans-serif">Inter</option>
              <option value="Plus Jakarta Sans, sans-serif">Plus Jakarta Sans</option>
              <option value="Montserrat, sans-serif">Montserrat</option>
              <option value="Space Grotesk, sans-serif">Space Grotesk</option>
            </optgroup>
            <optgroup label="Display & Headlines">
              <option value="Bebas Neue, sans-serif">Bebas Neue (Impact)</option>
              <option value="Oswald, sans-serif">Oswald</option>
              <option value="Righteous, cursive">Righteous</option>
            </optgroup>
            <optgroup label="Editorial & Serif">
              <option value="Playfair Display, serif">Playfair Display</option>
              <option value="Lora, serif">Lora</option>
              <option value="Merriweather, serif">Merriweather</option>
            </optgroup>
            <optgroup label="Monospace & Code">
              <option value="JetBrains Mono, monospace">JetBrains Mono</option>
              <option value="Fira Code, monospace">Fira Code</option>
              <option value="Space Mono, monospace">Space Mono</option>
            </optgroup>
            <optgroup label="Handwritten & Accents">
              <option value="Caveat, cursive">Caveat (Handwritten)</option>
              <option value="Permanent Marker, cursive">Permanent Marker</option>
            </optgroup>
          </select>
        </div>

        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Weight</label>
          <select
            value={String(layer.fontWeight)}
            onChange={(e) => updateLayer(layer.id, { fontWeight: e.target.value })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs text-foreground focus:outline-none focus:border-accent"
          >
            <option value="normal">Normal (400)</option>
            <option value="bold">Bold (700)</option>
            <option value="600">Semi-Bold (600)</option>
            <option value="300">Light (300)</option>
          </select>
        </div>

        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Text Color</label>
          <div className="flex items-center gap-1.5">
            <input
              type="color"
              value={layer.color.startsWith('#') ? layer.color : '#ffffff'}
              onChange={(e) => updateLayer(layer.id, { color: e.target.value })}
              className="w-7 h-7 rounded border border-border bg-bg cursor-pointer p-0.5"
            />
            <input
              type="text"
              value={layer.color}
              onChange={(e) => updateLayer(layer.id, { color: e.target.value })}
              className="flex-1 bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
            />
          </div>
        </div>
      </div>

      {/* Alignment & Transform */}
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Alignment</label>
          <div className="flex bg-bg border border-border rounded p-0.5">
            <button
              type="button"
              onClick={() => updateLayer(layer.id, { align: 'left' })}
              className={`flex-1 py-0.5 text-xs font-mono rounded ${layer.align === 'left' ? 'bg-panel text-accent font-bold' : 'text-muted'}`}
            >
              Left
            </button>
            <button
              type="button"
              onClick={() => updateLayer(layer.id, { align: 'center' })}
              className={`flex-1 py-0.5 text-xs font-mono rounded ${layer.align === 'center' ? 'bg-panel text-accent font-bold' : 'text-muted'}`}
            >
              Center
            </button>
            <button
              type="button"
              onClick={() => updateLayer(layer.id, { align: 'right' })}
              className={`flex-1 py-0.5 text-xs font-mono rounded ${layer.align === 'right' ? 'bg-panel text-accent font-bold' : 'text-muted'}`}
            >
              Right
            </button>
          </div>
        </div>

        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Transform</label>
          <select
            value={layer.textTransform}
            onChange={(e) => updateLayer(layer.id, { textTransform: e.target.value as any })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs text-foreground focus:outline-none focus:border-accent"
          >
            <option value="none">None</option>
            <option value="uppercase">UPPERCASE</option>
            <option value="lowercase">lowercase</option>
          </select>
        </div>
      </div>

      {/* Line Height & Max Width */}
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Line Height</label>
          <input
            type="number"
            step={0.05}
            min={0.8}
            max={3}
            value={layer.lineHeight}
            onChange={(e) => updateLayer(layer.id, { lineHeight: parseFloat(e.target.value) || 1.3 })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          />
        </div>

        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Max Width (px)</label>
          <input
            type="number"
            value={layer.maxWidth || ''}
            placeholder="Auto"
            onChange={(e) => updateLayer(layer.id, { maxWidth: parseInt(e.target.value) || undefined })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          />
        </div>
      </div>

      {/* Prefix / Suffix */}
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Prefix</label>
          <input
            type="text"
            value={layer.prefix || ''}
            placeholder='e.g. "— "'
            onChange={(e) => updateLayer(layer.id, { prefix: e.target.value || undefined })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          />
        </div>

        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Suffix</label>
          <input
            type="text"
            value={layer.suffix || ''}
            placeholder='e.g. "%"'
            onChange={(e) => updateLayer(layer.id, { suffix: e.target.value || undefined })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          />
        </div>
      </div>
    </div>
  );
}
