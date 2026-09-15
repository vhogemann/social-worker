import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import React from 'react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { TemplateEditorPage } from './TemplateEditorPage';
import * as templatesApi from '../../api/templates';

vi.mock('../../api/templates', () => ({
  fetchTemplate: vi.fn(),
  createTemplate: vi.fn(),
  updateTemplate: vi.fn(),
  previewTemplateWithImages: vi.fn(),
}));

describe('TemplateEditorPage', () => {
  const routerFuture = { v7_startTransition: true, v7_relativeSplatPath: true };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders blank template editor for new template', async () => {
    render(
      <MemoryRouter initialEntries={['/templates/editor']} future={routerFuture}>
        <Routes>
          <Route path="/templates/editor" element={<TemplateEditorPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('← Templates')).toBeInTheDocument();
      expect(screen.getByText('Save Template')).toBeInTheDocument();
      expect(screen.getByText('Show Source')).toBeInTheDocument();
    });
  });

  it('opens and closes source code drawer', async () => {
    render(
      <MemoryRouter initialEntries={['/templates/editor']} future={routerFuture}>
        <Routes>
          <Route path="/templates/editor" element={<TemplateEditorPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Show Source')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Show Source'));

    await waitFor(() => {
      expect(screen.getByText('Template Source JSON')).toBeInTheDocument();
      expect(screen.getByText('Apply Source')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: 'Close' }));

    await waitFor(() => {
      expect(screen.queryByText('Template Source JSON')).not.toBeInTheDocument();
    });
  });

  it('loads existing template by ID', async () => {
    vi.mocked(templatesApi.fetchTemplate).mockResolvedValueOnce({
      id: 't-edit-1',
      userId: 'u-1',
      name: 'Custom Edit Template',
      description: 'Editable description',
      width: 1080,
      height: 1350,
      schema: JSON.stringify({
        name: 'Custom Edit Template',
        width: 1080,
        height: 1350,
        slots: [{ name: 'quote_text', type: 'text' }],
        layers: [
          { type: 'rect', x: 0, y: 0, w: 1080, h: 1350, fill: '#18181b' },
          { type: 'text', slot: 'quote_text', x: 100, y: 300, fontSize: 40, text: 'Hello' },
        ],
      }),
      thumbnailMediaAssetId: null,
      thumbnailUrl: '/api/templates/t-edit-1/render',
      createdAt: '2026-08-25T10:00:00Z',
      updatedAt: '2026-08-25T10:00:00Z',
    });

    render(
      <MemoryRouter initialEntries={['/templates/editor/t-edit-1']} future={routerFuture}>
        <Routes>
          <Route path="/templates/editor/:id" element={<TemplateEditorPage />} />
        </Routes>
      </MemoryRouter>
    );

    await waitFor(() => {
      expect(screen.getByText('Custom Edit Template')).toBeInTheDocument();
      expect(screen.getByText('1080x1350')).toBeInTheDocument();
    });
  });
});
