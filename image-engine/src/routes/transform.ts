import { FastifyInstance } from 'fastify';
import { resizeImage, aspectRatioTransform } from '../services/transform.service.js';
import { TargetRatio, FitMode, TargetRatioSchema, FitModeSchema } from '../types/schema.js';

export async function transformRoutes(fastify: FastifyInstance) {
  fastify.post('/transform/resize', async (request, reply) => {
    let fileBuffer: Buffer | null = null;
    let maxDimension = 1200;

    const parts = request.parts();
    for await (const part of parts) {
      if (part.type === 'file') {
        fileBuffer = await part.toBuffer();
      } else if (part.type === 'field') {
        if (part.fieldname === 'max_dimension' || part.fieldname === 'maxDimension') {
          const parsed = parseInt(part.value as string, 10);
          if (!isNaN(parsed) && parsed > 0) {
            maxDimension = parsed;
          }
        }
      }
    }

    if (!fileBuffer) {
      return reply.status(400).send({ error: 'Missing image file in multipart request.' });
    }

    try {
      const result = await resizeImage(fileBuffer, maxDimension);
      reply
        .header('Content-Type', result.mimeType)
        .header('X-Width', result.width.toString())
        .header('X-Height', result.height.toString())
        .send(result.buffer);
    } catch (err: any) {
      request.log.error(err, 'Failed to resize image');
      return reply.status(400).send({ error: `Image resize failed: ${err.message}` });
    }
  });

  fastify.post('/transform/aspect-ratio', async (request, reply) => {
    let fileBuffer: Buffer | null = null;
    let targetRatio: TargetRatio = '1:1';
    let fitMode: FitMode = 'blur_padding';

    const parts = request.parts();
    for await (const part of parts) {
      if (part.type === 'file') {
        fileBuffer = await part.toBuffer();
      } else if (part.type === 'field') {
        if (part.fieldname === 'target_ratio' || part.fieldname === 'targetRatio') {
          const parseResult = TargetRatioSchema.safeParse(part.value);
          if (parseResult.success) {
            targetRatio = parseResult.data;
          }
        } else if (part.fieldname === 'fit_mode' || part.fieldname === 'fitMode') {
          const parseResult = FitModeSchema.safeParse(part.value);
          if (parseResult.success) {
            fitMode = parseResult.data;
          }
        }
      }
    }

    if (!fileBuffer) {
      return reply.status(400).send({ error: 'Missing image file in multipart request.' });
    }

    try {
      const result = await aspectRatioTransform(fileBuffer, targetRatio, fitMode);
      reply
        .header('Content-Type', result.mimeType)
        .header('X-Width', result.width.toString())
        .header('X-Height', result.height.toString())
        .send(result.buffer);
    } catch (err: any) {
      request.log.error(err, 'Failed to transform aspect ratio');
      return reply.status(400).send({ error: `Aspect ratio transform failed: ${err.message}` });
    }
  });
}
