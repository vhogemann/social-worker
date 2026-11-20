import { FastifyInstance } from 'fastify';
import { DiagramRenderSchema } from '../types/diagram.js';
import { DiagramSyntaxError, renderDiagram } from '../services/diagram.service.js';

export async function diagramRoutes(fastify: FastifyInstance) {
  fastify.post('/render/diagram', async (request, reply) => {
    const parseResult = DiagramRenderSchema.safeParse(request.body);
    if (!parseResult.success) {
      return reply.status(400).send({
        error: 'Diagram validation failed',
        issues: parseResult.error.issues,
      });
    }

    try {
      const result = await renderDiagram(parseResult.data);

      reply
        .header('Content-Type', 'image/png')
        .header('X-Width', result.width.toString())
        .header('X-Height', result.height.toString())
        .header('X-Aspect-Ratio', result.resolvedAspectRatio)
        .send(result.buffer);
    } catch (err: any) {
      if (err instanceof DiagramSyntaxError) {
        return reply.status(400).send({
          error: err.message,
          hint: err.hint,
        });
      }

      request.log.error(err, 'Failed to render diagram');
      return reply.status(500).send({ error: `Diagram render failed: ${err.message}` });
    }
  });
}
