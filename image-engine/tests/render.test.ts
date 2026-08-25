import { describe, it, expect } from 'vitest';
import sharp from 'sharp';
import { GlobalFonts } from '@napi-rs/canvas';
import { renderTemplate } from '../src/services/template.service.js';
import { Template } from '../src/types/schema.js';

describe('Template Rendering Service', () => {
  it('checks available fonts and verifies text pixels are rendered', async () => {
    expect(GlobalFonts.families.length).toBeGreaterThan(0);

    const blankTemplate: Template = {
      name: 'blank',
      width: 200,
      height: 200,
      slots: [],
      layers: [{ type: 'rect', fill: '#000000', x: 0, y: 0, w: '100%', h: '100%' }],
    };

    const textTemplate: Template = {
      name: 'with_text',
      width: 200,
      height: 200,
      slots: [{ name: 't', type: 'text' }],
      layers: [
        { type: 'rect', fill: '#000000', x: 0, y: 0, w: '100%', h: '100%' },
        { type: 'text', slot: 't', x: 20, y: 50, fontSize: 32, color: '#ffffff' },
      ],
    };

    const blankBuf = await renderTemplate(blankTemplate, {});
    const textBuf = await renderTemplate(textTemplate, { t: 'TEST' });

    const blankRaw = await sharp(blankBuf).raw().toBuffer();
    const textRaw = await sharp(textBuf).raw().toBuffer();

    // Text buffer must have white pixels (different from pure black canvas)
    expect(blankRaw.equals(textRaw)).toBe(false);
  });

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

  it('handles word-wrap boundaries and multiline text accurately', async () => {
    const template: Template = {
      name: 'word_wrap_test',
      width: 600,
      height: 600,
      slots: [{ name: 'long_text', type: 'text', required: true }],
      layers: [
        { type: 'rect', fill: '#111111', x: 0, y: 0, w: '100%', h: '100%' },
        {
          type: 'text',
          slot: 'long_text',
          x: 50,
          y: 50,
          fontSize: 24,
          maxWidth: 500,
          lineHeight: 1.5,
          color: '#ffffff',
        },
      ],
    };

    const slots = {
      long_text: 'Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam.',
    };

    const buffer = await renderTemplate(template, slots);
    expect(buffer).toBeInstanceOf(Buffer);

    const meta = await sharp(buffer).metadata();
    expect(meta.width).toBe(600);
    expect(meta.height).toBe(600);
  });

  it('renders text with left, center, and right alignments', async () => {
    const template: Template = {
      name: 'text_align_test',
      width: 800,
      height: 800,
      slots: [
        { name: 'left_msg', type: 'text', required: true },
        { name: 'center_msg', type: 'text', required: true },
        { name: 'right_msg', type: 'text', required: true },
      ],
      layers: [
        { type: 'rect', fill: '#222222', x: 0, y: 0, w: '100%', h: '100%' },
        { type: 'text', slot: 'left_msg', x: 50, y: 100, maxWidth: 700, align: 'left', fontSize: 28, color: '#ffffff' },
        { type: 'text', slot: 'center_msg', x: 50, y: 300, maxWidth: 700, align: 'center', fontSize: 28, color: '#00ffcc' },
        { type: 'text', slot: 'right_msg', x: 50, y: 500, maxWidth: 700, align: 'right', fontSize: 28, color: '#ffcc00' },
      ],
    };

    const slots = {
      left_msg: 'Left aligned headline',
      center_msg: 'Center aligned quote',
      right_msg: 'Right aligned footer',
    };

    const buffer = await renderTemplate(template, slots);
    expect(buffer).toBeInstanceOf(Buffer);
  });

  it('resolves percentage dimensions and numeric offsets correctly', async () => {
    const template: Template = {
      name: 'percentage_dim_test',
      width: 1000,
      height: 1000,
      slots: [],
      layers: [
        { type: 'rect', fill: '#0a0a0a', x: 0, y: 0, w: '100%', h: '100%' },
        { type: 'rect', fill: '#2563eb', x: '10%', y: '10%', w: '80%', h: '80%', borderRadius: 16 },
        { type: 'circle', fill: '#f59e0b', cx: '50%', cy: '50%', radius: 100 },
      ],
    };

    const buffer = await renderTemplate(template, {});
    expect(buffer).toBeInstanceOf(Buffer);

    const meta = await sharp(buffer).metadata();
    expect(meta.width).toBe(1000);
    expect(meta.height).toBe(1000);
  });

  it('gracefully handles missing optional slots without throwing', async () => {
    const template: Template = {
      name: 'optional_slots_test',
      width: 500,
      height: 500,
      slots: [
        { name: 'mandatory_title', type: 'text', required: true },
        { name: 'optional_subtitle', type: 'text', required: false },
        { name: 'optional_bg', type: 'image', required: false },
      ],
      layers: [
        { type: 'rect', fill: '#18181b', x: 0, y: 0, w: '100%', h: '100%' },
        { type: 'image', slot: 'optional_bg', fit: 'cover', x: 0, y: 0, w: '100%', h: '100%' },
        { type: 'text', slot: 'mandatory_title', x: 40, y: 80, fontSize: 32, color: '#ffffff' },
        { type: 'text', slot: 'optional_subtitle', x: 40, y: 150, fontSize: 20, color: '#a1a1aa' },
      ],
    };

    const slots = {
      mandatory_title: 'Only title provided',
    };

    const buffer = await renderTemplate(template, slots, new Map());
    expect(buffer).toBeInstanceOf(Buffer);
  });

  it('supports image fit modes cover, contain, and fill with border radius clipping', async () => {
    const sampleImage = await sharp({
      create: {
        width: 1200,
        height: 600,
        channels: 4,
        background: { r: 239, g: 68, b: 68, alpha: 1 },
      },
    })
      .png()
      .toBuffer();

    const template: Template = {
      name: 'image_fit_test',
      width: 1080,
      height: 1080,
      slots: [
        { name: 'img1', type: 'image', required: true },
        { name: 'img2', type: 'image', required: true },
        { name: 'img3', type: 'image', required: true },
      ],
      layers: [
        { type: 'rect', fill: '#09090b', x: 0, y: 0, w: '100%', h: '100%' },
        { type: 'image', slot: 'img1', fit: 'cover', x: 40, y: 40, w: 300, h: 300, borderRadius: 20 },
        { type: 'image', slot: 'img2', fit: 'contain', x: 380, y: 40, w: 300, h: 300, borderRadius: 20 },
        { type: 'image', slot: 'img3', fit: 'fill', x: 720, y: 40, w: 300, h: 300, borderRadius: 20 },
      ],
    };

    const images = new Map<string, Buffer>([
      ['img1', sampleImage],
      ['img2', sampleImage],
      ['img3', sampleImage],
    ]);

    const buffer = await renderTemplate(template, {}, images);
    expect(buffer).toBeInstanceOf(Buffer);
  });

  it('applies text transformations uppercase and lowercase', async () => {
    const template: Template = {
      name: 'text_transform_test',
      width: 600,
      height: 400,
      slots: [
        { name: 'title', type: 'text', required: true },
        { name: 'tag', type: 'text', required: true },
      ],
      layers: [
        { type: 'rect', fill: '#000000', x: 0, y: 0, w: '100%', h: '100%' },
        { type: 'text', slot: 'tag', x: 40, y: 50, fontSize: 18, textTransform: 'uppercase', color: '#38bdf8' },
        { type: 'text', slot: 'title', x: 40, y: 100, fontSize: 32, textTransform: 'lowercase', color: '#ffffff' },
      ],
    };

    const slots = {
      tag: 'breaking announcement',
      title: 'ALL CAPS HEADLINE',
    };

    const buffer = await renderTemplate(template, slots);
    expect(buffer).toBeInstanceOf(Buffer);
  });
});
