import { z } from 'zod';

export const DIAGRAM_THEMES = [
  'dark',
  'light',
  'zinc-light',
  'zinc-dark',
  'tokyo-night',
  'tokyo-night-storm',
  'tokyo-night-light',
  'catppuccin-mocha',
  'catppuccin-latte',
  'nord',
  'nord-light',
  'dracula',
  'github-light',
  'github-dark',
  'solarized-light',
  'solarized-dark',
  'one-dark',
] as const;

export const DiagramThemeSchema = z.enum(DIAGRAM_THEMES);

export const DiagramAspectRatioSchema = z.enum(['auto', '16:9', '1:1', '4:5']);

export const DiagramRenderSchema = z.object({
  code: z.string().min(1).max(20000),
  title: z.string().max(120).optional(),
  subtitle: z.string().max(200).optional(),
  theme: DiagramThemeSchema.default('dark'),
  aspectRatio: DiagramAspectRatioSchema.default('auto'),
  cardStyle: z.boolean().default(true),
});

export type DiagramTheme = z.infer<typeof DiagramThemeSchema>;
export type DiagramAspectRatio = z.infer<typeof DiagramAspectRatioSchema>;
export type DiagramRenderSpec = z.infer<typeof DiagramRenderSchema>;
