import { FastifyInstance } from 'fastify';
import { HealthResponseSchema } from '../types/schema.js';

export async function healthRoutes(fastify: FastifyInstance) {
  fastify.get(
    '/health',
    {
      schema: {
        response: {
          200: HealthResponseSchema,
        },
      },
    },
    async () => {
      return {
        status: 'ok' as const,
        version: '1.0.0',
        capabilities: ['resize', 'aspect-ratio', 'template-render'],
      };
    }
  );
}
