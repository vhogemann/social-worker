import { describe, it, expect } from 'vitest';
import { buildApp } from '../src/app.js';

describe('Health endpoint', () => {
  it('GET /health returns 200 with capabilities', async () => {
    const app = buildApp();
    const response = await app.inject({
      method: 'GET',
      url: '/health',
    });

    expect(response.statusCode).toBe(200);
    const body = JSON.parse(response.payload);
    expect(body.status).toBe('ok');
    expect(body.capabilities).toContain('resize');
    expect(body.capabilities).toContain('aspect-ratio');
    expect(body.capabilities).toContain('template-render');
  });
});
