export interface CodeFence {
  raw: string;
  language: string;
  code: string;
}

export interface ParsedSegment {
  cleanText: string;
  images: { alt: string; id: string; raw: string }[];
  youtubeUrls: string[];
  links: { text: string; url: string; raw: string }[];
}
