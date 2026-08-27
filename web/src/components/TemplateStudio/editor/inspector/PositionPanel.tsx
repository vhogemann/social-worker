import { EditorLayer, EditorCircleLayer } from '../schemaCodec';
import { useTemplateEditorStore } from '../templateEditorStore';

interface PositionPanelProps {
  layer: EditorLayer;
}

export function PositionPanel({ layer }: PositionPanelProps) {
  const { updateLayer } = useTemplateEditorStore();

  if (layer.kind === 'circle') {
    const circle = layer as EditorCircleLayer;
    return (
      <div className="space-y-3 pt-2 border-t border-border">
        <span className="text-[10px] font-mono uppercase tracking-wider text-muted block font-semibold">
          Transform
        </span>
        <div className="grid grid-cols-2 gap-2">
          <div>
            <label className="text-[10px] font-mono text-muted block mb-0.5">Center X</label>
            <input
              type="number"
              value={circle.cx}
              onChange={(e) => updateLayer(circle.id, { cx: parseInt(e.target.value) || 0 })}
              className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
            />
          </div>
          <div>
            <label className="text-[10px] font-mono text-muted block mb-0.5">Center Y</label>
            <input
              type="number"
              value={circle.cy}
              onChange={(e) => updateLayer(circle.id, { cy: parseInt(e.target.value) || 0 })}
              className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
            />
          </div>
          <div>
            <label className="text-[10px] font-mono text-muted block mb-0.5">Radius</label>
            <input
              type="number"
              min={1}
              value={circle.radius}
              onChange={(e) => updateLayer(circle.id, { radius: Math.max(1, parseInt(e.target.value) || 1) })}
              className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
            />
          </div>
          <div>
            <label className="text-[10px] font-mono text-muted block mb-0.5">Opacity</label>
            <input
              type="number"
              min={0}
              max={1}
              step={0.05}
              value={circle.opacity ?? 1}
              onChange={(e) => updateLayer(circle.id, { opacity: parseFloat(e.target.value) || 0 })}
              className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
            />
          </div>
        </div>
      </div>
    );
  }

  const standard = layer as any;

  return (
    <div className="space-y-3 pt-2 border-t border-border">
      <span className="text-[10px] font-mono uppercase tracking-wider text-muted block font-semibold">
        Transform
      </span>
      <div className="grid grid-cols-2 gap-2">
        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">X Position</label>
          <input
            type="number"
            value={standard.x}
            onChange={(e) => updateLayer(layer.id, { x: parseInt(e.target.value) || 0 })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          />
        </div>
        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Y Position</label>
          <input
            type="number"
            value={standard.y}
            onChange={(e) => updateLayer(layer.id, { y: parseInt(e.target.value) || 0 })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          />
        </div>

        {standard.w !== undefined && (
          <div>
            <label className="text-[10px] font-mono text-muted block mb-0.5">Width</label>
            <input
              type="number"
              min={1}
              value={standard.w}
              onChange={(e) => updateLayer(layer.id, { w: Math.max(1, parseInt(e.target.value) || 1) })}
              className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
            />
          </div>
        )}

        {standard.h !== undefined && (
          <div>
            <label className="text-[10px] font-mono text-muted block mb-0.5">Height</label>
            <input
              type="number"
              min={1}
              value={standard.h}
              onChange={(e) => updateLayer(layer.id, { h: Math.max(1, parseInt(e.target.value) || 1) })}
              className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
            />
          </div>
        )}

        <div>
          <label className="text-[10px] font-mono text-muted block mb-0.5">Opacity</label>
          <input
            type="number"
            min={0}
            max={1}
            step={0.05}
            value={layer.opacity ?? 1}
            onChange={(e) => updateLayer(layer.id, { opacity: parseFloat(e.target.value) || 0 })}
            className="w-full bg-bg border border-border rounded px-2 py-1 text-xs font-mono text-foreground focus:outline-none focus:border-accent"
          />
        </div>
      </div>
    </div>
  );
}
