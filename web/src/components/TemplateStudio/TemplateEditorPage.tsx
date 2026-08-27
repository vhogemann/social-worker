import { useEffect, useState } from 'react';
import { useParams, useLocation } from 'react-router-dom';
import { useTemplateEditorStore } from './editor/templateEditorStore';
import { fetchTemplate } from '../../api/templates';
import { TemplateEditorHeader } from './editor/TemplateEditorHeader';
import { TemplateEditorToolbar } from './editor/TemplateEditorToolbar';
import { LayerListPanel } from './editor/LayerListPanel';
import { CanvasArea } from './editor/CanvasArea';
import { LayerInspector } from './editor/inspector/LayerInspector';
import { RawJsonDrawer } from './editor/RawJsonDrawer';

export function TemplateEditorPage() {
  const { id } = useParams<{ id?: string }>();
  const location = useLocation();
  const { loadFromSchema, reset } = useTemplateEditorStore();

  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isSourceOpen, setIsSourceOpen] = useState(false);

  useEffect(() => {
    let isCancelled = false;

    async function init() {
      setIsLoading(true);
      setLoadError(null);

      try {
        const stateData = location.state as { generatedSchema?: any } | undefined;
        if (stateData?.generatedSchema) {
          const gen = stateData.generatedSchema;
          loadFromSchema(typeof gen.schema === 'string' ? gen.schema : JSON.stringify(gen.schema));
          useTemplateEditorStore.setState({
            name: gen.name || 'AI Generated Template',
            description: gen.description || '',
            width: gen.width || 1080,
            height: gen.height || 1080,
          });
        } else if (id) {
          const t = await fetchTemplate(id);
          if (!isCancelled) {
            loadFromSchema(t.schema, t.id);
            useTemplateEditorStore.setState({
              name: t.name,
              description: t.description || '',
              width: t.width,
              height: t.height,
            });
          }
        } else {
          reset(null);
        }
      } catch (err: any) {
        if (!isCancelled) {
          setLoadError(err?.message || 'Failed to load template');
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false);
        }
      }
    }

    void init();

    return () => {
      isCancelled = true;
    };
  }, [id, location.state, loadFromSchema, reset]);

  if (isLoading) {
    return (
      <div className="flex-1 min-h-0 flex items-center justify-center bg-bg text-muted font-mono text-xs">
        Loading template editor...
      </div>
    );
  }

  if (loadError) {
    return (
      <div className="flex-1 min-h-0 flex flex-col items-center justify-center bg-bg text-foreground p-6">
        <div className="p-4 bg-red-500/10 border border-red-500/20 text-red-400 text-sm font-mono rounded max-w-md text-center">
          {loadError}
        </div>
      </div>
    );
  }

  return (
    <div data-testid="template-editor-page" className="flex-1 min-h-0 flex flex-col bg-bg overflow-hidden text-foreground">
      {/* Header */}
      <TemplateEditorHeader />

      {/* Toolbar */}
      <TemplateEditorToolbar onOpenSource={() => setIsSourceOpen(true)} />

      {/* Main 3-column Layout */}
      <div className="flex-1 min-h-0 flex overflow-hidden">
        <LayerListPanel />
        <CanvasArea />
        <LayerInspector />
      </div>

      {/* Raw Source Drawer */}
      <RawJsonDrawer isOpen={isSourceOpen} onClose={() => setIsSourceOpen(false)} />
    </div>
  );
}
