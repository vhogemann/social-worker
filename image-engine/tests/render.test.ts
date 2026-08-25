import { describe, it, expect } from 'vitest';
import sharp from 'sharp';
import { renderTemplate } from '../src/services/template.service.js';
import { Template } from '../src/types/schema.js';

describe('Template Renderer Service', () => {
  it('renders a declarative template with background, rect, and wrapped text', async () => {
    const bgImage = await sharp({
      create: {
        width: 800,
        height: 800,
        channels: 4,
        background: { r: 50, g: 100, b: 150, alpha: 1 },
      },
    })
      .png()
      .toBuffer();

    const template: Template = {
      name: 'quote_card',
      width: 1080,
      height: 1080,
      slots: [
        { name: 'bg', type: 'image', required: true },
        { name: 'quote', type: 'text', required: true },
        { name: 'author', type: 'text', required: false },
      ],
      layers: [
        { type: 'image', slot: 'bg', fit: 'cover', x: 0, y: 0, w: '100%', h: '100%' },
        { type: 'rect', fill: 'rgba(0, 0, 0, 0.4)', x: 0, y: 0, w: '100%', h: '100%' },
        {
          type: 'text',
          slot: 'quote',
          x: 100,
          y: 400,
          fontSize: 48,
          fontFamily: 'sans-serif',
          color: '#ffffff',
          maxWidth: 880,
          lineHeight: 1.4,
          align: 'left',
        },
        {
          type: 'text',
          slot: 'author',
          x: 100,
          y: 800,
          fontSize: 32,
          fontFamily: 'sans-serif',
          color: '#ffd700',
          prefix: '— ',
        },
        {
          type: 'circle',
          fill: '#ff0055',
          cx: 1000,
          cy: 80,
          radius: 30,
        },
      ],
    };

    const images = new Map<string, Buffer>([['bg', bgImage]]);
    const slots = {
      quote: 'Simplicity is prerequisite for reliability. This is a longer text that will be wrapped properly.',
      author: 'Edsger W. Dijkstra',
    };

    const renderedBuffer = await renderTemplate(template, slots, images);

    expect(renderedBuffer).toBeInstanceOf(Buffer);
    const meta = await sharp(renderedBuffer).metadata();
    expect(meta.format).toBe('png');
    expect(meta.width).toBe(1080);
    expect(meta.height).toBe(1080);
  });
});
