import React from "react";
import ReactMarkdown from "react-markdown";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faGithub } from "@fortawesome/free-brands-svg-icons";
import { faArrowUpRightFromSquare, faBookOpen, faTag } from "@fortawesome/free-solid-svg-icons";
import { useDraftStore } from "../../../store/draftStore";
import { extractVideoId } from "./utils";

interface GitHubBlogPreviewProps {
  content: string;
}

interface ParsedBlogDocument {
  title: string | null;
  date: string | null;
  layout: string | null;
  tags: string[];
  categories: string[];
  excerpt: string | null;
  body: string;
  hasUnclosedFrontmatter: boolean;
}

function unquoteYaml(val: string): string {
  const trimmed = val.trim();
  if (
    trimmed.length >= 2 &&
    ((trimmed.startsWith('"') && trimmed.endsWith('"')) ||
      (trimmed.startsWith("'") && trimmed.endsWith("'")))
  ) {
    return trimmed.slice(1, -1);
  }
  return trimmed;
}

function parseYamlList(val: string): string[] {
  const trimmed = val.trim();
  if (!trimmed) return [];
  const inner =
    trimmed.startsWith("[") && trimmed.endsWith("]")
      ? trimmed.slice(1, -1)
      : trimmed;
  return inner
    .split(",")
    .map((item) => unquoteYaml(item))
    .filter(Boolean);
}

export function parseBlogDocument(content: string): ParsedBlogDocument {
  const normalized = (content || "").replace(/\r\n/g, "\n");
  const lines = normalized.split("\n");
  let fmLines: string[] = [];
  let body = normalized;
  let hasUnclosedFrontmatter = false;

  if (lines.length > 0 && lines[0].trim() === "---") {
    let closeIdx = -1;
    for (let i = 1; i < lines.length; i++) {
      const t = lines[i].trim();
      if (t === "---" || t === "...") {
        closeIdx = i;
        break;
      }
    }

    if (closeIdx > 0) {
      fmLines = lines.slice(1, closeIdx);
      body = lines.slice(closeIdx + 1).join("\n");
    } else {
      hasUnclosedFrontmatter = true;
    }
  }

  let title: string | null = null;
  let date: string | null = null;
  let layout: string | null = null;
  let excerpt: string | null = null;
  let tags: string[] = [];
  let categories: string[] = [];

  for (const line of fmLines) {
    const match = line.match(/^([A-Za-z0-9_-]+)\s*:\s*(.*)$/);
    if (!match) continue;
    const key = match[1].toLowerCase();
    const rawVal = match[2];
    if (key === "title") title = unquoteYaml(rawVal);
    else if (key === "date") date = unquoteYaml(rawVal);
    else if (key === "layout") layout = unquoteYaml(rawVal);
    else if (key === "excerpt" || key === "description") excerpt = unquoteYaml(rawVal);
    else if (key === "tags") tags = parseYamlList(rawVal);
    else if (key === "categories") categories = parseYamlList(rawVal);
  }

  if (!title) {
    const h1Match = body.match(/^\s*#\s+([^\r\n]+?)(?:\s+#+\s*)?(?:\n|$)/);
    if (h1Match) {
      title = h1Match[1].trim();
      body = body.slice(h1Match[0].length).replace(/^\n+/, "");
    }
  }

  return {
    title,
    date,
    layout,
    tags,
    categories,
    excerpt,
    body: body.trim(),
    hasUnclosedFrontmatter,
  };
}

function estimateReadingMetrics(text: string): { words: number; minutes: number } {
  const words = text
    .replace(/```[\s\S]*?```/g, " ")
    .replace(/[#*_`>\[\]()]/g, " ")
    .split(/\s+/)
    .filter(Boolean).length;
  const minutes = Math.max(1, Math.ceil(words / 200));
  return { words, minutes };
}

export const GitHubBlogPreview: React.FC<GitHubBlogPreviewProps> = ({ content }) => {
  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const blogThread = activeDraft?.threads?.find(
    (t) => t.platform.toLowerCase() === "githubblog"
  );
  const post = blogThread?.posts?.find((p) => p.segmentIndex === 0);

  if (!content || !content.trim()) {
    return (
      <div className="flex h-full items-center justify-center p-8 text-zinc-500">
        No content to preview. Start writing your Markdown article in the editor.
      </div>
    );
  }

  const parsed = parseBlogDocument(content);
  const displayTitle = parsed.title || activeDraft?.title || "Untitled Post";
  const { words, minutes } = estimateReadingMetrics(parsed.body);

  return (
    <div className="max-w-3xl mx-auto py-6 px-5" data-testid="github-blog-preview">
      {parsed.hasUnclosedFrontmatter && (
        <div
          className="mb-4 rounded-xl border border-red-500/40 bg-red-950/30 px-4 py-3 text-xs text-red-300"
          data-testid="github-blog-frontmatter-error"
        >
          YAML frontmatter starts with <code>---</code> but is missing a closing <code>---</code> fence.
        </div>
      )}

      {post?.url && (
        <div
          className="mb-4 flex items-center justify-between rounded-xl border border-emerald-500/30 bg-emerald-500/10 px-4 py-2.5 text-xs text-emerald-300"
          data-testid="github-blog-published-banner"
        >
          <span className="font-mono">Published to GitHub Blog</span>
          <a
            href={post.url}
            target="_blank"
            rel="noreferrer"
            className="inline-flex items-center gap-1.5 font-medium underline hover:opacity-90"
          >
            <span>Open Post</span>
            <FontAwesomeIcon icon={faArrowUpRightFromSquare} className="w-3 h-3" />
          </a>
        </div>
      )}

      <article className="rounded-2xl border border-border bg-panel p-6 sm:p-8 shadow-sm">
        <header className="mb-6 border-b border-border pb-5">
          <div className="mb-3 flex flex-wrap items-center gap-2 text-[11px] font-mono text-muted">
            <span className="inline-flex items-center gap-1.5 rounded-full border border-emerald-500/30 bg-emerald-500/10 px-2.5 py-0.5 text-emerald-300">
              <FontAwesomeIcon icon={faGithub} className="w-3 h-3" />
              <span>{activeDraft?.targetAccountHandle || "Jekyll Blog"}</span>
            </span>
            <span className="inline-flex items-center gap-1">
              <FontAwesomeIcon icon={faBookOpen} className="w-3 h-3" />
              <span>{words} words · ~{minutes} min read</span>
            </span>
            {parsed.date && <span>· {parsed.date}</span>}
            {parsed.layout && <span>· layout: {parsed.layout}</span>}
          </div>

          <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-foreground">
            {displayTitle}
          </h1>

          {parsed.excerpt && (
            <p className="mt-2 text-sm text-muted leading-relaxed">{parsed.excerpt}</p>
          )}

          {(parsed.categories.length > 0 || parsed.tags.length > 0) && (
            <div className="mt-3 flex flex-wrap gap-1.5">
              {parsed.categories.map((cat) => (
                <span
                  key={`cat-${cat}`}
                  className="inline-flex items-center gap-1 rounded bg-accent/15 border border-accent/30 px-2 py-0.5 text-[11px] font-mono text-accent"
                >
                  {cat}
                </span>
              ))}
              {parsed.tags.map((tag) => (
                <span
                  key={`tag-${tag}`}
                  className="inline-flex items-center gap-1 rounded bg-bg border border-border px-2 py-0.5 text-[11px] font-mono text-muted"
                >
                  <FontAwesomeIcon icon={faTag} className="w-2.5 h-2.5" />
                  <span>{tag}</span>
                </span>
              ))}
            </div>
          )}
        </header>

        <div className="prose prose-sm sm:prose-base dark:prose-invert max-w-none break-words">
          <ReactMarkdown
            components={{
              img({ src, alt }) {
                const url = src || "";
                const videoId = extractVideoId(url);
                if (videoId) {
                  return (
                    <a
                      href={url}
                      target="_blank"
                      rel="noreferrer"
                      className="block my-4 overflow-hidden rounded-xl border border-border no-underline"
                    >
                      <img
                        src={`https://img.youtube.com/vi/${videoId}/hqdefault.jpg`}
                        alt={alt || "Watch on YouTube"}
                        className="w-full object-cover m-0"
                      />
                      <span className="block bg-bg/80 px-3 py-1.5 text-xs font-mono text-muted">
                        YouTube: {alt || url}
                      </span>
                    </a>
                  );
                }

                const resolvedSrc = url.startsWith("media://")
                  ? `/api/media/${url.slice("media://".length)}`
                  : url;

                return (
                  <img
                    src={resolvedSrc}
                    alt={alt || ""}
                    className="rounded-lg border border-border max-w-full"
                  />
                );
              },
            }}
          >
            {parsed.body}
          </ReactMarkdown>
        </div>
      </article>
    </div>
  );
};
