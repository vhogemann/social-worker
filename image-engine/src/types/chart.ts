import { z } from 'zod';

export const ChartTypeSchema = z.enum([
  'bar',
  'horizontal_bar',
  'line',
  'area',
  'doughnut',
  'pie'
]);

export const ChartDatasetSchema = z.object({
  label: z.string().nullish(),
  data: z.array(z.number()),
  backgroundColor: z.union([z.string(), z.array(z.string())]).nullish(),
  borderColor: z.union([z.string(), z.array(z.string())]).nullish(),
  fill: z.boolean().nullish(),
});

export const ChartRenderSchema = z.object({
  type: ChartTypeSchema,
  labels: z.array(z.string()).min(1),
  datasets: z.array(ChartDatasetSchema).min(1),
  title: z.string().nullish(),
  subtitle: z.string().nullish(),
  theme: z.enum(['dark', 'light']).default('dark'),
  aspectRatio: z.enum(['16:9', '1:1', '4:5']).default('16:9'),
  cardStyle: z.boolean().default(true),
});

export type ChartType = z.infer<typeof ChartTypeSchema>;
export type ChartDataset = z.infer<typeof ChartDatasetSchema>;
export type ChartRenderSpec = z.infer<typeof ChartRenderSchema>;
