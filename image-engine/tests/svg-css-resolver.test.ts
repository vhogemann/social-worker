import { describe, it, expect } from 'vitest';
import { extractSvgDimensions, resolveSvgCssVariables } from '../src/services/svg-css-resolver.js';

describe('svg-css-resolver', () => {
  it('strips remote @import rules and resolves nested var() and color-mix()', () => {
    const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 200" style="--bg:#000000;--fg:#ffffff;--accent:#6366f1">
<style>
  @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400&display=swap');
  svg {
    --_text: var(--fg);
    --_muted: var(--muted, color-mix(in srgb, var(--fg) 50%, var(--bg)));
    --_arrow: var(--accent, color-mix(in srgb, var(--fg) 80%, var(--bg)));
  }
</style>
<rect fill="var(--_muted)" stroke="var(--_arrow)" />
<text fill="var(--_text)">Hello</text>
</svg>`;

    const resolved = resolveSvgCssVariables(svg);

    expect(resolved).not.toContain('@import');
    expect(resolved).not.toContain('var(--');
    expect(resolved).not.toContain('color-mix(');
    expect(resolved).toContain('fill="#808080"');
    expect(resolved).toContain('stroke="#6366f1"');
    expect(resolved).toContain('fill="#ffffff"');
  });

  it('extracts dimensions from viewBox first and falls back to width/height', () => {
    expect(extractSvgDimensions('<svg viewBox="0 0 512.5 256.25" width="100" height="50"></svg>')).toEqual({
      width: 512.5,
      height: 256.25,
    });
    expect(extractSvgDimensions('<svg width="320" height="480"></svg>')).toEqual({
      width: 320,
      height: 480,
    });
  });
});
