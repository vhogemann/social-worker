import { useTemplateEditorStore } from './templateEditorStore';

interface TemplateEditorToolbarProps {
  onOpenSource: () => void;
}

export function TemplateEditorToolbar({ onOpenSource }: TemplateEditorToolbarProps) {
  const { addLayer, width, height, setMeta } = useTemplateEditorStore();

  const handleSetRatio = (w: number, h: number) => {
    setMeta({ width: w, height: h });
  };

  return (
    <div className="h-12 border-b border-border bg-panel flex items-center justify-between px-4 shrink-0 text-foreground">
      {/* Add elements group */}
      <div className="flex items-center gap-2">
        <span className="text-xs font-mono text-muted mr-1 font-semibold">Add:</span>
        <button
          type="button"
          data-testid="add-layer-text"
          onClick={() => addLayer('text')}
          className="px-2.5 py-1 bg-bg hover:bg-panel-light border border-border rounded text-xs font-mono flex items-center gap-1.5 transition-colors"
        >
          <span className="text-accent font-bold">T</span> Text
        </button>
        <button
          type="button"
          data-testid="add-layer-rect"
          onClick={() => addLayer('rect')}
          className="px-2.5 py-1 bg-bg hover:bg-panel-light border border-border rounded text-xs font-mono flex items-center gap-1.5 transition-colors"
        >
          <span className="text-sky-400 font-bold">■</span> Rectangle
        </button>
        <button
          type="button"
          data-testid="add-layer-circle"
          onClick={() => addLayer('circle')}
          className="px-2.5 py-1 bg-bg hover:bg-panel-light border border-border rounded text-xs font-mono flex items-center gap-1.5 transition-colors"
        >
          <span className="text-pink-400 font-bold">●</span> Circle
        </button>
        <button
          type="button"
          data-testid="add-layer-image"
          onClick={() => addLayer('image')}
          className="px-2.5 py-1 bg-bg hover:bg-panel-light border border-border rounded text-xs font-mono flex items-center gap-1.5 transition-colors"
        >
          <span className="text-emerald-400 font-bold">🖼</span> Image
        </button>
      </div>

      {/* Preset ratios & Show source */}
      <div className="flex items-center gap-3">
        <div className="flex items-center gap-1 bg-bg border border-border p-0.5 rounded text-xs font-mono">
          <button
            type="button"
            onClick={() => handleSetRatio(1080, 1080)}
            className={`px-2 py-0.5 rounded ${width === 1080 && height === 1080 ? 'bg-panel text-accent font-semibold' : 'text-muted hover:text-foreground'}`}
            title="Square (1:1)"
          >
            1:1
          </button>
          <button
            type="button"
            onClick={() => handleSetRatio(1080, 1350)}
            className={`px-2 py-0.5 rounded ${width === 1080 && height === 1350 ? 'bg-panel text-accent font-semibold' : 'text-muted hover:text-foreground'}`}
            title="Portrait (4:5)"
          >
            4:5
          </button>
          <button
            type="button"
            onClick={() => handleSetRatio(1080, 1920)}
            className={`px-2 py-0.5 rounded ${width === 1080 && height === 1920 ? 'bg-panel text-accent font-semibold' : 'text-muted hover:text-foreground'}`}
            title="Story / Reel (9:16)"
          >
            9:16
          </button>
        </div>

        <button
          type="button"
          onClick={onOpenSource}
          className="px-3 py-1 bg-bg hover:bg-panel-light border border-border rounded text-xs font-mono text-muted hover:text-foreground flex items-center gap-1.5 transition-colors"
          title="View and edit declarative JSON schema"
        >
          <span>&lt;/&gt;</span> Show Source
        </button>
      </div>
    </div>
  );
}
