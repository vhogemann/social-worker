import React, { useState } from "react";
import { useDraftStore } from "../../store/draftStore";

interface ThreadPreviewProps {
  content: string;
}

const MEDIA_REGEX = /!\[(.*?)\]\(media:\/\/([0-9a-fA-F\-]{36})\)/g;
const YOUTUBE_REGEX = /!\[(.*?)\]\((https?:\/\/(?:www\.)?youtube\.com\/watch\?v=[\w-]+|https?:\/\/youtu\.be\/[\w-]+)\)/g;
const MD_LINK_REGEX = /\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g;
const RAW_LINK_REGEX = /(?<!\]\()https?:\/\/[^\s]+(?!\))/g;

function extractVideoId(url: string): string | null {
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

interface ParsedSegment {
  cleanText: string;
  images: { alt: string; id: string; raw: string }[];
  youtubeUrls: string[];
  links: { text: string; url: string; raw: string }[];
}

function parseSegment(text: string): ParsedSegment {
  const images: { alt: string; id: string; raw: string }[] = [];
  const youtubeUrls: string[] = [];
  const links: { text: string; url: string; raw: string }[] = [];

  let match;
  let textWithoutYt = text;
  YOUTUBE_REGEX.lastIndex = 0;
  while ((match = YOUTUBE_REGEX.exec(text)) !== null) {
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

export const ThreadPreview: React.FC<ThreadPreviewProps> = ({ content }) => {
  const rawSegments = content.split(/\n---\n|\r\n---\r\n|\n---\r\n|\r\n---\n/);
  const segments = rawSegments.map((s) => s.trim()).filter((s) => s.length > 0);

  if (segments.length === 0) {
    return (
      <div className="flex h-full items-center justify-center p-8 text-zinc-500">
        No content to preview. Start writing in the editor to see your thread.
      </div>
    );
  }

  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);
  const blueskyThread = activeDraft?.threads?.find(t => t.platform === "Bluesky");

  return (
    <div className="space-y-0 relative max-w-xl mx-auto py-6 px-4">
      {segments.map((segment, index) => {
        const post = blueskyThread?.posts?.find(p => p.segmentIndex === index);
        return (
          <PreviewCard
            key={index}
            content={segment}
            index={index}
            total={segments.length}
            isLast={index === segments.length - 1}
            postUrl={post?.url}
          />
        );
      })}
    </div>
  );
};

interface PreviewCardProps {
  content: string;
  index: number;
  total: number;
  isLast: boolean;
  postUrl?: string;
}

const AltTextEditor: React.FC<{ mediaId: string; initialAlt: string }> = ({ mediaId, initialAlt }) => {
  const [isEditing, setIsEditing] = useState(false);
  const [alt, setAlt] = useState(initialAlt);
  const updateMediaAltText = useDraftStore((s) => s.updateMediaAltText);

  const handleSave = async () => {
    try {
      await updateMediaAltText(mediaId, alt);
      setIsEditing(false);
    } catch (err) {
      console.error("Failed to save alt text:", err);
    }
  };

  if (isEditing) {
    return (
      <div className="absolute inset-x-0 bottom-0 bg-black/80 p-2 flex items-center gap-2">
        <input
          type="text"
          value={alt}
          onChange={(e) => setAlt(e.target.value)}
          placeholder="Describe this image for screen readers..."
          className="flex-1 px-2.5 py-1 bg-zinc-900 text-white text-xs rounded border border-zinc-700 focus:outline-none focus:border-indigo-500"
          autoFocus
        />
        <button onClick={handleSave} className="px-2 py-1 bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-semibold rounded">
          Save
        </button>
        <button onClick={() => setIsEditing(false)} className="px-2 py-1 bg-zinc-800 hover:bg-zinc-700 text-white text-xs font-semibold rounded">
          Cancel
        </button>
      </div>
    );
  }

  return (
    <div className="absolute bottom-2 left-2 flex items-center gap-1.5 opacity-90 hover:opacity-100 transition">
      <button
        onClick={() => setIsEditing(true)}
        className="px-2 py-1 rounded bg-black/60 backdrop-blur-sm text-white text-[10px] font-semibold flex items-center gap-1 shadow hover:bg-black/80"
      >
        <span>ALT</span>
        <span className="text-zinc-300 truncate max-w-[120px]">
          {initialAlt ? `: ${initialAlt}` : " (missing)"}
        </span>
      </button>
    </div>
  );
};

const PreviewCard: React.FC<PreviewCardProps> = ({ content, index, total, isLast, postUrl }) => {
  const [copied, setCopied] = useState(false);
  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);

  const { cleanText, images, youtubeUrls, links } = parseSegment(content);
  const hasConflict = images.length > 0 && youtubeUrls.length > 0;

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(cleanText);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch (err) {
      console.error("Failed to copy text: ", err);
    }
  };

  return (
    <div className="relative flex items-start gap-4 pb-8 group">
      {!isLast && (
        <div className="absolute left-[20px] top-[40px] bottom-0 w-[2px] bg-zinc-200 dark:bg-zinc-800" />
      )}

      <div className="relative z-10 flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-indigo-500 to-purple-600 text-white font-semibold text-sm shadow-sm select-none">
        U
      </div>

      <div className="flex-1 min-w-0 bg-white dark:bg-zinc-950 border border-zinc-200 dark:border-zinc-800 rounded-2xl p-4 shadow-sm group-hover:border-zinc-300 dark:group-hover:border-zinc-700 transition duration-150 relative">
        <div className="flex items-center justify-between gap-2 mb-2">
          <div className="flex items-center gap-1.5 min-w-0">
            <span className="font-semibold text-zinc-900 dark:text-zinc-50 truncate text-sm">
              You
            </span>
            <span className="text-zinc-500 text-xs truncate">
              @social_worker
            </span>
            <span className="text-zinc-400 dark:text-zinc-600 text-[10px]">•</span>
            <span className="text-xs font-medium text-indigo-600 dark:text-indigo-400 shrink-0">
              {index + 1}/{total}
            </span>
          </div>

          <button
            onClick={handleCopy}
            className={`flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-medium border transition-colors select-none ${
              copied
                ? "bg-emerald-50 dark:bg-emerald-950/20 text-emerald-600 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800/50"
                : "bg-zinc-50 dark:bg-zinc-900 text-zinc-600 dark:text-zinc-400 border-zinc-200 dark:border-zinc-800 hover:bg-zinc-100 dark:hover:bg-zinc-800"
            }`}
          >
            {copied ? (
              <>
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  viewBox="0 0 20 20"
                  fill="currentColor"
                  className="w-3.5 h-3.5"
                >
                  <path
                    fillRule="evenodd"
                    d="M16.704 4.153a.75.75 0 0 1 .143 1.052l-8 10.5a.75.75 0 0 1-1.127.075l-4.5-4.5a.75.75 0 0 1 1.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 0 1 1.05-.143Z"
                    clipRule="evenodd"
                  />
                </svg>
                <span>Copied</span>
              </>
            ) : (
              <>
                <svg
                  xmlns="http://www.w3.org/2000/svg"
                  fill="none"
                  viewBox="0 0 24 24"
                  strokeWidth={1.8}
                  stroke="currentColor"
                  className="w-3.5 h-3.5"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    d="M8.25 7.5V6.108c0-1.135.845-2.098 1.976-2.192.373-.03.748-.057 1.123-.08M15.75 18H18a3 3 0 0 0 3-3V7.5M19.5 3.75c-.373.03-.748.057-1.123.08M18 7.5a3 3 0 0 0-3-3M18 7.5V18m-9-9h9M9 9a3 3 0 0 0-3 3v6a3 3 0 0 0 3 3h6a3 3 0 0 0 3-3V12a3 3 0 0 0-3-3H9Z"
                  />
                </svg>
                <span>Copy</span>
              </>
            )}
          </button>
        </div>

        {postUrl && (
          <a
            href={postUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="mb-3 inline-flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-700 dark:text-indigo-400 dark:hover:text-indigo-300"
          >
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor" className="w-3.5 h-3.5">
              <path fillRule="evenodd" d="M4.25 5.5a.75.75 0 00-.75.75v8.5c0 .414.336.75.75.75h8.5a.75.75 0 00.75-.75v-4a.75.75 0 011.5 0v4A2.25 2.25 0 0112.75 17h-8.5A2.25 2.25 0 012 14.75v-8.5A2.25 2.25 0 014.25 4h5a.75.75 0 010 1.5h-5z" clipRule="evenodd" />
              <path fillRule="evenodd" d="M6.194 12.753a.75.75 0 001.06.053L16.5 4.44v2.81a.75.75 0 001.5 0v-4.5a.75.75 0 00-.75-.75h-4.5a.75.75 0 000 1.5h2.553l-9.056 8.194a.75.75 0 00-.053 1.06z" clipRule="evenodd" />
            </svg>
            View on Bluesky
          </a>
        )}

        {cleanText && (
          <div className="text-zinc-800 dark:text-zinc-200 text-sm whitespace-pre-wrap break-words leading-relaxed select-text">
            {cleanText}
          </div>
        )}

        {images.map((img) => {
          const asset = activeDraft?.mediaAssets?.find((m) => m.id === img.id);
          const currentAlt = asset?.altText ?? img.alt ?? "";

          return (
            <div key={img.id} className="mt-3 relative group/img rounded-xl overflow-hidden border border-zinc-200 dark:border-zinc-800 bg-zinc-50 dark:bg-zinc-900">
              <img
                src={`/api/media/${img.id}`}
                alt={currentAlt}
                className="max-h-72 w-full object-cover"
              />
              <AltTextEditor mediaId={img.id} initialAlt={currentAlt} />
            </div>
          );
        })}

        {youtubeUrls.map((url) => {
          const videoId = extractVideoId(url);
          if (!videoId) return null;

          const thumbnail = `https://img.youtube.com/vi/${videoId}/hqdefault.jpg`;
          return (
            <a
              key={url}
              href={url}
              target="_blank"
              rel="noopener noreferrer"
              className="mt-3 block overflow-hidden rounded-xl border border-zinc-200 dark:border-zinc-800 bg-zinc-50 dark:bg-zinc-900 hover:opacity-95 transition relative group/yt"
            >
              <div className="relative aspect-video w-full bg-black flex items-center justify-center">
                <img
                  src={thumbnail}
                  alt="YouTube Video Thumbnail"
                  className="w-full h-full object-cover opacity-90 group-hover/yt:scale-105 transition duration-300"
                />
                <div className="absolute h-12 w-12 rounded-full bg-red-600/90 flex items-center justify-center shadow-lg group-hover/yt:bg-red-600 group-hover/yt:scale-110 transition duration-150">
                  <svg className="w-6 h-6 text-white ml-0.5" fill="currentColor" viewBox="0 0 24 24">
                    <path d="M8 5v14l11-7z" />
                  </svg>
                </div>
              </div>
              <div className="p-3 border-t border-zinc-200 dark:border-zinc-800">
                <div className="text-xs text-zinc-500 font-mono">youtube.com</div>
                <div className="text-sm font-semibold text-zinc-800 dark:text-zinc-200 truncate mt-0.5">
                  Watch YouTube Video
                </div>
              </div>
            </a>
          );
        })}

        {links.length === 1 && (
          (() => {
            const link = links[0];
            let domain = "";
            try {
              domain = new URL(link.url).hostname;
            } catch {
              domain = "link";
            }

            const source = activeDraft?.sources?.find(
              (s) => s.reference.toLowerCase() === link.url.toLowerCase()
            );

            const displayTitle = source?.title || link.text || "View Link";

            return (
              <a
                href={link.url}
                target="_blank"
                rel="noopener noreferrer"
                className="mt-3 block overflow-hidden rounded-xl border border-zinc-200 dark:border-zinc-800 bg-zinc-50 dark:bg-zinc-900 hover:opacity-95 transition relative group/link"
              >
                <div className="p-3">
                  <div className="text-xs text-zinc-500 font-mono">{domain}</div>
                  <div className="text-sm font-semibold text-zinc-800 dark:text-zinc-200 truncate mt-0.5">
                    {displayTitle}
                  </div>
                </div>
              </a>
            );
          })()
        )}

        {hasConflict && (
          <div className="mt-3 px-3 py-2 bg-amber-500/10 border border-amber-500/20 text-amber-600 dark:text-amber-400 text-xs rounded-xl flex items-center gap-1.5 font-medium select-none">
            <svg className="w-4 h-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
            <span>Bluesky: only images OR one YouTube embed is allowed per post.</span>
          </div>
        )}
      </div>
    </div>
  );
};
