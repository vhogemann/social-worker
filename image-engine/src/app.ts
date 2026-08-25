import Fastify from 'fastify';
import cors from '@fastify/cors';
import multipart from '@fastify/multipart';
import { serializerCompiler, validatorCompiler, ZodTypeProvider } from 'fastify-type-provider-zod';
import { healthRoutes } from './routes/health.js';
import { transformRoutes } from './routes/transform.js';
import { renderRoutes } from './routes/render.js';

export function buildApp() {
  const fastify = Fastify({
    logger: {
      level: process.env.LOG_LEVEL || 'info',
    },
  }).withTypeProvider<ZodTypeProvider>();

  fastify.setValidatorCompiler(validatorCompiler);
  fastify.setSerializerCompiler(serializerCompiler);

  fastify.register(cors, {
    origin: true,
  });

  fastify.register(multipart, {
    limits: {
      fileSize: 50 * 1024 * 1024, // 50MB
      files: 20,
    },
  });

  fastify.register(healthRoutes);
  fastify.register(transformRoutes);
  fastify.register(renderRoutes);

  return fastify;
}
