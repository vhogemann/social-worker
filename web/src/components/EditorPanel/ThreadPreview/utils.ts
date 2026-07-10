import { CodeFence, ParsedSegment } from "./types";

export const MEDIA_REGEX = /!\[([\s\S]*?)\]\(media:\/\/([0-9a-fA-F\-]{36})\)/g;
export const YOUTUBE_REGEX = /!\[(.*?)\]\((https?:\/\/(?:www\.)?youtube\.com\/watch\?v=[\w-]+|https?:\/\/youtu\.be\/[\w-]+)\)/g;
export const MD_LINK_REGEX = /\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g;
export const RAW_LINK_REGEX = /(?<!\]\()https?:\/\/[^\s]+(?!\))/g;
export const CODE_FENCE_REGEX = /```(\w*)\r?\n([\s\S]*?)```/g;

export function extractCodeFences(text: string): CodeFence[] {
  const fences: CodeFence[] = [];
  CODE_FENCE_REGEX.lastIndex = 0;
  let m: RegExpExecArray | null;
  while ((m = CODE_FENCE_REGEX.exec(text)) !== null) {
    fences.push({ raw: m[0], language: m[1].trim(), code: m[2].trimEnd() });
  }
  return fences;
}

export function isCodeFenceAltText(alt: string): boolean {
  return alt.startsWith("```") && alt.endsWith("```");
}

export function extractCodeFenceFromAltText(alt: string): string | null {
  if (!isCodeFenceAltText(alt)) return null;
  return alt;
}

export function extractVideoId(url: string): string | null {
  if (url.includes("youtube.com/watch")) {
    const match = url.match(/[?&]v=([\w-]+)/);
    return match ? match[1] : null;
  }
  if (url.includes("youtu.be/")) {
    const parts = url.split("youtu.be/");
    if (parts.length > 1) {
      return parts[1].split("?")[0];
    }
  }
  return null;
}

export function parseSegment(text: string): ParsedSegment {
  const images: { alt: string; id: string; raw: string }[] = [];
  const youtubeUrls: string[] = [];
  const links: { text: string; url: string; raw: string }[] = [];

  let textWithoutFences = text;
  CODE_FENCE_REGEX.lastIndex = 0;
  let m: RegExpExecArray | null;
  while ((m = CODE_FENCE_REGEX.exec(text)) !== null) {
    textWithoutFences = textWithoutFences.replace(m[0], "");
  }

  let match;
  let textWithoutYt = textWithoutFences;
  YOUTUBE_REGEX.lastIndex = 0;
  while ((match = YOUTUBE_REGEX.exec(textWithoutFences)) !== null) {
    youtubeUrls.push(match[2]);
    textWithoutYt = textWithoutYt.replace(match[0], "");
  }

  let textWithoutImages = textWithoutYt;
  MEDIA_REGEX.lastIndex = 0;
  while ((match = MEDIA_REGEX.exec(textWithoutYt)) !== null) {
    images.push({ alt: match[1], id: match[2], raw: match[0] });
    textWithoutImages = textWithoutImages.replace(match[0], "");
  }

  let textWithoutMdLinks = textWithoutImages;
  MD_LINK_REGEX.lastIndex = 0;
  while ((match = MD_LINK_REGEX.exec(textWithoutImages)) !== null) {
    links.push({ text: match[1], url: match[2], raw: match[0] });
    textWithoutMdLinks = textWithoutMdLinks.replace(match[0], match[1]);
  }

  RAW_LINK_REGEX.lastIndex = 0;
  while ((match = RAW_LINK_REGEX.exec(textWithoutMdLinks)) !== null) {
    const url = match[0];
    if (!youtubeUrls.includes(url) && !links.some(l => l.url === url)) {
      links.push({ text: url, url, raw: url });
    }
  }

  return {
    cleanText: textWithoutMdLinks.trim(),
    images,
    youtubeUrls,
    links,
  };
}
