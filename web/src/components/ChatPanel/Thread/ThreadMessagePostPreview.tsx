import ReactMarkdown from "react-markdown";
import { splitIntoSegments } from "../../EditorPanel/ThreadPreview/utils";
import { resolveMediaUri, rewriteMediaUris } from "./ThreadMessageMedia";

export type ParsedPostPreview = {
  posts: string[];
};

const POST_HEADER_REGEX = /(?:^|\n)\s*(?:\*\*)?Post\s+\d+\s*:\s*(?:\*\*)?/gi;
const TRAILING_META_LINE_REGEX =
  /^(The thread is ready for publication|Let me know if you would like me to publish|if you need any adjustments)/i;

function sanitizePostBody(post: string): string {
  const lines = post.split("\n");

  while (lines.length > 0) {
    const last = (lines[lines.length - 1] ?? "").trim();
    if (last === "" || last === "---" || TRAILING_META_LINE_REGEX.test(last)) {
      lines.pop();
      continue;
    }
    break;
  }

  return lines.join("\n").trim();
}

function extractPostsByHeaders(text: string): string[] {
  const posts: string[] = [];
  const matches = Array.from(text.matchAll(POST_HEADER_REGEX));
  if (matches.length < 2) return posts;

  for (let i = 0; i < matches.length; i++) {
    const current = matches[i];
    const next = matches[i + 1];
    const start = (current.index ?? 0) + current[0].length;
    const end = next?.index ?? text.length;
    const raw = text.slice(start, end).trim();
    const cleaned = sanitizePostBody(raw);
    if (cleaned) posts.push(cleaned);
  }

  return posts;
}

export function parsePostPreview(text: string): ParsedPostPreview | null {
  const byHeaders = extractPostsByHeaders(text);
  if (byHeaders.length >= 2) return { posts: byHeaders };

  const posts = splitIntoSegments(text).map(sanitizePostBody).filter(Boolean);

  if (posts.length < 2) return null;
  return { posts };
}

export function ThreadMessagePostPreview({ posts }: { posts: string[] }) {
  return (
    <div className="space-y-3">
      {posts.map((post, index) => (
        <div key={index} className="rounded-xl border border-border bg-panel p-3">
          <div className="mb-2 text-[10px] font-mono uppercase tracking-wider text-muted">Post {index + 1}</div>
          <div className="prose prose-invert prose-sm max-w-none">
            <ReactMarkdown
              components={{
                img: ({ src, alt }) => (
                  <img
                    src={resolveMediaUri(src)}
                    alt={alt ?? ""}
                    loading="lazy"
                    className="h-44 w-full rounded-md border border-border object-cover object-center"
                  />
                ),
                a: ({ href, children }) => (
                  <a href={resolveMediaUri(href)} target="_blank" rel="noreferrer">
                    {children}
                  </a>
                ),
              }}
            >
              {rewriteMediaUris(post)}
            </ReactMarkdown>
          </div>
        </div>
      ))}
    </div>
  );
}
