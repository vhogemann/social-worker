import sharp from 'sharp';
import { TargetRatio, FitMode } from '../types/schema.js';

export interface TransformResult {
  buffer: Buffer;
  width: number;
  height: number;
  mimeType: string;
}

const RATIO_DIMENSIONS: Record<TargetRatio, { width: number; height: number }> = {
  '1:1': { width: 1080, height: 1080 },
  '4:5': { width: 1080, height: 1350 },
  '9:16': { width: 1080, height: 1920 },
};

export async function resizeImage(
  inputBuffer: Buffer,
  maxDimension: number = 1200
): Promise<TransformResult> {
  const image = sharp(inputBuffer);
  const metadata = await image.metadata();

  const originalWidth = metadata.width || maxDimension;
  const originalHeight = metadata.height || maxDimension;
  const format = metadata.format || 'jpeg';

  if (originalWidth <= maxDimension && originalHeight <= maxDimension) {
    const mimeType = formatToMimeType(format);
    return {
      buffer: inputBuffer,
      width: originalWidth,
      height: originalHeight,
      mimeType,
    };
  }

  const resizedBuffer = await sharp(inputBuffer)
    .resize(maxDimension, maxDimension, {
      fit: 'inside',
      withoutEnlargement: true,
    })
    .toBuffer();

  const resultMeta = await sharp(resizedBuffer).metadata();
  const width = resultMeta.width || maxDimension;
  const height = resultMeta.height || maxDimension;
  const mimeType = formatToMimeType(resultMeta.format || format);

  return {
    buffer: resizedBuffer,
    width,
    height,
    mimeType,
  };
}

export async function aspectRatioTransform(
  inputBuffer: Buffer,
  targetRatio: TargetRatio,
  fitMode: FitMode = 'blur_padding'
): Promise<TransformResult> {
  const { width: targetWidth, height: targetHeight } = RATIO_DIMENSIONS[targetRatio];

  let outputBuffer: Buffer;

  if (fitMode === 'cover_crop') {
    outputBuffer = await sharp(inputBuffer)
      .resize(targetWidth, targetHeight, {
        fit: 'cover',
        position: 'center',
      })
      .jpeg({ quality: 90 })
      .toBuffer();
  } else if (fitMode === 'solid_padding') {
    outputBuffer = await sharp(inputBuffer)
      .resize(targetWidth, targetHeight, {
        fit: 'contain',
        background: { r: 18, g: 18, b: 18, alpha: 1 },
      })
      .jpeg({ quality: 90 })
      .toBuffer();
  } else {
    // blur_padding
    const background = await sharp(inputBuffer)
      .resize(targetWidth, targetHeight, { fit: 'cover' })
      .blur(25)
      .toBuffer();

    const foreground = await sharp(inputBuffer)
      .resize(targetWidth, targetHeight, { fit: 'inside' })
      .toBuffer();

    outputBuffer = await sharp(background)
      .composite([{ input: foreground, gravity: 'center' }])
      .jpeg({ quality: 90 })
      .toBuffer();
  }

  return {
    buffer: outputBuffer,
    width: targetWidth,
    height: targetHeight,
    mimeType: 'image/jpeg',
  };
}

function formatToMimeType(format: string): string {
  switch (format) {
    case 'png':
      return 'image/png';
    case 'webp':
      return 'image/webp';
    case 'gif':
      return 'image/gif';
    case 'svg':
      return 'image/svg+xml';
    case 'jpeg':
    case 'jpg':
    default:
      return 'image/jpeg';
  }
}
