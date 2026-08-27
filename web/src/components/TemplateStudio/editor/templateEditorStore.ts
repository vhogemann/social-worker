import { create } from 'zustand';
import {
  EditorLayer,
  EditorRectLayer,
  EditorCircleLayer,
  EditorImageLayer,
  EditorTextLayer,
  LayerKind,
  SlotDefinition,
  parseSchemaToEditorLayers,
  serializeEditorLayersToSchema,
  generateLayerId,
} from './schemaCodec';

export interface TemplateEditorState {
  templateId: string | null;
  name: string;
  description: string;
  width: number;
  height: number;
  slots: SlotDefinition[];
  layers: EditorLayer[];
  selectedId: string | null;
  isDirty: boolean;
  activeTab: 'canvas' | 'source';

  // Metadata actions
  setMeta: (patch: Partial<Pick<TemplateEditorState, 'name' | 'description' | 'width' | 'height'>>) => void;
  setActiveTab: (tab: 'canvas' | 'source') => void;

  // Slot actions
  addSlot: (slot: SlotDefinition) => void;
  updateSlot: (index: number, patch: Partial<SlotDefinition>) => void;
  removeSlot: (index: number) => void;

  // Layer actions
  addLayer: (kind: LayerKind) => void;
  updateLayer: (id: string, patch: Partial<EditorLayer>) => void;
  deleteLayer: (id: string) => void;
  duplicateLayer: (id: string) => void;
  moveLayerUp: (id: string) => void;
  moveLayerDown: (id: string) => void;
  reorderLayers: (fromIndex: number, toIndex: number) => void;
  setSelected: (id: string | null) => void;

  // Image slot file actions
  setImageSlotFile: (layerId: string, file: File) => void;
  setImageSlotUrl: (layerId: string, url: string) => void;
  setFixedImageAsset: (layerId: string, url: string) => void;
  clearImageSlot: (layerId: string) => void;

  // Loading & Serialization
  loadFromSchema: (schemaJson: string, templateId?: string | null) => void;
  reset: (templateId?: string | null) => void;
  toSchemaJson: () => string;
  getImageFiles: () => Record<string, File>;
}

const DEFAULT_WIDTH = 1080;
const DEFAULT_HEIGHT = 1080;

export const useTemplateEditorStore = create<TemplateEditorState>((set, get) => ({
  templateId: null,
  name: 'New Template',
  description: '',
  width: DEFAULT_WIDTH,
  height: DEFAULT_HEIGHT,
  slots: [
    { name: 'headline', type: 'text', required: true, defaultValue: 'Your Headline Here' },
    { name: 'body_text', type: 'text', required: false, defaultValue: 'Supporting description or takeaway text.' },
  ],
  layers: [
    {
      id: 'bg_rect',
      kind: 'rect',
      x: 0,
      y: 0,
      w: DEFAULT_WIDTH,
      h: DEFAULT_HEIGHT,
      fill: '#0f172a',
      opacity: 1,
    } as EditorRectLayer,
    {
      id: 'text_headline',
      kind: 'text',
      slot: 'headline',
      text: 'Your Headline Here',
      x: 100,
      y: 200,
      maxWidth: 880,
      fontSize: 56,
      fontFamily: 'Inter, sans-serif',
      fontWeight: 'bold',
      color: '#ffffff',
      lineHeight: 1.3,
      align: 'left',
      textTransform: 'none',
      opacity: 1,
    } as EditorTextLayer,
  ],
  selectedId: null,
  isDirty: false,
  activeTab: 'canvas',

  setMeta: (patch) =>
    set((state) => ({
      ...state,
      ...patch,
      isDirty: true,
    })),

  setActiveTab: (tab) =>
    set({ activeTab: tab }),

  addSlot: (slot) =>
    set((state) => ({
      slots: [...state.slots, slot],
      isDirty: true,
    })),

  updateSlot: (index, patch) =>
    set((state) => {
      const next = [...state.slots];
      if (index >= 0 && index < next.length) {
        next[index] = { ...next[index], ...patch };
      }
      return { slots: next, isDirty: true };
    }),

  removeSlot: (index) =>
    set((state) => ({
      slots: state.slots.filter((_, i) => i !== index),
      isDirty: true,
    })),

  addLayer: (kind) => {
    const id = generateLayerId();
    const { width, height } = get();

    let newLayer: EditorLayer;

    switch (kind) {
      case 'rect':
        newLayer = {
          id,
          kind: 'rect',
          x: Math.round(width * 0.1),
          y: Math.round(height * 0.1),
          w: Math.round(width * 0.4),
          h: Math.round(height * 0.3),
          fill: '#38bdf8',
          opacity: 1,
          borderRadius: 8,
        };
        break;

      case 'circle':
        newLayer = {
          id,
          kind: 'circle',
          cx: Math.round(width / 2),
          cy: Math.round(height / 2),
          radius: Math.round(Math.min(width, height) * 0.15),
          fill: '#ec4899',
          opacity: 1,
        };
        break;

      case 'image':
        newLayer = {
          id,
          kind: 'image',
          x: Math.round(width * 0.1),
          y: Math.round(height * 0.1),
          w: Math.round(width * 0.8),
          h: Math.round(height * 0.5),
          fit: 'cover',
          opacity: 1,
          borderRadius: 12,
        };
        break;

      case 'text':
      default:
        newLayer = {
          id,
          kind: 'text',
          text: 'New Text',
          x: Math.round(width * 0.1),
          y: Math.round(height * 0.4),
          fontSize: 40,
          fontFamily: 'Inter, sans-serif',
          fontWeight: 'normal',
          color: '#f8fafc',
          lineHeight: 1.3,
          align: 'left',
          textTransform: 'none',
          opacity: 1,
        };
        break;
    }

    set((state) => ({
      layers: [...state.layers, newLayer],
      selectedId: id,
      isDirty: true,
    }));
  },

  updateLayer: (id, patch) =>
    set((state) => ({
      layers: state.layers.map((l) => (l.id === id ? ({ ...l, ...patch } as EditorLayer) : l)),
      isDirty: true,
    })),

  deleteLayer: (id) =>
    set((state) => ({
      layers: state.layers.filter((l) => l.id !== id),
      selectedId: state.selectedId === id ? null : state.selectedId,
      isDirty: true,
    })),

  duplicateLayer: (id) => {
    const layer = get().layers.find((l) => l.id === id);
    if (!layer) return;

    const newId = generateLayerId();
    let cloned: EditorLayer;

    if (layer.kind === 'circle') {
      cloned = { ...layer, id: newId, cx: layer.cx + 20, cy: layer.cy + 20 };
    } else {
      cloned = { ...layer, id: newId, x: layer.x + 20, y: layer.y + 20 };
    }

    set((state) => ({
      layers: [...state.layers, cloned],
      selectedId: newId,
      isDirty: true,
    }));
  },

  moveLayerUp: (id) =>
    set((state) => {
      const idx = state.layers.findIndex((l) => l.id === id);
      if (idx < 0 || idx === state.layers.length - 1) return state;
      const next = [...state.layers];
      const [item] = next.splice(idx, 1);
      next.splice(idx + 1, 0, item);
      return { layers: next, isDirty: true };
    }),

  moveLayerDown: (id) =>
    set((state) => {
      const idx = state.layers.findIndex((l) => l.id === id);
      if (idx <= 0) return state;
      const next = [...state.layers];
      const [item] = next.splice(idx, 1);
      next.splice(idx - 1, 0, item);
      return { layers: next, isDirty: true };
    }),

  reorderLayers: (fromIndex, toIndex) =>
    set((state) => {
      if (
        fromIndex < 0 ||
        fromIndex >= state.layers.length ||
        toIndex < 0 ||
        toIndex >= state.layers.length ||
        fromIndex === toIndex
      ) {
        return state;
      }
      const next = [...state.layers];
      const [item] = next.splice(fromIndex, 1);
      next.splice(toIndex, 0, item);
      return { layers: next, isDirty: true };
    }),

  setSelected: (id) =>
    set({ selectedId: id }),

  setImageSlotFile: (layerId, file) =>
    set((state) => {
      const previewUrl = URL.createObjectURL(file);
      return {
        layers: state.layers.map((l) =>
          l.id === layerId && l.kind === 'image'
            ? { ...l, previewFile: file, previewUrl }
            : l
        ),
        isDirty: true,
      };
    }),

  setImageSlotUrl: (layerId, url) =>
    set((state) => ({
      layers: state.layers.map((l) =>
        l.id === layerId && l.kind === 'image'
          ? { ...l, previewFile: null, previewUrl: url, src: url }
          : l
      ),
      isDirty: true,
    })),

  setFixedImageAsset: (layerId, url) =>
    set((state) => ({
      layers: state.layers.map((l) =>
        l.id === layerId && l.kind === 'image'
          ? { ...l, previewFile: null, previewUrl: url, src: url, slot: undefined }
          : l
      ),
      isDirty: true,
    })),

  clearImageSlot: (layerId) =>
    set((state) => ({
      layers: state.layers.map((l) =>
        l.id === layerId && l.kind === 'image'
          ? { ...l, previewFile: null, previewUrl: null, src: undefined }
          : l
      ),
      isDirty: true,
    })),

  loadFromSchema: (schemaJson, templateId = null) => {
    try {
      const parsed = parseSchemaToEditorLayers(schemaJson);
      set({
        templateId,
        name: parsed.name,
        description: parsed.description || '',
        width: parsed.width,
        height: parsed.height,
        slots: parsed.slots,
        layers: parsed.layers,
        selectedId: parsed.layers[0]?.id || null,
        isDirty: false,
      });
    } catch {
      // invalid schema, ignore or handle upstream
    }
  },

  reset: (templateId = null) =>
    set({
      templateId,
      name: 'New Template',
      description: '',
      width: DEFAULT_WIDTH,
      height: DEFAULT_HEIGHT,
      slots: [
        { name: 'headline', type: 'text', required: true, defaultValue: 'Your Headline Here' },
        { name: 'body_text', type: 'text', required: false, defaultValue: 'Supporting description or takeaway text.' },
      ],
      layers: [
        {
          id: 'bg_rect',
          kind: 'rect',
          x: 0,
          y: 0,
          w: DEFAULT_WIDTH,
          h: DEFAULT_HEIGHT,
          fill: '#0f172a',
          opacity: 1,
        } as EditorRectLayer,
        {
          id: 'text_headline',
          kind: 'text',
          slot: 'headline',
          text: 'Your Headline Here',
          x: 100,
          y: 200,
          maxWidth: 880,
          fontSize: 56,
          fontFamily: 'Inter, sans-serif',
          fontWeight: 'bold',
          color: '#ffffff',
          lineHeight: 1.3,
          align: 'left',
          textTransform: 'none',
          opacity: 1,
        } as EditorTextLayer,
      ],
      selectedId: null,
      isDirty: false,
      activeTab: 'canvas',
    }),

  toSchemaJson: () => {
    const { layers, slots, name, description, width, height } = get();
    return serializeEditorLayersToSchema(layers, slots, {
      name,
      description: description || undefined,
      width,
      height,
    });
  },

  getImageFiles: () => {
    const { layers } = get();
    const files: Record<string, File> = {};
    for (const l of layers) {
      if (l.kind === 'image' && l.previewFile) {
        const key = l.slot || l.id;
        files[key] = l.previewFile;
      }
    }
    return files;
  },
}));
