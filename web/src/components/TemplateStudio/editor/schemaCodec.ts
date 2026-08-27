export type LayerKind = 'rect' | 'circle' | 'image' | 'text';

export interface BaseEditorLayer {
  id: string;
  kind: LayerKind;
  opacity: number;
}

export interface EditorRectLayer extends BaseEditorLayer {
  kind: 'rect';
  x: number;
  y: number;
  w: number;
  h: number;
  fill?: string;
  stroke?: string;
  strokeWidth?: number;
  borderRadius?: number;
}

export interface EditorCircleLayer extends BaseEditorLayer {
  kind: 'circle';
  cx: number;
  cy: number;
  radius: number;
  fill?: string;
  stroke?: string;
  strokeWidth?: number;
}

export interface EditorImageLayer extends BaseEditorLayer {
  kind: 'image';
  x: number;
  y: number;
  w: number;
  h: number;
  slot?: string;
  src?: string;
  fit: 'cover' | 'contain' | 'fill';
  borderRadius?: number;
  previewFile?: File | null;
  previewUrl?: string | null;
}

export interface EditorTextLayer extends BaseEditorLayer {
  kind: 'text';
  x: number;
  y: number;
  slot?: string;
  text?: string;
  fontSize: number;
  fontFamily: string;
  fontWeight: string | number;
  color: string;
  maxWidth?: number;
  lineHeight: number;
  align: 'left' | 'center' | 'right';
  prefix?: string;
  suffix?: string;
  textTransform: 'none' | 'uppercase' | 'lowercase';
}

export type EditorLayer =
  | EditorRectLayer
  | EditorCircleLayer
  | EditorImageLayer
  | EditorTextLayer;

export interface SlotDefinition {
  name: string;
  type: 'text' | 'image' | 'color';
  required: boolean;
  description?: string;
  defaultValue?: string;
}

export interface ParsedTemplate {
  name: string;
  description?: string;
  width: number;
  height: number;
  slots: SlotDefinition[];
  layers: EditorLayer[];
}

export function resolveDimension(
  value: number | string | undefined | null,
  total: number,
  fallback: number = 0
): number {
  if (value === undefined || value === null) return fallback;
  if (typeof value === 'number') return value;
  const trimmed = value.trim();
  if (trimmed.endsWith('%')) {
    const percent = parseFloat(trimmed.slice(0, -1));
    return isNaN(percent) ? fallback : Math.round((percent / 100) * total);
  }
  const parsed = parseFloat(trimmed);
  return isNaN(parsed) ? fallback : parsed;
}

let layerCounter = 0;
export function generateLayerId(): string {
  layerCounter += 1;
  return `layer_${Date.now()}_${layerCounter}`;
}

export function parseSchemaToEditorLayers(schemaJsonOrObj: string | any): ParsedTemplate {
  const schema = typeof schemaJsonOrObj === 'string' ? JSON.parse(schemaJsonOrObj) : schemaJsonOrObj;

  const width = typeof schema.width === 'number' && schema.width > 0 ? schema.width : 1080;
  const height = typeof schema.height === 'number' && schema.height > 0 ? schema.height : 1080;
  const name = schema.name || 'Untitled Template';
  const description = schema.description || undefined;

  const rawSlots: any[] = Array.isArray(schema.slots) ? schema.slots : [];
  const slots: SlotDefinition[] = rawSlots.map((s) => ({
    name: s.name || 'slot',
    type: s.type === 'image' || s.type === 'color' ? s.type : 'text',
    required: Boolean(s.required),
    description: s.description || undefined,
    defaultValue: s.defaultValue || undefined,
  }));

  const rawLayers: any[] = Array.isArray(schema.layers) ? schema.layers : [];
  const layers: EditorLayer[] = rawLayers.map((l, index) => {
    const id = l.id || `layer_${index + 1}_${Date.now()}`;
    const opacity = typeof l.opacity === 'number' ? Math.max(0, Math.min(1, l.opacity)) : 1;

    switch (l.type) {
      case 'rect':
        return {
          id,
          kind: 'rect',
          x: resolveDimension(l.x, width, 0),
          y: resolveDimension(l.y, height, 0),
          w: resolveDimension(l.w, width, width),
          h: resolveDimension(l.h, height, height),
          fill: l.fill,
          stroke: l.stroke,
          strokeWidth: l.strokeWidth,
          borderRadius: l.borderRadius,
          opacity,
        } as EditorRectLayer;

      case 'circle':
        return {
          id,
          kind: 'circle',
          cx: resolveDimension(l.cx, width, width / 2),
          cy: resolveDimension(l.cy, height, height / 2),
          radius: resolveDimension(l.radius, Math.min(width, height), 50),
          fill: l.fill,
          stroke: l.stroke,
          strokeWidth: l.strokeWidth,
          opacity,
        } as EditorCircleLayer;

      case 'image':
        return {
          id,
          kind: 'image',
          x: resolveDimension(l.x, width, 0),
          y: resolveDimension(l.y, height, 0),
          w: resolveDimension(l.w, width, width),
          h: resolveDimension(l.h, height, height),
          slot: l.slot,
          src: l.src,
          fit: l.fit === 'contain' || l.fit === 'fill' ? l.fit : 'cover',
          borderRadius: l.borderRadius,
          opacity,
        } as EditorImageLayer;

      case 'text':
      default:
        return {
          id,
          kind: 'text',
          x: resolveDimension(l.x, width, 0),
          y: resolveDimension(l.y, height, 0),
          slot: l.slot,
          text: l.text,
          fontSize: typeof l.fontSize === 'number' ? l.fontSize : 32,
          fontFamily: l.fontFamily || 'Inter, sans-serif',
          fontWeight: l.fontWeight ?? 'normal',
          color: l.color || '#ffffff',
          maxWidth: l.maxWidth !== undefined ? resolveDimension(l.maxWidth, width, width) : undefined,
          lineHeight: typeof l.lineHeight === 'number' ? l.lineHeight : 1.3,
          align: l.align === 'center' || l.align === 'right' ? l.align : 'left',
          prefix: l.prefix,
          suffix: l.suffix,
          textTransform: l.textTransform === 'uppercase' || l.textTransform === 'lowercase' ? l.textTransform : 'none',
          opacity,
        } as EditorTextLayer;
    }
  });

  return {
    name,
    description,
    width,
    height,
    slots,
    layers,
  };
}

export function serializeEditorLayersToSchema(
  layers: EditorLayer[],
  slots: SlotDefinition[],
  meta: { name: string; description?: string; width: number; height: number }
): string {
  const schemaLayers = layers.map((layer) => {
    switch (layer.kind) {
      case 'rect': {
        const out: Record<string, any> = {
          type: 'rect',
          x: layer.x,
          y: layer.y,
          w: layer.w,
          h: layer.h,
        };
        if (layer.fill !== undefined) out.fill = layer.fill;
        if (layer.stroke !== undefined) out.stroke = layer.stroke;
        if (layer.strokeWidth !== undefined && layer.strokeWidth > 0) out.strokeWidth = layer.strokeWidth;
        if (layer.borderRadius !== undefined && layer.borderRadius > 0) out.borderRadius = layer.borderRadius;
        if (layer.opacity !== undefined && layer.opacity < 1) out.opacity = layer.opacity;
        return out;
      }
      case 'circle': {
        const out: Record<string, any> = {
          type: 'circle',
          cx: layer.cx,
          cy: layer.cy,
          radius: layer.radius,
        };
        if (layer.fill !== undefined) out.fill = layer.fill;
        if (layer.stroke !== undefined) out.stroke = layer.stroke;
        if (layer.strokeWidth !== undefined && layer.strokeWidth > 0) out.strokeWidth = layer.strokeWidth;
        if (layer.opacity !== undefined && layer.opacity < 1) out.opacity = layer.opacity;
        return out;
      }
      case 'image': {
        const out: Record<string, any> = {
          type: 'image',
          x: layer.x,
          y: layer.y,
          w: layer.w,
          h: layer.h,
          fit: layer.fit || 'cover',
        };
        if (layer.slot) out.slot = layer.slot;
        if (layer.src) out.src = layer.src;
        if (layer.borderRadius !== undefined && layer.borderRadius > 0) out.borderRadius = layer.borderRadius;
        if (layer.opacity !== undefined && layer.opacity < 1) out.opacity = layer.opacity;
        return out;
      }
      case 'text': {
        const out: Record<string, any> = {
          type: 'text',
          x: layer.x,
          y: layer.y,
          fontSize: layer.fontSize,
          fontFamily: layer.fontFamily,
          fontWeight: layer.fontWeight,
          color: layer.color,
        };
        if (layer.slot) out.slot = layer.slot;
        if (layer.text) out.text = layer.text;
        if (layer.maxWidth !== undefined) out.maxWidth = layer.maxWidth;
        if (layer.lineHeight !== undefined && layer.lineHeight !== 1.3) out.lineHeight = layer.lineHeight;
        if (layer.align && layer.align !== 'left') out.align = layer.align;
        if (layer.prefix) out.prefix = layer.prefix;
        if (layer.suffix) out.suffix = layer.suffix;
        if (layer.textTransform && layer.textTransform !== 'none') out.textTransform = layer.textTransform;
        if (layer.opacity !== undefined && layer.opacity < 1) out.opacity = layer.opacity;
        return out;
      }
    }
  });

  const schemaSlots = slots.map((s) => {
    const slotObj: Record<string, any> = {
      name: s.name,
      type: s.type,
      required: s.required,
    };
    if (s.description) slotObj.description = s.description;
    if (s.defaultValue) slotObj.defaultValue = s.defaultValue;
    return slotObj;
  });

  const schema: Record<string, any> = {
    name: meta.name || 'Untitled Template',
    width: meta.width || 1080,
    height: meta.height || 1080,
    slots: schemaSlots,
    layers: schemaLayers,
  };

  if (meta.description) {
    schema.description = meta.description;
  }

  return JSON.stringify(schema, null, 2);
}
