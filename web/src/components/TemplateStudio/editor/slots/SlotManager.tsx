import { useState } from 'react';
import { useTemplateEditorStore } from '../templateEditorStore';
import { SlotDefinition } from '../schemaCodec';

export function SlotManager() {
  const { slots, addSlot, updateSlot, removeSlot } = useTemplateEditorStore();
  const [newSlotName, setNewSlotName] = useState('');
  const [newSlotType, setNewSlotType] = useState<'text' | 'image' | 'color'>('text');

  const handleAddSlot = (e: React.FormEvent) => {
    e.preventDefault();
    const trimmed = newSlotName.trim();
    if (!trimmed) return;
    if (slots.some((s) => s.name === trimmed)) {
      alert(`A slot named "${trimmed}" already exists.`);
      return;
    }

    addSlot({
      name: trimmed,
      type: newSlotType,
      required: true,
      defaultValue: newSlotType === 'text' ? 'Sample text' : undefined,
    });
    setNewSlotName('');
  };

  return (
    <div className="space-y-4">
      <div>
        <h3 className="text-xs font-mono uppercase tracking-wider text-muted font-semibold mb-1">
          Dynamic Template Slots
        </h3>
        <p className="text-xs text-muted leading-relaxed">
          Slots define the inputs the AI and user supply when rendering this graphic card.
        </p>
      </div>

      {/* Add Slot Form */}
      <form onSubmit={handleAddSlot} className="flex gap-2 items-center">
        <input
          type="text"
          placeholder="New slot name (e.g. quote_text)..."
          value={newSlotName}
          onChange={(e) => setNewSlotName(e.target.value)}
          className="flex-1 bg-bg border border-border rounded px-2.5 py-1.5 text-xs text-foreground placeholder:text-muted/60 focus:outline-none focus:border-accent"
        />
        <select
          value={newSlotType}
          onChange={(e) => setNewSlotType(e.target.value as any)}
          className="bg-bg border border-border rounded px-2 py-1.5 text-xs text-foreground focus:outline-none focus:border-accent"
        >
          <option value="text">Text</option>
          <option value="image">Image</option>
          <option value="color">Color</option>
        </select>
        <button
          type="submit"
          disabled={!newSlotName.trim()}
          className="px-3 py-1.5 bg-accent text-accent-foreground text-xs font-mono rounded font-medium disabled:opacity-40"
        >
          + Add
        </button>
      </form>

      {/* Slots List */}
      <div className="space-y-2.5">
        {slots.length === 0 ? (
          <div className="text-xs font-mono text-muted text-center py-6 border border-dashed border-border rounded">
            No slots defined yet.
          </div>
        ) : (
          slots.map((slot, idx) => (
            <div
              key={slot.name}
              className="bg-bg border border-border rounded-lg p-3 space-y-2.5"
            >
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-mono font-semibold text-foreground">
                    {slot.name}
                  </span>
                  <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-muted/20 text-muted uppercase">
                    {slot.type}
                  </span>
                </div>
                <button
                  type="button"
                  onClick={() => removeSlot(idx)}
                  className="text-muted/60 hover:text-destructive text-xs font-mono"
                  title="Remove slot"
                >
                  ✕
                </button>
              </div>

              <div className="grid grid-cols-2 gap-2 text-xs">
                <div>
                  <label className="text-[10px] font-mono text-muted block mb-0.5">Type</label>
                  <select
                    value={slot.type}
                    onChange={(e) => updateSlot(idx, { type: e.target.value as any })}
                    className="w-full bg-panel border border-border rounded px-2 py-1 text-xs text-foreground focus:outline-none focus:border-accent"
                  >
                    <option value="text">Text</option>
                    <option value="image">Image</option>
                    <option value="color">Color</option>
                  </select>
                </div>

                <div className="flex items-end pb-1">
                  <label className="flex items-center gap-1.5 cursor-pointer text-xs text-muted">
                    <input
                      type="checkbox"
                      checked={slot.required}
                      onChange={(e) => updateSlot(idx, { required: e.target.checked })}
                      className="rounded border-border text-accent focus:ring-0"
                    />
                    <span>Required</span>
                  </label>
                </div>
              </div>

              <div>
                <label className="text-[10px] font-mono text-muted block mb-0.5">Description</label>
                <input
                  type="text"
                  placeholder="Optional hint for AI / composer..."
                  value={slot.description || ''}
                  onChange={(e) => updateSlot(idx, { description: e.target.value || undefined })}
                  className="w-full bg-panel border border-border rounded px-2 py-1 text-xs text-foreground placeholder:text-muted/60 focus:outline-none focus:border-accent"
                />
              </div>

              {slot.type === 'text' && (
                <div>
                  <label className="text-[10px] font-mono text-muted block mb-0.5">Default Value</label>
                  <input
                    type="text"
                    placeholder="Default sample text..."
                    value={slot.defaultValue || ''}
                    onChange={(e) => updateSlot(idx, { defaultValue: e.target.value || undefined })}
                    className="w-full bg-panel border border-border rounded px-2 py-1 text-xs text-foreground placeholder:text-muted/60 focus:outline-none focus:border-accent"
                  />
                </div>
              )}
            </div>
          ))
        )}
      </div>
    </div>
  );
}
