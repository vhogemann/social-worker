interface RgbColor {
  r: number;
  g: number;
  b: number;
  a: number;
}

export interface SvgDimensions {
  width: number;
  height: number;
}

function parseColor(raw: string): RgbColor | null {
  const s = raw.trim().toLowerCase();
  if (s === 'transparent') {
    return { r: 0, g: 0, b: 0, a: 0 };
  }
  if (s.startsWith('#')) {
    const hex = s.slice(1);
    if (hex.length === 3) {
      return {
        r: parseInt(hex[0] + hex[0], 16),
        g: parseInt(hex[1] + hex[1], 16),
        b: parseInt(hex[2] + hex[2], 16),
        a: 1,
      };
    }
    if (hex.length === 6) {
      return {
        r: parseInt(hex.slice(0, 2), 16),
        g: parseInt(hex.slice(2, 4), 16),
        b: parseInt(hex.slice(4, 6), 16),
        a: 1,
      };
    }
    if (hex.length === 8) {
      return {
        r: parseInt(hex.slice(0, 2), 16),
        g: parseInt(hex.slice(2, 4), 16),
        b: parseInt(hex.slice(4, 6), 16),
        a: parseInt(hex.slice(6, 8), 16) / 255,
      };
    }
    return null;
  }

  const rgbaMatch = s.match(
    /^rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)(?:\s*,\s*([\d.]+))?\s*\)$/
  );
  if (rgbaMatch) {
    return {
      r: Number(rgbaMatch[1]),
      g: Number(rgbaMatch[2]),
      b: Number(rgbaMatch[3]),
      a: rgbaMatch[4] !== undefined ? Number(rgbaMatch[4]) : 1,
    };
  }

  return null;
}

function formatColor(c: RgbColor): string {
  const r = Math.max(0, Math.min(255, Math.round(c.r)));
  const g = Math.max(0, Math.min(255, Math.round(c.g)));
  const b = Math.max(0, Math.min(255, Math.round(c.b)));
  if (c.a >= 0.999) {
    return `#${r.toString(16).padStart(2, '0')}${g.toString(16).padStart(2, '0')}${b.toString(16).padStart(2, '0')}`;
  }
  const a = Math.max(0, Math.min(1, Number(c.a.toFixed(3))));
  return `rgba(${r}, ${g}, ${b}, ${a})`;
}

function splitTopLevelCommas(input: string): string[] {
  const parts: string[] = [];
  let depth = 0;
  let start = 0;
  for (let i = 0; i < input.length; i++) {
    const ch = input[i];
    if (ch === '(') depth++;
    else if (ch === ')') depth--;
    else if (ch === ',' && depth === 0) {
      parts.push(input.slice(start, i).trim());
      start = i + 1;
    }
  }
  parts.push(input.slice(start).trim());
  return parts;
}

function replaceFunctionCalls(
  input: string,
  fnName: string,
  evaluator: (args: string) => string
): string {
  const token = `${fnName}(`;
  let result = '';
  let i = 0;

  while (i < input.length) {
    const idx = input.indexOf(token, i);
    if (idx === -1) {
      result += input.slice(i);
      break;
    }

    result += input.slice(i, idx);
    let depth = 1;
    let j = idx + token.length;
    while (j < input.length && depth > 0) {
      if (input[j] === '(') depth++;
      else if (input[j] === ')') depth--;
      j++;
    }

    if (depth !== 0) {
      result += input.slice(idx);
      break;
    }

    const inner = input.slice(idx + token.length, j - 1);
    const resolvedInner = replaceFunctionCalls(inner, fnName, evaluator);
    result += evaluator(resolvedInner);
    i = j;
  }

  return result;
}

function parseColorStop(part: string): { color: RgbColor | null; pct: number | null } {
  const trimmed = part.trim();
  const pctMatch = trimmed.match(/^(.*?)\s+([\d.]+)%$/);
  if (pctMatch) {
    return {
      color: parseColor(pctMatch[1]),
      pct: Number(pctMatch[2]) / 100,
    };
  }
  return {
    color: parseColor(trimmed),
    pct: null,
  };
}

function evaluateColorMix(args: string): string {
  const parts = splitTopLevelCommas(args);
  if (parts.length < 3) {
    return `color-mix(${args})`;
  }

  const stop1 = parseColorStop(parts[1]);
  const stop2 = parseColorStop(parts[2]);

  if (!stop1.color || !stop2.color) {
    return `color-mix(${args})`;
  }

  let w1 = stop1.pct;
  let w2 = stop2.pct;

  if (w1 === null && w2 === null) {
    w1 = 0.5;
    w2 = 0.5;
  } else if (w1 !== null && w2 === null) {
    w2 = Math.max(0, 1 - w1);
  } else if (w1 === null && w2 !== null) {
    w1 = Math.max(0, 1 - w2);
  }

  const total = (w1 ?? 0) + (w2 ?? 0);
  if (total <= 0) {
    return formatColor(stop1.color);
  }

  const p1 = (w1 ?? 0) / total;
  const p2 = (w2 ?? 0) / total;

  return formatColor({
    r: stop1.color.r * p1 + stop2.color.r * p2,
    g: stop1.color.g * p1 + stop2.color.g * p2,
    b: stop1.color.b * p1 + stop2.color.b * p2,
    a: stop1.color.a * p1 + stop2.color.a * p2,
  });
}

function resolveExpression(expr: string, vars: Map<string, string>, depth = 0): string {
  if (depth > 8) {
    return expr;
  }

  let out = replaceFunctionCalls(expr, 'var', (args) => {
    const parts = splitTopLevelCommas(args);
    const name = parts[0]?.trim();
    const fallback = parts.length > 1 ? parts.slice(1).join(', ').trim() : undefined;

    if (name && vars.has(name)) {
      const value = vars.get(name)!;
      return resolveExpression(value, vars, depth + 1);
    }
    if (fallback !== undefined) {
      return resolveExpression(fallback, vars, depth + 1);
    }
    return `var(${args})`;
  });

  out = replaceFunctionCalls(out, 'color-mix', evaluateColorMix);
  return out;
}

function collectVariables(text: string, vars: Map<string, string>, overwrite: boolean) {
  const cleaned = text.replace(/\/\*[\s\S]*?\*\//g, '');
  const declRegex = /(--[a-zA-Z0-9_-]+)\s*:\s*([^;{}]+)/g;
  let match: RegExpExecArray | null;
  while ((match = declRegex.exec(cleaned)) !== null) {
    const name = match[1].trim();
    const value = match[2].trim();
    if (overwrite || !vars.has(name)) {
      vars.set(name, value);
    }
  }
}

export function extractSvgDimensions(svg: string): SvgDimensions {
  const viewBoxMatch = svg.match(/viewBox=["']\s*[\d.-]+\s+[\d.-]+\s+([\d.]+)\s+([\d.]+)\s*["']/i);
  if (viewBoxMatch) {
    const width = Number(viewBoxMatch[1]);
    const height = Number(viewBoxMatch[2]);
    if (Number.isFinite(width) && Number.isFinite(height) && width > 0 && height > 0) {
      return { width, height };
    }
  }

  const widthMatch = svg.match(/\bwidth=["']([\d.]+)(?:px)?["']/i);
  const heightMatch = svg.match(/\bheight=["']([\d.]+)(?:px)?["']/i);
  const width = widthMatch ? Number(widthMatch[1]) : 800;
  const height = heightMatch ? Number(heightMatch[1]) : 600;

  return {
    width: Number.isFinite(width) && width > 0 ? width : 800,
    height: Number.isFinite(height) && height > 0 ? height : 600,
  };
}

export function resolveSvgCssVariables(svg: string): string {
  let processed = svg.replace(/@import\s+url\([^)]*\)\s*;?/gi, '');

  const vars = new Map<string, string>();

  const styleBlocks = processed.match(/<style[^>]*>[\s\S]*?<\/style>/gi) ?? [];
  for (const block of styleBlocks) {
    collectVariables(block, vars, false);
  }

  const rootStyleMatch = processed.match(/<svg[^>]*\bstyle=["']([^"']*)["']/i);
  if (rootStyleMatch) {
    collectVariables(rootStyleMatch[1], vars, true);
  }

  for (let pass = 0; pass < 4; pass++) {
    for (const [key, val] of vars.entries()) {
      const next = resolveExpression(val, vars);
      if (next !== val) {
        vars.set(key, next);
      }
    }
  }

  processed = resolveExpression(processed, vars);

  processed = processed.replace(/font-family:\s*['"]?Inter['"]?[^;"}]*/gi, "font-family: 'Inter', 'DejaVu Sans', 'Noto Sans', sans-serif");

  return processed;
}
