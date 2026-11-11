import { FastifyInstance } from 'fastify';
import { ChartRenderSchema } from '../types/chart.js';
import { renderChart, getResolution } from '../services/chart.service.js';

export async function chartRoutes(fastify: FastifyInstance) {
  fastify.post('/render/chart', async (request, reply) => {
    const parseResult = ChartRenderSchema.safeParse(request.body);
    if (!parseResult.success) {
      return reply.status(400).send({
        error: 'Chart validation failed',
        issues: parseResult.error.issues,
      });
    }

    try {
      const buffer = await renderChart(parseResult.data);
      const { width, height } = getResolution(parseResult.data.aspectRatio);

      reply
        .header('Content-Type', 'image/png')
        .header('X-Width', width.toString())
        .header('X-Height', height.toString())
        .send(buffer);
    } catch (err: any) {
      request.log.error(err, 'Failed to render chart');
      return reply.status(500).send({ error: `Chart render failed: ${err.message}` });
    }
  });
}
