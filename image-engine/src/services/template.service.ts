import { createCanvas, loadImage, SKRSContext2D } from '@napi-rs/canvas';
import { Template, Layer, TextLayer, RectLayer, CircleLayer, ImageLayer } from '../types/schema.js';

export async function renderTemplate(
  template: Template,
  slots: Record<string, string> = {},
  images: Map<string, Buffer> = new Map()
): Promise<Buffer> {
  const width = template.width || 1080;
  const height = template.height || 1080;

  const canvas = createCanvas(width, height);
  const ctx = canvas.getContext('2d');

  for (const layer of template.layers) {
    ctx.save();
    try {
      if (layer.opacity !== undefined && layer.opacity < 1) {
        ctx.globalAlpha = layer.opacity;
      }

      switch (layer.type) {
        case 'rect':
          await renderRect(ctx, layer, width, height);
          break;
        case 'circle':
          await renderCircle(ctx, layer, width, height);
          break;
        case 'image':
          await renderImage(ctx, layer, width, height, slots, images);
          break;
        case 'text':
          renderText(ctx, layer, width, height, slots);
          break;
      }
    } finally {
      ctx.restore();
    }
  }

  return canvas.toBuffer('image/png');
}

function resolveDimension(value: number | string | undefined, total: number, fallback: number = 0): number {
  if (value === undefined || value === null) return fallback;
  if (typeof value === 'number') return value;
  const trimmed = value.trim();
  if (trimmed.endsWith('%')) {
    const percent = parseFloat(trimmed.slice(0, -1));
    return isNaN(percent) ? fallback : (percent / 100) * total;
  }
  const parsed = parseFloat(trimmed);
  return isNaN(parsed) ? fallback : parsed;
}

async function renderRect(
  ctx: SKRSContext2D,
  layer: RectLayer,
  canvasWidth: number,
  canvasHeight: number
) {
  const x = resolveDimension(layer.x, canvasWidth, 0);
  const y = resolveDimension(layer.y, canvasHeight, 0);
  const w = resolveDimension(layer.w, canvasWidth, canvasWidth);
  const h = resolveDimension(layer.h, canvasHeight, canvasHeight);
  const radius = layer.borderRadius || 0;

  ctx.beginPath();
  if (radius > 0 && typeof ctx.roundRect === 'function') {
    ctx.roundRect(x, y, w, h, radius);
  } else {
    ctx.rect(x, y, w, h);
  }

  if (layer.fill) {
    ctx.fillStyle = layer.fill;
    ctx.fill();
  }

  if (layer.stroke) {
    ctx.strokeStyle = layer.stroke;
    ctx.lineWidth = layer.strokeWidth || 1;
    ctx.stroke();
  }
}

async function renderCircle(
  ctx: SKRSContext2D,
  layer: CircleLayer,
  canvasWidth: number,
  canvasHeight: number
) {
  const cx = resolveDimension(layer.cx, canvasWidth, canvasWidth / 2);
  const cy = resolveDimension(layer.cy, canvasHeight, canvasHeight / 2);
  const radius = resolveDimension(layer.radius, Math.min(canvasWidth, canvasHeight), 50);

  ctx.beginPath();
  ctx.arc(cx, cy, radius, 0, Math.PI * 2);

  if (layer.fill) {
    ctx.fillStyle = layer.fill;
    ctx.fill();
  }

  if (layer.stroke) {
    ctx.strokeStyle = layer.stroke;
    ctx.lineWidth = layer.strokeWidth || 1;
    ctx.stroke();
  }
}

async function renderImage(
  ctx: SKRSContext2D,
  layer: ImageLayer,
  canvasWidth: number,
  canvasHeight: number,
  slots: Record<string, string>,
  images: Map<string, Buffer>
) {
  const key = layer.slot ? (slots[layer.slot] || layer.slot) : (layer.src || '');
  let imageBuffer = images.get(key);

  if (!imageBuffer && layer.slot && images.has(layer.slot)) {
    imageBuffer = images.get(layer.slot);
  }
  if (!imageBuffer && layer.src && images.has(layer.src)) {
    imageBuffer = images.get(layer.src);
  }

  if (!imageBuffer) {
    return;
  }

  const img = await loadImage(imageBuffer);
  const dx = resolveDimension(layer.x, canvasWidth, 0);
  const dy = resolveDimension(layer.y, canvasHeight, 0);
  const dw = resolveDimension(layer.w, canvasWidth, canvasWidth);
  const dh = resolveDimension(layer.h, canvasHeight, canvasHeight);
  const radius = layer.borderRadius || 0;

  if (radius > 0) {
    ctx.beginPath();
    if (typeof ctx.roundRect === 'function') {
      ctx.roundRect(dx, dy, dw, dh, radius);
    } else {
      ctx.rect(dx, dy, dw, dh);
    }
    ctx.clip();
  }

  const fit = layer.fit || 'cover';
  const imgW = img.width;
  const imgH = img.height;

  if (fit === 'fill') {
    ctx.drawImage(img, dx, dy, dw, dh);
  } else if (fit === 'contain') {
    const scale = Math.min(dw / imgW, dh / imgH);
    const renderW = imgW * scale;
    const renderH = imgH * scale;
    const offsetX = dx + (dw - renderW) / 2;
    const offsetY = dy + (dh - renderH) / 2;
    ctx.drawImage(img, offsetX, offsetY, renderW, renderH);
  } else {
    // cover
    const scale = Math.max(dw / imgW, dh / imgH);
    const renderW = imgW * scale;
    const renderH = imgH * scale;
    const sx = (renderW - dw) / 2 / scale;
    const sy = (renderH - dh) / 2 / scale;
    const sw = dw / scale;
    const sh = dh / scale;

    ctx.drawImage(img, sx, sy, sw, sh, dx, dy, dw, dh);
  }
}

function renderText(
  ctx: SKRSContext2D,
  layer: TextLayer,
  canvasWidth: number,
  canvasHeight: number,
  slots: Record<string, string>
) {
  let content = layer.slot && slots[layer.slot] !== undefined ? slots[layer.slot] : (layer.text || '');
  if (!content) return;

  if (layer.prefix) {
    content = `${layer.prefix}${content}`;
  }
  if (layer.suffix) {
    content = `${content}${layer.suffix}`;
  }
  if (layer.textTransform === 'uppercase') {
    content = content.toUpperCase();
  } else if (layer.textTransform === 'lowercase') {
    content = content.toLowerCase();
  }

  const fontSize = layer.fontSize || 32;
  const fontFamily = layer.fontFamily || 'Inter, sans-serif';
  const fontWeight = layer.fontWeight || 'normal';
  ctx.font = `${fontWeight} ${fontSize}px ${fontFamily}`;
  ctx.fillStyle = layer.color || '#ffffff';
  ctx.textAlign = layer.align || 'left';
  ctx.textBaseline = 'top';

  const x = resolveDimension(layer.x, canvasWidth, 0);
  const y = resolveDimension(layer.y, canvasHeight, 0);
  const maxWidth = layer.maxWidth ? resolveDimension(layer.maxWidth, canvasWidth, canvasWidth - x) : undefined;
  const lineHeight = fontSize * (layer.lineHeight || 1.3);

  const lines = wrapText(ctx, content, maxWidth);

  for (let i = 0; i < lines.length; i++) {
    const lineY = y + i * lineHeight;
    let lineX = x;
    if (layer.align === 'center') {
      lineX = maxWidth ? x + maxWidth / 2 : x;
    } else if (layer.align === 'right') {
      lineX = maxWidth ? x + maxWidth : x;
    }
    ctx.fillText(lines[i], lineX, lineY);
  }
}

function wrapText(
  ctx: SKRSContext2D,
  text: string,
  maxWidth?: number
): string[] {
  if (!maxWidth || maxWidth <= 0) {
    return text.split('\n');
  }

  const result: string[] = [];
  const paragraphs = text.split('\n');

  for (const paragraph of paragraphs) {
    if (!paragraph) {
      result.push('');
      continue;
    }

    const words = paragraph.split(' ');
    let currentLine = '';

    for (const word of words) {
      const candidate = currentLine ? `${currentLine} ${word}` : word;
      const width = ctx.measureText(candidate).width;

      if (width <= maxWidth || !currentLine) {
        currentLine = candidate;
      } else {
        result.push(currentLine);
        currentLine = word;
      }
    }

    if (currentLine) {
      result.push(currentLine);
    }
  }

  return result;
}
