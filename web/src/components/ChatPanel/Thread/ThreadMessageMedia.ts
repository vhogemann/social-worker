const MEDIA_URI_REGEX = /^media:\/\/([0-9a-fA-F-]{36})$/;
const MEDIA_URI_IN_TEXT_REGEX = /media:\/\/([0-9a-fA-F-]{36})/g;

export function resolveMediaUri(uri?: string | null): string {
  if (!uri) return "";
  const match = uri.match(MEDIA_URI_REGEX);
  if (!match) return uri;
  return `/api/media/${match[1]}`;
}

export function rewriteMediaUris(text: string): string {
  return text.replace(MEDIA_URI_IN_TEXT_REGEX, (_match, guid: string) => `/api/media/${guid}`);
}
