import { useState } from 'react';
import { useTemplateEditorStore } from '../templateEditorStore';
import { PositionPanel } from './PositionPanel';
import { TextInspector } from './TextInspector';
import { RectInspector } from './RectInspector';
import { CircleInspector } from './CircleInspector';
import { ImageInspector } from './ImageInspector';
import { SlotManager } from '../slots/SlotManager';
import {
  EditorRectLayer,
  EditorCircleLayer,
  EditorImageLayer,
  EditorTextLayer,
} from '../schemaCodec';

export function LayerInspector() {
  const { layers, selectedId, deleteLayer, duplicateLayer } = useTemplateEditorStore();
  const [activeTab, setActiveTab] = useState<'layer' | 'slots'>('layer');

  const selectedLayer = layers.find((l) => l.id === selectedId);

  return (
    <div className="w-80 border-l border-border bg-panel flex flex-col h-full overflow-hidden text-foreground shrink-0">
      {/* Tab bar */}
      <div className="flex border-b border-border bg-panel shrink-0">
        <button
          type="button"
          onClick={() => setActiveTab('layer')}
          className={`flex-1 py-2.5 text-xs font-mono font-medium border-b-2 transition-colors ${
            activeTab === 'layer'
              ? 'border-accent text-foreground'
              : 'border-transparent text-muted hover:text-foreground'
          }`}
        >
          Properties
        </button>
        <button
          type="button"
          onClick={() => setActiveTab('slots')}
          className={`flex-1 py-2.5 text-xs font-mono font-medium border-b-2 transition-colors ${
            activeTab === 'slots'
              ? 'border-accent text-foreground'
              : 'border-transparent text-muted hover:text-foreground'
          }`}
        >
          Slots
        </button>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4">
        {activeTab === 'slots' ? (
          <SlotManager />
        ) : !selectedLayer ? (
          <div className="text-center py-16 text-muted text-xs font-mono space-y-2">
            <div>No layer selected</div>
            <div className="text-[11px] text-muted/60">
              Click any element on the canvas or in the layer list to edit its properties.
            </div>
          </div>
        ) : (
          <div className="space-y-4">
            {/* Header with layer kind & quick actions */}
            <div className="flex items-center justify-between pb-3 border-b border-border">
              <div className="flex items-center gap-2">
                <span className="text-xs font-mono font-semibold uppercase text-accent">
                  {selectedLayer.kind}
                </span>
                <span className="text-[10px] font-mono text-muted truncate max-w-[120px]">
                  {selectedLayer.id}
                </span>
              </div>

              <div className="flex items-center gap-1 text-xs font-mono">
                <button
                  type="button"
                  onClick={() => duplicateLayer(selectedLayer.id)}
                  className="px-2 py-1 bg-bg hover:bg-panel-light border border-border rounded text-muted hover:text-foreground"
                  title="Duplicate layer"
                >
                  Clone
                </button>
                <button
                  type="button"
                  onClick={() => deleteLayer(selectedLayer.id)}
                  className="px-2 py-1 bg-bg hover:bg-destructive/20 border border-border hover:border-destructive/40 rounded text-muted hover:text-destructive"
                  title="Delete layer"
                >
                  Del
                </button>
              </div>
            </div>

            {/* Specialized inspector per kind */}
            {selectedLayer.kind === 'text' && (
              <TextInspector layer={selectedLayer as EditorTextLayer} />
            )}
            {selectedLayer.kind === 'rect' && (
              <RectInspector layer={selectedLayer as EditorRectLayer} />
            )}
            {selectedLayer.kind === 'circle' && (
              <CircleInspector layer={selectedLayer as EditorCircleLayer} />
            )}
            {selectedLayer.kind === 'image' && (
              <ImageInspector layer={selectedLayer as EditorImageLayer} />
            )}

            {/* Position & Size */}
            <PositionPanel layer={selectedLayer} />
          </div>
        )}
      </div>
    </div>
  );
}
