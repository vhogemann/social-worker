import { z } from 'zod';

export const HealthResponseSchema = z.object({
  status: z.literal('ok'),
  version: z.string(),
  capabilities: z.array(z.string()),
});

export type HealthResponse = z.infer<typeof HealthResponseSchema>;

export const TargetRatioSchema = z.enum(['1:1', '4:5', '9:16']);
export type TargetRatio = z.infer<typeof TargetRatioSchema>;

export const FitModeSchema = z.enum(['blur_padding', 'cover_crop', 'solid_padding']);
export type FitMode = z.infer<typeof FitModeSchema>;

export const SlotDefinitionSchema = z.object({
  name: z.string(),
  type: z.enum(['text', 'image', 'color']),
  required: z.boolean().default(false),
  description: z.string().optional(),
  defaultValue: z.string().optional(),
});

export type SlotDefinition = z.infer<typeof SlotDefinitionSchema>;

const DimensionValueSchema = z.union([z.number(), z.string()]);

export const ImageLayerSchema = z.object({
  type: z.literal('image'),
  slot: z.string().optional(),
  src: z.string().optional(),
  fit: z.enum(['cover', 'contain', 'fill']).default('cover'),
  x: DimensionValueSchema.default(0),
  y: DimensionValueSchema.default(0),
  w: DimensionValueSchema.default('100%'),
  h: DimensionValueSchema.default('100%'),
  opacity: z.number().min(0).max(1).default(1),
  borderRadius: z.number().nonnegative().optional(),
});

export const RectLayerSchema = z.object({
  type: z.literal('rect'),
  fill: z.string().optional(),
  stroke: z.string().optional(),
  strokeWidth: z.number().nonnegative().optional(),
  x: DimensionValueSchema.default(0),
  y: DimensionValueSchema.default(0),
  w: DimensionValueSchema.default('100%'),
  h: DimensionValueSchema.default('100%'),
  opacity: z.number().min(0).max(1).default(1),
  borderRadius: z.number().nonnegative().optional(),
});

export const CircleLayerSchema = z.object({
  type: z.literal('circle'),
  fill: z.string().optional(),
  stroke: z.string().optional(),
  strokeWidth: z.number().nonnegative().optional(),
  cx: DimensionValueSchema,
  cy: DimensionValueSchema,
  radius: DimensionValueSchema,
  opacity: z.number().min(0).max(1).default(1),
});

export const TextLayerSchema = z.object({
  type: z.literal('text'),
  slot: z.string().optional(),
  text: z.string().optional(),
  x: DimensionValueSchema.default(0),
  y: DimensionValueSchema.default(0),
  fontSize: z.number().positive().default(32),
  fontFamily: z.string().default('Inter, sans-serif'),
  fontWeight: z.union([z.string(), z.number()]).default('normal'),
  color: z.string().default('#ffffff'),
  maxWidth: DimensionValueSchema.optional(),
  lineHeight: z.number().positive().default(1.3),
  align: z.enum(['left', 'center', 'right']).default('left'),
  prefix: z.string().optional(),
  suffix: z.string().optional(),
  opacity: z.number().min(0).max(1).default(1),
  textTransform: z.enum(['uppercase', 'lowercase', 'none']).default('none'),
});

export const LayerSchema = z.discriminatedUnion('type', [
  ImageLayerSchema,
  RectLayerSchema,
  CircleLayerSchema,
  TextLayerSchema,
]);

export type Layer = z.infer<typeof LayerSchema>;
export type ImageLayer = z.infer<typeof ImageLayerSchema>;
export type RectLayer = z.infer<typeof RectLayerSchema>;
export type CircleLayer = z.infer<typeof CircleLayerSchema>;
export type TextLayer = z.infer<typeof TextLayerSchema>;

export const TemplateSchema = z.object({
  name: z.string(),
  description: z.string().optional(),
  width: z.number().int().positive().default(1080),
  height: z.number().int().positive().default(1080),
  slots: z.array(SlotDefinitionSchema).default([]),
  layers: z.array(LayerSchema).default([]),
});

export type Template = z.infer<typeof TemplateSchema>;
