import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import sharp from 'sharp';
import { buildApp } from '../src/app.js';
import {
  DiagramSyntaxError,
  renderDiagram,
  selectResolution,
  stripMermaidFences,
} from '../src/services/diagram.service.js';

describe('diagram.service', () => {
  it('strips markdown mermaid fences cleanly', () => {
    const fenced = '```mermaid\nflowchart LR\n  A --> B\n```';
    expect(stripMermaidFences(fenced)).toBe('flowchart LR\n  A --> B');

    const bare = 'sequenceDiagram\n  Alice->>Bob: Hi';
    expect(stripMermaidFences(bare)).toBe(bare);
  });

  it('selects best-fitting social aspect ratio in auto mode', () => {
    expect(selectResolution('auto', 900, 300)).toEqual({
      width: 1200,
      height: 675,
      resolvedAspectRatio: '16:9',
    });
    expect(selectResolution('auto', 300, 700)).toEqual({
      width: 1080,
      height: 1350,
      resolvedAspectRatio: '4:5',
    });
    expect(selectResolution('auto', 500, 500)).toEqual({
      width: 1080,
      height: 1080,
      resolvedAspectRatio: '1:1',
    });
  });

  it('renders flowchart, sequence, state, class, and ER diagrams to valid PNGs', async () => {
    const samples = [
      'flowchart LR\n  A[Client] --> B[API]\n  B --> C[(Postgres)]',
      'sequenceDiagram\n  User->>API: POST /render\n  API-->>User: 200 PNG',
      'stateDiagram-v2\n  [*] --> Draft\n  Draft --> Ready\n  Ready --> Sent',
      'classDiagram\n  class Draft {\n    +Guid Id\n    +string Stage\n  }\n  Draft --> MediaAsset',
      'erDiagram\n  DRAFT ||--o{ MEDIA_ASSET : has',
    ];

    for (const code of samples) {
      const out = await renderDiagram({
        code,
        title: 'Architecture Flow',
        subtitle: 'Social Worker',
        theme: 'dark',
        aspectRatio: 'auto',
        cardStyle: true,
      });

      const meta = await sharp(out.buffer).metadata();
      expect(meta.format).toBe('png');
      expect(meta.width).toBe(out.width);
      expect(meta.height).toBe(out.height);
    }
  });

  it('supports light theme, named presets, and cardStyle=false', async () => {
    const light = await renderDiagram({
      code: 'flowchart TD\n  A --> B',
      theme: 'light',
      aspectRatio: '1:1',
      cardStyle: true,
    });
    expect(light.width).toBe(1080);
    expect(light.height).toBe(1080);

    const preset = await renderDiagram({
      code: 'flowchart LR\n  A --> B --> C',
      theme: 'tokyo-night',
      aspectRatio: '16:9',
      cardStyle: false,
    });
    expect(preset.width).toBe(1200);
    expect(preset.height).toBe(675);
  });

  it('throws DiagramSyntaxError with helpful hint on invalid syntax', async () => {
    await expect(
      renderDiagram({
        code: 'notAValidDiagramType\n  foo bar baz',
        theme: 'dark',
        aspectRatio: 'auto',
        cardStyle: true,
      })
    ).rejects.toThrow(DiagramSyntaxError);
  });
});

describe('POST /render/diagram route', () => {
  const app = buildApp();

  beforeAll(async () => {
    await app.ready();
  });

  afterAll(async () => {
    await app.close();
  });

  it('returns 200 PNG with dimension headers for valid Mermaid diagram', async () => {
    const res = await app.inject({
      method: 'POST',
      url: '/render/diagram',
      payload: {
        code: '```mermaid\nflowchart LR\n  A[Draft] --> B[Publish]\n```',
        title: 'Publish Flow',
        theme: 'dracula',
        aspectRatio: '16:9',
      },
    });

    expect(res.statusCode).toBe(200);
    expect(res.headers['content-type']).toBe('image/png');
    expect(res.headers['x-width']).toBe('1200');
    expect(res.headers['x-height']).toBe('675');
    expect(res.headers['x-aspect-ratio']).toBe('16:9');
    expect(res.rawPayload.length).toBeGreaterThan(1000);
  });

  it('returns 400 with hint on unsupported/invalid Mermaid syntax', async () => {
    const res = await app.inject({
      method: 'POST',
      url: '/render/diagram',
      payload: {
        code: 'gantt\n  title Unsupported\n  section A\n  Task :a1, 2026-01-01, 30d',
      },
    });

    expect(res.statusCode).toBe(400);
    const body = res.json();
    expect(body.error).toBeTruthy();
    expect(body.hint).toContain('flowchart');
  });

  it('returns 400 on schema validation error', async () => {
    const res = await app.inject({
      method: 'POST',
      url: '/render/diagram',
      payload: {
        code: '',
      },
    });

    expect(res.statusCode).toBe(400);
  });
});
