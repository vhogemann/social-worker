import { describe, it, expect } from 'vitest';
import sharp from 'sharp';
import { resizeImage, aspectRatioTransform } from '../src/services/transform.service.js';

describe('Transform Service', () => {
  it('resizes an image within maxDimension', async () => {
    const inputBuffer = await sharp({
      create: {
        width: 2000,
        height: 1000,
        channels: 4,
        background: { r: 255, g: 0, b: 0, alpha: 1 },
      },
    })
      .png()
      .toBuffer();

    const result = await resizeImage(inputBuffer, 1200);

    expect(result.width).toBe(1200);
    expect(result.height).toBe(600);
    expect(result.buffer).toBeInstanceOf(Buffer);
  });

  it('keeps image untouched if already smaller than maxDimension', async () => {
    const inputBuffer = await sharp({
      create: {
        width: 400,
        height: 300,
        channels: 4,
        background: { r: 0, g: 255, b: 0, alpha: 1 },
      },
    })
      .jpeg()
      .toBuffer();

    const result = await resizeImage(inputBuffer, 1200);

    expect(result.width).toBe(400);
    expect(result.height).toBe(300);
  });

  it('formats aspect ratio 1:1 with blur padding', async () => {
    const inputBuffer = await sharp({
      create: {
        width: 800,
        height: 400,
        channels: 4,
        background: { r: 0, g: 0, b: 255, alpha: 1 },
      },
    })
      .jpeg()
      .toBuffer();

    const result = await aspectRatioTransform(inputBuffer, '1:1', 'blur_padding');

    expect(result.width).toBe(1080);
    expect(result.height).toBe(1080);
    expect(result.mimeType).toBe('image/jpeg');
  });

  it('formats aspect ratio 4:5 with cover crop', async () => {
    const inputBuffer = await sharp({
      create: {
        width: 1200,
        height: 1200,
        channels: 4,
        background: { r: 255, g: 255, b: 0, alpha: 1 },
      },
    })
      .jpeg()
      .toBuffer();

    const result = await aspectRatioTransform(inputBuffer, '4:5', 'cover_crop');

    expect(result.width).toBe(1080);
    expect(result.height).toBe(1350);
  });

  it('formats aspect ratio 9:16 with solid padding', async () => {
    const inputBuffer = await sharp({
      create: {
        width: 600,
        height: 600,
        channels: 4,
        background: { r: 255, g: 0, b: 255, alpha: 1 },
      },
    })
      .jpeg()
      .toBuffer();

    const result = await aspectRatioTransform(inputBuffer, '9:16', 'solid_padding');

    expect(result.width).toBe(1080);
    expect(result.height).toBe(1920);
  });
});
