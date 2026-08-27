import { useTemplateEditorStore } from './templateEditorStore';
import { EditorLayer } from './schemaCodec';

export function LayerListPanel() {
  const {
    layers,
    selectedId,
    setSelected,
    deleteLayer,
    duplicateLayer,
    moveLayerUp,
    moveLayerDown,
    updateLayer,
  } = useTemplateEditorStore();

  const getLayerLabel = (layer: EditorLayer) => {
    switch (layer.kind) {
      case 'text': {
        const t = layer as any;
        return t.slot ? `Text (${t.slot})` : (t.text ? `"${t.text.slice(0, 16)}..."` : 'Text');
      }
      case 'rect':
        return 'Rectangle';
      case 'circle':
        return 'Circle';
      case 'image': {
        const img = layer as any;
        return img.slot ? `Image (${img.slot})` : 'Image';
      }
      default:
        return 'Layer';
    }
  };

  // Render reversed so that the top-most z-index appears on top of the list
  const reversedLayers = [...layers].map((l, i) => ({ layer: l, originalIndex: i })).reverse();

  return (
    <div className="w-64 border-r border-border bg-panel flex flex-col h-full overflow-hidden text-foreground shrink-0">
      <div className="px-4 py-3 border-b border-border flex items-center justify-between shrink-0 bg-panel">
        <span className="text-xs font-mono uppercase tracking-wider text-muted font-semibold">
          Layers ({layers.length})
        </span>
      </div>

      <div className="flex-1 overflow-y-auto p-2 space-y-1">
        {reversedLayers.length === 0 ? (
          <div className="text-center py-12 text-xs font-mono text-muted">
            No layers. Add one from the toolbar above.
          </div>
        ) : (
          reversedLayers.map(({ layer, originalIndex }) => {
            const isSelected = layer.id === selectedId;
            return (
              <div
                key={layer.id}
                onClick={() => setSelected(layer.id)}
                className={`flex items-center justify-between px-3 py-2 rounded-lg text-xs font-mono cursor-pointer transition-colors group ${
                  isSelected
                    ? 'bg-accent/15 border border-accent/40 text-foreground font-medium'
                    : 'hover:bg-bg border border-transparent text-muted hover:text-foreground'
                }`}
              >
                <div className="flex items-center gap-2 min-w-0 flex-1">
                  <span className="text-[10px] text-muted uppercase font-bold shrink-0">
                    {layer.kind[0]}
                  </span>
                  <span className="truncate" title={getLayerLabel(layer)}>
                    {getLayerLabel(layer)}
                  </span>
                </div>

                <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity shrink-0">
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      updateLayer(layer.id, { opacity: layer.opacity > 0 ? 0 : 1 });
                    }}
                    className="p-1 hover:text-foreground text-muted"
                    title={layer.opacity > 0 ? 'Hide layer' : 'Show layer'}
                  >
                    {layer.opacity > 0 ? '👁' : '🚫'}
                  </button>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      moveLayerUp(layer.id);
                    }}
                    disabled={originalIndex === layers.length - 1}
                    className="p-0.5 hover:text-foreground text-muted disabled:opacity-20"
                    title="Bring forward"
                  >
                    ↑
                  </button>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      moveLayerDown(layer.id);
                    }}
                    disabled={originalIndex === 0}
                    className="p-0.5 hover:text-foreground text-muted disabled:opacity-20"
                    title="Send backward"
                  >
                    ↓
                  </button>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      duplicateLayer(layer.id);
                    }}
                    className="p-0.5 hover:text-foreground text-muted"
                    title="Duplicate"
                  >
                    +
                  </button>
                  <button
                    type="button"
                    onClick={(e) => {
                      e.stopPropagation();
                      deleteLayer(layer.id);
                    }}
                    className="p-0.5 hover:text-destructive text-muted"
                    title="Delete"
                  >
                    ✕
                  </button>
                </div>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}
