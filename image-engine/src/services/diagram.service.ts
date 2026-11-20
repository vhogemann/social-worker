import { renderMermaidSVG, THEMES } from 'beautiful-mermaid';
import { createCanvas, loadImage } from '@napi-rs/canvas';
import sharp from 'sharp';
import { initFonts } from './template.service.js';
import { extractSvgDimensions, resolveSvgCssVariables } from './svg-css-resolver.js';
import { DiagramAspectRatio, DiagramRenderSpec, DiagramTheme } from '../types/diagram.js';

export class DiagramSyntaxError extends Error {
  public readonly hint: string;

  constructor(message: string, hint?: string) {
    super(message);
    this.name = 'DiagramSyntaxError';
    this.hint =
      hint ??
      'Supported Mermaid diagram types: flowchart (or graph), sequenceDiagram, stateDiagram-v2, classDiagram, erDiagram. Quote node labels containing parentheses or brackets, e.g. A["API (v2)"].';
  }
}

interface CardThemePalette {
  outerBg: string;
  cardBg: string;
  cardBorder: string;
  titleColor: string;
  subtitleColor: string;
  mermaid: {
    bg: string;
    fg: string;
    accent?: string;
    line?: string;
    muted?: string;
    surface?: string;
    border?: string;
  };
}

export interface DiagramRenderOutput {
  buffer: Buffer;
  width: number;
  height: number;
  resolvedAspectRatio: '16:9' | '1:1' | '4:5';
}

const LIGHT_PRESETS = new Set<string>([
  'light',
  'zinc-light',
  'tokyo-night-light',
  'catppuccin-latte',
  'nord-light',
  'github-light',
  'solarized-light',
]);

export function stripMermaidFences(raw: string): string {
  let cleaned = raw.trim();
  const fenceMatch = cleaned.match(/^```(?:mermaid)?\s*\r?\n([\s\S]*?)\r?\n?```$/i);
  if (fenceMatch) {
    cleaned = fenceMatch[1].trim();
  }
  return cleaned;
}

export function resolveThemePalette(theme: DiagramTheme): CardThemePalette {
  if (theme === 'dark') {
    return {
      outerBg: '#0f172a',
      cardBg: '#1e293b',
      cardBorder: '#334155',
      titleColor: '#f8fafc',
      subtitleColor: '#94a3b8',
      mermaid: {
        bg: '#1e293b',
        fg: '#f1f5f9',
        accent: '#6366f1',
        line: '#64748b',
        muted: '#94a3b8',
        surface: '#0f172a',
        border: '#475569',
      },
    };
  }

  if (theme === 'light') {
    return {
      outerBg: '#f8fafc',
      cardBg: '#ffffff',
      cardBorder: '#e2e8f0',
      titleColor: '#0f172a',
      subtitleColor: '#64748b',
      mermaid: {
        bg: '#ffffff',
        fg: '#0f172a',
        accent: '#4f46e5',
        line: '#64748b',
        muted: '#64748b',
        surface: '#f8fafc',
        border: '#cbd5e1',
      },
    };
  }

  const preset = (THEMES as Record<string, CardThemePalette['mermaid']>)[theme];
  const isLight = LIGHT_PRESETS.has(theme);
  if (!preset) {
    return resolveThemePalette(isLight ? 'light' : 'dark');
  }

  return {
    outerBg: preset.surface ?? (isLight ? '#f1f5f9' : '#0b1120'),
    cardBg: preset.bg,
    cardBorder: preset.border ?? (isLight ? '#cbd5e1' : '#334155'),
    titleColor: preset.fg,
    subtitleColor: preset.muted ?? (isLight ? '#64748b' : '#94a3b8'),
    mermaid: {
      bg: preset.bg,
      fg: preset.fg,
      accent: preset.accent,
      line: preset.line,
      muted: preset.muted,
      surface: preset.surface,
      border: preset.border,
    },
  };
}

export function selectResolution(
  aspectRatio: DiagramAspectRatio,
  svgWidth: number,
  svgHeight: number
): { width: number; height: number; resolvedAspectRatio: '16:9' | '1:1' | '4:5' } {
  let target: '16:9' | '1:1' | '4:5';

  if (aspectRatio === 'auto') {
    const ratio = svgHeight > 0 ? svgWidth / svgHeight : 1;
    if (ratio > 1.3) {
      target = '16:9';
    } else if (ratio < 0.85) {
      target = '4:5';
    } else {
      target = '1:1';
    }
  } else {
    target = aspectRatio;
  }

  switch (target) {
    case '1:1':
      return { width: 1080, height: 1080, resolvedAspectRatio: '1:1' };
    case '4:5':
      return { width: 1080, height: 1350, resolvedAspectRatio: '4:5' };
    case '16:9':
    default:
      return { width: 1200, height: 675, resolvedAspectRatio: '16:9' };
  }
}

export async function renderDiagram(spec: DiagramRenderSpec): Promise<DiagramRenderOutput> {
  initFonts();

  const cleanCode = stripMermaidFences(spec.code);
  if (!cleanCode) {
    throw new DiagramSyntaxError('Mermaid diagram code cannot be empty.');
  }

  const palette = resolveThemePalette(spec.theme);

  let rawSvg: string;
  try {
    rawSvg = renderMermaidSVG(cleanCode, {
      ...palette.mermaid,
      font: 'Inter',
      transparent: true,
    });
  } catch (err: any) {
    const msg = err instanceof Error ? err.message : String(err);
    throw new DiagramSyntaxError(`Invalid Mermaid diagram syntax: ${msg}`);
  }

  const resolvedSvg = resolveSvgCssVariables(rawSvg);
  const { width: svgW, height: svgH } = extractSvgDimensions(resolvedSvg);
  const { width: totalWidth, height: totalHeight, resolvedAspectRatio } = selectResolution(
    spec.aspectRatio,
    svgW,
    svgH
  );

  const mainCanvas = createCanvas(totalWidth, totalHeight);
  const mainCtx = mainCanvas.getContext('2d');

  let contentX = 32;
  let contentY = 32;
  let contentW = totalWidth - 64;
  let contentH = totalHeight - 64;

  if (spec.cardStyle) {
    const padding = 48;
    const innerPadding = 32;

    mainCtx.fillStyle = palette.outerBg;
    mainCtx.fillRect(0, 0, totalWidth, totalHeight);

    const cardX = padding;
    const cardY = padding;
    const cardW = totalWidth - padding * 2;
    const cardH = totalHeight - padding * 2;
    const cardRadius = 24;

    mainCtx.save();
    mainCtx.beginPath();
    mainCtx.roundRect(cardX, cardY, cardW, cardH, cardRadius);
    mainCtx.fillStyle = palette.cardBg;
    mainCtx.fill();
    mainCtx.lineWidth = 1.5;
    mainCtx.strokeStyle = palette.cardBorder;
    mainCtx.stroke();
    mainCtx.restore();

    let headerOffset = cardY + innerPadding;
    if (spec.title) {
      mainCtx.font = 'bold 30px Inter, sans-serif';
      mainCtx.fillStyle = palette.titleColor;
      mainCtx.textBaseline = 'top';
      mainCtx.fillText(spec.title, cardX + innerPadding, headerOffset);
      headerOffset += 38;
    }

    if (spec.subtitle) {
      mainCtx.font = '16px Inter, sans-serif';
      mainCtx.fillStyle = palette.subtitleColor;
      mainCtx.textBaseline = 'top';
      mainCtx.fillText(spec.subtitle, cardX + innerPadding, headerOffset);
      headerOffset += 28;
    }

    const headerHeight = headerOffset - cardY;
    contentX = cardX + innerPadding;
    contentY = cardY + headerHeight + (spec.title || spec.subtitle ? 16 : 0);
    contentW = cardW - innerPadding * 2;
    contentH = cardH - (contentY - cardY) - innerPadding;
  } else {
    mainCtx.fillStyle = palette.cardBg;
    mainCtx.fillRect(0, 0, totalWidth, totalHeight);
  }

  const maxScale = 2.5;
  const scaleX = contentW / svgW;
  const scaleY = contentH / svgH;
  const scale = Math.min(scaleX, scaleY, maxScale);

  const targetW = Math.max(1, Math.round(svgW * scale));
  const targetH = Math.max(1, Math.round(svgH * scale));
  const density = Math.max(144, Math.min(400, Math.round(72 * scale * 2)));

  const diagramPngBuffer = await sharp(Buffer.from(resolvedSvg, 'utf-8'), { density })
    .resize(targetW, targetH, {
      fit: 'inside',
      withoutEnlargement: false,
      background: { r: 0, g: 0, b: 0, alpha: 0 },
    })
    .png()
    .toBuffer();

  const diagramImage = await loadImage(diagramPngBuffer);
  const drawX = Math.round(contentX + (contentW - diagramImage.width) / 2);
  const drawY = Math.round(contentY + (contentH - diagramImage.height) / 2);

  mainCtx.drawImage(diagramImage, drawX, drawY);

  return {
    buffer: mainCanvas.toBuffer('image/png'),
    width: totalWidth,
    height: totalHeight,
    resolvedAspectRatio,
  };
}
