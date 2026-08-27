import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ImageInspector } from './ImageInspector';
import { useTemplateEditorStore } from '../templateEditorStore';
import * as templatesApi from '../../../../api/templates';
import { EditorImageLayer } from '../schemaCodec';

vi.mock('../../../../api/templates', () => ({
  uploadTemplateAsset: vi.fn(),
}));

describe('ImageInspector', () => {
  const mockLayer: EditorImageLayer = {
    id: 'layer_img_1',
    kind: 'image',
    x: 0,
    y: 0,
    w: 500,
    h: 500,
    fit: 'cover',
    opacity: 1,
    borderRadius: 0,
  };

  beforeEach(() => {
    vi.clearAllMocks();
    useTemplateEditorStore.getState().reset();
    useTemplateEditorStore.setState({
      layers: [mockLayer],
      slots: [
        { name: 'post_image', type: 'image', required: true },
      ],
    });
  });

  it('renders mode switcher buttons', () => {
    render(<ImageInspector layer={mockLayer} />);
    expect(screen.getByRole('button', { name: /fixed asset \/ logo/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /dynamic slot/i })).toBeInTheDocument();
  });

  it('switches to dynamic slot mode and binds to slot', () => {
    render(<ImageInspector layer={mockLayer} />);

    fireEvent.click(screen.getByRole('button', { name: /dynamic slot/i }));

    expect(screen.getByText(/bind to slot/i)).toBeInTheDocument();
    const select = screen.getByRole('combobox', { name: /bind to slot/i });
    expect(select).toHaveValue('post_image');
  });

  it('uploads fixed image asset and updates store', async () => {
    vi.mocked(templatesApi.uploadTemplateAsset).mockResolvedValueOnce({
      id: 'asset-123',
      url: '/api/media/asset-123',
      fileName: 'logo.png',
      mimeType: 'image/png',
      width: 200,
      height: 200,
      sizeBytes: 1024,
    });

    render(<ImageInspector layer={mockLayer} />);

    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    const testFile = new File(['fake'], 'logo.png', { type: 'image/png' });

    fireEvent.change(fileInput, { target: { files: [testFile] } });

    await waitFor(() => {
      expect(templatesApi.uploadTemplateAsset).toHaveBeenCalledWith(testFile);
      const state = useTemplateEditorStore.getState();
      const updated = state.layers.find((l) => l.id === mockLayer.id) as EditorImageLayer;
      expect(updated?.src).toBe('/api/media/asset-123');
    });
  });

  it('allows applying external URL for fixed image', () => {
    render(<ImageInspector layer={mockLayer} />);

    const input = screen.getByPlaceholderText(/or paste image url/i);
    fireEvent.change(input, { target: { value: 'https://example.com/logo.png' } });

    fireEvent.click(screen.getByRole('button', { name: /^set$/i }));

    const state = useTemplateEditorStore.getState();
    const updated = state.layers.find((l) => l.id === mockLayer.id) as EditorImageLayer;
    expect(updated?.src).toBe('https://example.com/logo.png');
  });
});
