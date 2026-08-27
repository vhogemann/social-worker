import { EditorCircleLayer } from '../schemaCodec';
import { useTemplateEditorStore } from '../templateEditorStore';

interface CircleInspectorProps {
  layer: EditorCircleLayer;
}

export function CircleInspector({ layer }: CircleInspectorProps) {
  const { updateLayer } = useTemplateEditorStore();

  return (
    <div className="space-y-4">
      <div>
        <label className="text-[10px] font-mono uppercase tracking-wider text-muted block mb-1">
          Fill Color
        </label>
        <div className="flex items-center gap-1.5">
          <input
            type="color"
            value={layer.fill?.startsWith('#') ? layer.fill : '#ec4899'}
            onChange={(e) => updateLayer(layer.id, { fill: e.target.value })}
            className="w-7 h-7 rounded border border-border bg-bg cursor-pointer p-0.5"
          />
          <input
            type="text"
            value={layer.fill || ''}
            placeholder="#ec4899 or rgba(...)"
            onChange={(e) => updateLayer(layer.id, { fill: e.target.value || undefined })}
            className="flex-1 bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Stroke Color</label>
          <div className="flex items-center gap-1.5">
            <input
              type="color"
              value={layer.stroke?.startsWith('#') ? layer.stroke : '#ffffff'}
              onChange={(e) => updateLayer(layer.id, { stroke: e.target.value })}
              className="w-7 h-7 rounded border border-border bg-bg cursor-pointer p-0.5"
            />
            <input
              type="text"
              value={layer.stroke || ''}
              placeholder="None"
              onChange={(e) => updateLayer(layer.id, { stroke: e.target.value || undefined })}
              className="flex-1 bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
            />
          </div>
        </div>

        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Stroke Width (px)</label>
          <input
            type="number"
            min={0}
            max={50}
            value={layer.strokeWidth || 0}
            onChange={(e) => updateLayer(layer.id, { strokeWidth: parseInt(e.target.value) || 0 })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          />
        </div>
      </div>
    </div>
  );
}
