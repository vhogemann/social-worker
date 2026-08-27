import { describe, it, expect, beforeEach } from 'vitest';
import { useTemplateEditorStore } from './templateEditorStore';
import { EditorImageLayer } from './schemaCodec';

describe('templateEditorStore', () => {
  beforeEach(() => {
    useTemplateEditorStore.getState().reset();
  });

  it('initializes with default template state', () => {
    const state = useTemplateEditorStore.getState();
    expect(state.name).toBe('New Template');
    expect(state.width).toBe(1080);
    expect(state.height).toBe(1080);
    expect(state.layers.length).toBeGreaterThan(0);
    expect(state.slots.length).toBeGreaterThan(0);
    expect(state.isDirty).toBe(false);
  });

  it('updates metadata and marks dirty', () => {
    useTemplateEditorStore.getState().setMeta({ name: 'Custom Name', width: 800 });
    const state = useTemplateEditorStore.getState();
    expect(state.name).toBe('Custom Name');
    expect(state.width).toBe(800);
    expect(state.isDirty).toBe(true);
  });

  it('adds layers of different kinds', () => {
    const store = useTemplateEditorStore.getState();
    const initialCount = store.layers.length;

    store.addLayer('rect');
    expect(useTemplateEditorStore.getState().layers.length).toBe(initialCount + 1);
    expect(useTemplateEditorStore.getState().layers.at(-1)?.kind).toBe('rect');

    store.addLayer('circle');
    expect(useTemplateEditorStore.getState().layers.at(-1)?.kind).toBe('circle');

    store.addLayer('image');
    expect(useTemplateEditorStore.getState().layers.at(-1)?.kind).toBe('image');
  });

  it('updates and deletes a layer', () => {
    const store = useTemplateEditorStore.getState();
    const layerId = store.layers[0].id;

    store.updateLayer(layerId, { opacity: 0.5 });
    expect(useTemplateEditorStore.getState().layers[0].opacity).toBe(0.5);

    store.deleteLayer(layerId);
    expect(useTemplateEditorStore.getState().layers.some((l) => l.id === layerId)).toBe(false);
  });

  it('duplicates a layer', () => {
    const store = useTemplateEditorStore.getState();
    const layerId = store.layers[0].id;
    const initialLength = store.layers.length;

    store.duplicateLayer(layerId);
    const state = useTemplateEditorStore.getState();
    expect(state.layers.length).toBe(initialLength + 1);
    expect(state.selectedId).not.toBe(layerId);
  });

  it('reorders and moves layers up and down', () => {
    const store = useTemplateEditorStore.getState();
    const id0 = store.layers[0].id;
    const id1 = store.layers[1].id;

    store.moveLayerUp(id0);
    let state = useTemplateEditorStore.getState();
    expect(state.layers[0].id).toBe(id1);
    expect(state.layers[1].id).toBe(id0);

    store.moveLayerDown(id0);
    state = useTemplateEditorStore.getState();
    expect(state.layers[0].id).toBe(id0);
    expect(state.layers[1].id).toBe(id1);
  });

  it('manages slots correctly', () => {
    const store = useTemplateEditorStore.getState();
    store.addSlot({ name: 'author_name', type: 'text', required: false, defaultValue: 'Jane Doe' });
    let state = useTemplateEditorStore.getState();
    expect(state.slots.some((s) => s.name === 'author_name')).toBe(true);

    const index = state.slots.findIndex((s) => s.name === 'author_name');
    store.updateSlot(index, { defaultValue: 'John Doe' });
    state = useTemplateEditorStore.getState();
    expect(state.slots[index].defaultValue).toBe('John Doe');

    store.removeSlot(index);
    state = useTemplateEditorStore.getState();
    expect(state.slots.some((s) => s.name === 'author_name')).toBe(false);
  });

  it('sets and clears fixed image assets on image layers', () => {
    const store = useTemplateEditorStore.getState();
    store.addLayer('image');
    const imageLayerId = useTemplateEditorStore.getState().layers.at(-1)!.id;

    store.setFixedImageAsset(imageLayerId, '/api/media/12345');
    let state = useTemplateEditorStore.getState();
    let img = state.layers.find((l) => l.id === imageLayerId) as EditorImageLayer;
    expect(img?.src).toBe('/api/media/12345');
    expect(img?.slot).toBeUndefined();

    store.clearImageSlot(imageLayerId);
    state = useTemplateEditorStore.getState();
    img = state.layers.find((l) => l.id === imageLayerId) as EditorImageLayer;
    expect(img?.src).toBeUndefined();
    expect(img?.previewUrl).toBeNull();
  });

  it('serializes to JSON schema correctly', () => {
    const store = useTemplateEditorStore.getState();
    const json = store.toSchemaJson();
    const parsed = JSON.parse(json);
    expect(parsed.name).toBe('New Template');
    expect(parsed.layers.length).toBe(store.layers.length);
    expect(parsed.slots.length).toBe(store.slots.length);
  });
});
