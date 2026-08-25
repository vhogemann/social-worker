import { FastifyInstance } from 'fastify';
import { renderTemplate } from '../services/template.service.js';
import { TemplateSchema } from '../types/schema.js';

export async function renderRoutes(fastify: FastifyInstance) {
  fastify.post('/render/template', async (request, reply) => {
    let templateRaw: string | null = null;
    let slotsRaw: string | null = null;
    const images = new Map<string, Buffer>();

    const parts = request.parts();
    for await (const part of parts) {
      if (part.type === 'file') {
        const buffer = await part.toBuffer();
        if (part.fieldname === 'template') {
          templateRaw = buffer.toString('utf-8');
        } else if (part.fieldname === 'slots') {
          slotsRaw = buffer.toString('utf-8');
        } else {
          images.set(part.fieldname, buffer);
          if (part.filename && part.filename !== part.fieldname) {
            images.set(part.filename, buffer);
          }
        }
      } else if (part.type === 'field') {
        if (part.fieldname === 'template') {
          templateRaw = part.value as string;
        } else if (part.fieldname === 'slots') {
          slotsRaw = part.value as string;
        } else if (typeof part.value === 'string') {
          // If field value is passed as slot value
          // e.g. fieldname could be slot name if slots JSON not provided
        }
      }
    }

    if (!templateRaw) {
      return reply.status(400).send({ error: 'Missing template definition in request.' });
    }

    let parsedTemplateJson: any;
    try {
      parsedTemplateJson = JSON.parse(templateRaw);
    } catch (e: any) {
      return reply.status(400).send({ error: `Invalid JSON in template: ${e.message}` });
    }

    const templateValidation = TemplateSchema.safeParse(parsedTemplateJson);
    if (!templateValidation.success) {
      return reply.status(400).send({
        error: 'Template validation failed',
        issues: templateValidation.error.issues,
      });
    }

    let slots: Record<string, string> = {};
    if (slotsRaw) {
      try {
        slots = JSON.parse(slotsRaw);
      } catch (e: any) {
        return reply.status(400).send({ error: `Invalid JSON in slots: ${e.message}` });
      }
    }

    try {
      const pngBuffer = await renderTemplate(templateValidation.data, slots, images);
      reply
        .header('Content-Type', 'image/png')
        .header('X-Width', (templateValidation.data.width || 1080).toString())
        .header('X-Height', (templateValidation.data.height || 1080).toString())
        .send(pngBuffer);
    } catch (err: any) {
      request.log.error(err, 'Failed to render template');
      return reply.status(500).send({ error: `Template render failed: ${err.message}` });
    }
  });
}
