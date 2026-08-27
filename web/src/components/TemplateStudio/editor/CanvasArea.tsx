import { useState, useEffect, useRef } from 'react';
import { WysiwygCanvas } from './WysiwygCanvas';
import { useTemplateEditorStore } from './templateEditorStore';

export function CanvasArea() {
  const { width, height } = useTemplateEditorStore();
  const containerRef = useRef<HTMLDivElement>(null);
  const [zoomMode, setZoomMode] = useState<'fit' | 0.5 | 0.75 | 1>('fit');
  const [autoScale, setAutoScale] = useState(0.5);

  useEffect(() => {
    if (!containerRef.current) return;
    if (typeof ResizeObserver === 'undefined') return;

    const observer = new ResizeObserver((entries) => {
      for (const entry of entries) {
        const cw = entry.contentRect.width - 64;
        const ch = entry.contentRect.height - 64;
        if (cw > 0 && ch > 0) {
          const scale = Math.min(cw / width, ch / height);
          setAutoScale(Math.max(0.1, Math.min(1.5, scale)));
        }
      }
    });

    observer.observe(containerRef.current);
    return () => observer.disconnect();
  }, [width, height]);

  const effectiveScale = zoomMode === 'fit' ? autoScale : zoomMode;

  return (
    <div className="flex-1 min-w-0 min-h-0 flex flex-col bg-bg overflow-hidden relative">
      {/* Zoom Toolbar overlay */}
      <div className="absolute bottom-4 left-4 z-20 flex items-center gap-1 bg-panel/90 backdrop-blur border border-border px-2 py-1 rounded shadow-md text-xs font-mono">
        <button
          type="button"
          onClick={() => setZoomMode('fit')}
          className={`px-2 py-0.5 rounded ${zoomMode === 'fit' ? 'bg-accent text-accent-foreground font-semibold' : 'text-muted hover:text-foreground'}`}
        >
          Fit
        </button>
        <button
          type="button"
          onClick={() => setZoomMode(0.5)}
          className={`px-2 py-0.5 rounded ${zoomMode === 0.5 ? 'bg-accent text-accent-foreground font-semibold' : 'text-muted hover:text-foreground'}`}
        >
          50%
        </button>
        <button
          type="button"
          onClick={() => setZoomMode(0.75)}
          className={`px-2 py-0.5 rounded ${zoomMode === 0.75 ? 'bg-accent text-accent-foreground font-semibold' : 'text-muted hover:text-foreground'}`}
        >
          75%
        </button>
        <button
          type="button"
          onClick={() => setZoomMode(1)}
          className={`px-2 py-0.5 rounded ${zoomMode === 1 ? 'bg-accent text-accent-foreground font-semibold' : 'text-muted hover:text-foreground'}`}
        >
          100%
        </button>
        <span className="text-[10px] text-muted/60 ml-1 border-l border-border pl-2">
          {Math.round(effectiveScale * 100)}%
        </span>
      </div>

      {/* Canvas Viewport */}
      <div
        ref={containerRef}
        className="flex-1 w-full h-full overflow-auto flex items-center justify-center p-8 bg-[radial-gradient(#27272a_1px,transparent_1px)] [background-size:16px_16px]"
      >
        <WysiwygCanvas scale={effectiveScale} />
      </div>
    </div>
  );
}
