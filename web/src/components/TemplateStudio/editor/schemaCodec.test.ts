import { describe, it, expect } from 'vitest';
import {
  parseSchemaToEditorLayers,
  serializeEditorLayersToSchema,
  resolveDimension,
} from './schemaCodec';

describe('schemaCodec', () => {
  describe('resolveDimension', () => {
    it('resolves raw numbers', () => {
      expect(resolveDimension(100, 1000)).toBe(100);
      expect(resolveDimension(0, 1000)).toBe(0);
    });

    it('resolves percentage strings', () => {
      expect(resolveDimension('100%', 1080)).toBe(1080);
      expect(resolveDimension('50%', 1080)).toBe(540);
      expect(resolveDimension(' 25% ', 800)).toBe(200);
    });

    it('resolves string numbers', () => {
      expect(resolveDimension('120', 1000)).toBe(120);
    });

    it('falls back on null / undefined', () => {
      expect(resolveDimension(undefined, 1000, 42)).toBe(42);
      expect(resolveDimension(null, 1000, 50)).toBe(50);
    });
  });

  describe('parseSchemaToEditorLayers & serializeEditorLayersToSchema', () => {
    it('parses basic schema with rect and text layers', () => {
      const input = {
        name: 'Quote Card',
        description: 'A test quote',
        width: 1080,
        height: 1080,
        slots: [
          { name: 'quote_text', type: 'text', required: true, description: 'Quote' },
          { name: 'bg', type: 'image', required: false },
        ],
        layers: [
          { type: 'rect', fill: '#0f172a', x: 0, y: 0, w: '100%', h: '100%' },
          {
            type: 'text',
            slot: 'quote_text',
            x: 100,
            y: 200,
            fontSize: 48,
            fontFamily: 'Inter, sans-serif',
            fontWeight: 'bold',
            color: '#ffffff',
            maxWidth: 880,
            align: 'left',
          },
        ],
      };

      const parsed = parseSchemaToEditorLayers(JSON.stringify(input));
      expect(parsed.name).toBe('Quote Card');
      expect(parsed.description).toBe('A test quote');
      expect(parsed.width).toBe(1080);
      expect(parsed.height).toBe(1080);
      expect(parsed.slots).toHaveLength(2);
      expect(parsed.slots[0].name).toBe('quote_text');
      expect(parsed.slots[1].type).toBe('image');
      expect(parsed.layers).toHaveLength(2);

      const rect = parsed.layers[0];
      expect(rect.kind).toBe('rect');
      if (rect.kind === 'rect') {
        expect(rect.fill).toBe('#0f172a');
        expect(rect.w).toBe(1080);
        expect(rect.h).toBe(1080);
      }

      const text = parsed.layers[1];
      expect(text.kind).toBe('text');
      if (text.kind === 'text') {
        expect(text.slot).toBe('quote_text');
        expect(text.fontSize).toBe(48);
        expect(text.maxWidth).toBe(880);
      }

      const serializedJson = serializeEditorLayersToSchema(parsed.layers, parsed.slots, {
        name: parsed.name,
        description: parsed.description,
        width: parsed.width,
        height: parsed.height,
      });

      const reparsed = JSON.parse(serializedJson);
      expect(reparsed.name).toBe('Quote Card');
      expect(reparsed.width).toBe(1080);
      expect(reparsed.height).toBe(1080);
      expect(reparsed.layers).toHaveLength(2);
      expect(reparsed.layers[0].type).toBe('rect');
      expect(reparsed.layers[1].type).toBe('text');
    });

    it('round-trips circle and image layers properly', () => {
      const input = {
        name: 'Circle & Image Template',
        width: 800,
        height: 800,
        slots: [{ name: 'avatar', type: 'image', required: true }],
        layers: [
          { type: 'circle', cx: 400, cy: 400, radius: 100, fill: '#ff0000', stroke: '#000000', strokeWidth: 2 },
          { type: 'image', slot: 'avatar', x: 200, y: 200, w: 400, h: 400, fit: 'contain', borderRadius: 16 },
        ],
      };

      const parsed = parseSchemaToEditorLayers(input);
      expect(parsed.layers).toHaveLength(2);
      expect(parsed.layers[0].kind).toBe('circle');
      expect(parsed.layers[1].kind).toBe('image');

      const serialized = serializeEditorLayersToSchema(parsed.layers, parsed.slots, {
        name: parsed.name,
        width: parsed.width,
        height: parsed.height,
      });

      const re = JSON.parse(serialized);
      expect(re.layers[0].type).toBe('circle');
      expect(re.layers[0].radius).toBe(100);
      expect(re.layers[1].type).toBe('image');
      expect(re.layers[1].fit).toBe('contain');
      expect(re.layers[1].borderRadius).toBe(16);
    });
  });
});
