import { useRef, useEffect, useState } from 'react';
import Konva from 'konva';
import { useTemplateEditorStore } from './templateEditorStore';
import { TextEditOverlay } from './TextEditOverlay';
import {
  EditorLayer,
  EditorRectLayer,
  EditorCircleLayer,
  EditorImageLayer,
  EditorTextLayer,
} from './schemaCodec';

interface WysiwygCanvasProps {
  scale: number;
}

export function WysiwygCanvas({ scale }: WysiwygCanvasProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const stageRef = useRef<Konva.Stage | null>(null);
  const layerRef = useRef<Konva.Layer | null>(null);
  const transformerRef = useRef<Konva.Transformer | null>(null);

  const {
    width,
    height,
    layers,
    selectedId,
    setSelected,
    updateLayer,
  } = useTemplateEditorStore();

  const [editingText, setEditingText] = useState<{
    layer: EditorTextLayer;
    x: number;
    y: number;
    width: number;
    height: number;
    fontSize: number;
  } | null>(null);

  // 1. Initialize Konva Stage and Transformer on mount
  useEffect(() => {
    if (!containerRef.current) return;

    const stage = new Konva.Stage({
      container: containerRef.current,
      width: width * scale,
      height: height * scale,
      scaleX: scale,
      scaleY: scale,
    });

    const konvaLayer = new Konva.Layer();
    stage.add(konvaLayer);

    const transformer = new Konva.Transformer({
      rotateEnabled: false,
      keepRatio: false,
      enabledAnchors: [
        'top-left',
        'top-center',
        'top-right',
        'middle-right',
        'middle-left',
        'bottom-left',
        'bottom-center',
        'bottom-right',
      ],
      boundBoxFunc: (oldBox, newBox) => {
        if (Math.abs(newBox.width) < 5 || Math.abs(newBox.height) < 5) {
          return oldBox;
        }
        return newBox;
      },
      anchorCornerRadius: 2,
      anchorSize: 8,
      anchorStroke: '#38bdf8',
      anchorFill: '#0f172a',
      borderStroke: '#38bdf8',
      borderDash: [4, 4],
    });
    konvaLayer.add(transformer);

    stage.on('pointerdown', (e) => {
      if (e.target === stage) {
        setSelected(null);
        setEditingText(null);
      }
    });

    stageRef.current = stage;
    layerRef.current = konvaLayer;
    transformerRef.current = transformer;

    return () => {
      stage.destroy();
      stageRef.current = null;
      layerRef.current = null;
      transformerRef.current = null;
    };
  }, []);

  // 2. Update Stage Dimensions and Scale when width / height / scale change
  useEffect(() => {
    const stage = stageRef.current;
    if (!stage) return;

    stage.width(width * scale);
    stage.height(height * scale);
    stage.scale({ x: scale, y: scale });
    stage.batchDraw();
  }, [width, height, scale]);

  // 3. Synchronize layers to Konva shapes
  useEffect(() => {
    const stage = stageRef.current;
    const konvaLayer = layerRef.current;
    const transformer = transformerRef.current;
    if (!stage || !konvaLayer || !transformer) return;

    // Remove old shapes except transformer
    const children = konvaLayer.getChildren((node) => node !== transformer);
    for (const child of children) {
      child.destroy();
    }

    for (const layer of layers) {
      let shapeNode: Konva.Shape | null = null;

      switch (layer.kind) {
        case 'rect': {
          const rect = layer as EditorRectLayer;
          const node = new Konva.Rect({
            id: rect.id,
            x: rect.x,
            y: rect.y,
            width: rect.w,
            height: rect.h,
            fill: rect.fill || '#38bdf8',
            stroke: rect.stroke,
            strokeWidth: rect.strokeWidth,
            cornerRadius: rect.borderRadius || 0,
            opacity: rect.opacity ?? 1,
            draggable: true,
          });

          node.on('dragend', (e) => {
            updateLayer(rect.id, {
              x: Math.round(e.target.x()),
              y: Math.round(e.target.y()),
            });
          });

          node.on('transformend', () => {
            const scaleX = node.scaleX();
            const scaleY = node.scaleY();
            node.scaleX(1);
            node.scaleY(1);

            updateLayer(rect.id, {
              x: Math.round(node.x()),
              y: Math.round(node.y()),
              w: Math.max(5, Math.round(node.width() * scaleX)),
              h: Math.max(5, Math.round(node.height() * scaleY)),
            });
          });

          shapeNode = node;
          break;
        }

        case 'circle': {
          const circle = layer as EditorCircleLayer;
          const node = new Konva.Circle({
            id: circle.id,
            x: circle.cx,
            y: circle.cy,
            radius: circle.radius,
            fill: circle.fill || '#ec4899',
            stroke: circle.stroke,
            strokeWidth: circle.strokeWidth,
            opacity: circle.opacity ?? 1,
            draggable: true,
          });

          node.on('dragend', (e) => {
            updateLayer(circle.id, {
              cx: Math.round(e.target.x()),
              cy: Math.round(e.target.y()),
            });
          });

          node.on('transformend', () => {
            const scaleX = node.scaleX();
            node.scaleX(1);
            node.scaleY(1);

            updateLayer(circle.id, {
              cx: Math.round(node.x()),
              cy: Math.round(node.y()),
              radius: Math.max(5, Math.round(node.radius() * scaleX)),
            });
          });

          shapeNode = node;
          break;
        }

        case 'image': {
          const imgLayer = layer as EditorImageLayer;
          const src = imgLayer.previewUrl || imgLayer.src;

          if (src) {
            const imgEl = new window.Image();
            imgEl.crossOrigin = 'anonymous';
            imgEl.src = src;

            const node = new Konva.Image({
              id: imgLayer.id,
              image: imgEl,
              x: imgLayer.x,
              y: imgLayer.y,
              width: imgLayer.w,
              height: imgLayer.h,
              cornerRadius: imgLayer.borderRadius || 0,
              opacity: imgLayer.opacity ?? 1,
              draggable: true,
            });

            imgEl.onload = () => {
              node.image(imgEl);
              konvaLayer.batchDraw();
            };

            node.on('dragend', (e) => {
              updateLayer(imgLayer.id, {
                x: Math.round(e.target.x()),
                y: Math.round(e.target.y()),
              });
            });

            node.on('transformend', () => {
              const scaleX = node.scaleX();
              const scaleY = node.scaleY();
              node.scaleX(1);
              node.scaleY(1);

              updateLayer(imgLayer.id, {
                x: Math.round(node.x()),
                y: Math.round(node.y()),
                w: Math.max(10, Math.round(node.width() * scaleX)),
                h: Math.max(10, Math.round(node.height() * scaleY)),
              });
            });

            shapeNode = node;
          } else {
            const node = new Konva.Rect({
              id: imgLayer.id,
              x: imgLayer.x,
              y: imgLayer.y,
              width: imgLayer.w,
              height: imgLayer.h,
              fill: '#1e293b',
              stroke: '#475569',
              strokeWidth: 2,
              dash: [8, 6],
              cornerRadius: imgLayer.borderRadius || 0,
              opacity: imgLayer.opacity ?? 1,
              draggable: true,
            });

            node.on('dragend', (e) => {
              updateLayer(imgLayer.id, {
                x: Math.round(e.target.x()),
                y: Math.round(e.target.y()),
              });
            });

            node.on('transformend', () => {
              const scaleX = node.scaleX();
              const scaleY = node.scaleY();
              node.scaleX(1);
              node.scaleY(1);

              updateLayer(imgLayer.id, {
                x: Math.round(node.x()),
                y: Math.round(node.y()),
                w: Math.max(10, Math.round(node.width() * scaleX)),
                h: Math.max(10, Math.round(node.height() * scaleY)),
              });
            });

            shapeNode = node;
          }
          break;
        }

        case 'text':
        default: {
          const textLayer = layer as EditorTextLayer;
          let displayContent = textLayer.text || (textLayer.slot ? `[${textLayer.slot}]` : 'Text');
          if (textLayer.prefix) displayContent = `${textLayer.prefix}${displayContent}`;
          if (textLayer.suffix) displayContent = `${displayContent}${textLayer.suffix}`;
          if (textLayer.textTransform === 'uppercase') displayContent = displayContent.toUpperCase();
          if (textLayer.textTransform === 'lowercase') displayContent = displayContent.toLowerCase();

          const node = new Konva.Text({
            id: textLayer.id,
            x: textLayer.x,
            y: textLayer.y,
            text: displayContent,
            fontSize: textLayer.fontSize || 32,
            fontFamily: textLayer.fontFamily || 'Inter, sans-serif',
            fontStyle: String(textLayer.fontWeight || 'normal'),
            fill: textLayer.color || '#ffffff',
            align: textLayer.align || 'left',
            width: textLayer.maxWidth || undefined,
            lineHeight: textLayer.lineHeight || 1.3,
            opacity: textLayer.opacity ?? 1,
            draggable: true,
          });

          node.on('dblclick', () => {
            setEditingText({
              layer: textLayer,
              x: node.x() * scale,
              y: node.y() * scale,
              width: Math.max(100, (node.width() || 200) * scale),
              height: Math.max(40, (node.height() || 60) * scale),
              fontSize: (textLayer.fontSize || 32) * scale,
            });
          });

          node.on('dragend', (e) => {
            updateLayer(textLayer.id, {
              x: Math.round(e.target.x()),
              y: Math.round(e.target.y()),
            });
          });

          node.on('transformend', () => {
            const scaleX = node.scaleX();
            node.scaleX(1);
            node.scaleY(1);

            updateLayer(textLayer.id, {
              x: Math.round(node.x()),
              y: Math.round(node.y()),
              maxWidth: Math.max(50, Math.round((node.width() || 200) * scaleX)),
            });
          });

          shapeNode = node;
          break;
        }
      }

      if (shapeNode) {
        shapeNode.on('click tap', () => {
          setSelected(layer.id);
        });
        konvaLayer.add(shapeNode);
      }
    }

    // Move transformer to top
    transformer.moveToTop();

    // Attach transformer to selected node
    if (selectedId) {
      const selectedNode = konvaLayer.findOne(`#${selectedId}`);
      if (selectedNode) {
        transformer.nodes([selectedNode]);
      } else {
        transformer.nodes([]);
      }
    } else {
      transformer.nodes([]);
    }

    konvaLayer.batchDraw();
  }, [layers, selectedId, scale]);

  return (
    <div className="relative flex items-center justify-center select-none">
      <div
        ref={containerRef}
        data-testid="konva-container"
        className="shadow-2xl border border-border/80 bg-zinc-900 rounded overflow-hidden"
      />

      {editingText && (
        <TextEditOverlay
          initialText={editingText.layer.text || ''}
          x={editingText.x}
          y={editingText.y}
          width={editingText.width}
          height={editingText.height}
          fontSize={editingText.fontSize}
          fontFamily={editingText.layer.fontFamily}
          color={editingText.layer.color}
          align={editingText.layer.align}
          lineHeight={editingText.layer.lineHeight}
          onSave={(text) => {
            updateLayer(editingText.layer.id, { text });
            setEditingText(null);
          }}
          onCancel={() => setEditingText(null)}
        />
      )}
    </div>
  );
}
