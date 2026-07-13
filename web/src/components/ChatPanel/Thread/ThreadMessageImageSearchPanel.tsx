import { useMemo, useState } from "react";
import { importMediaFromUrl } from "../../../api/drafts";
import { useDraftStore } from "../../../store/draftStore";

export type ImageSearchItem = {
  title: string;
  url: string;
  description?: string;
};

export type ParsedImageSearchResult = {
  query: string;
  items: ImageSearchItem[];
};

export function parseImageSearchResult(text: string): ParsedImageSearchResult | null {
  if (!text.toLowerCase().startsWith("image search results for:")) {
    return null;
  }

  const queryMatch = text.match(/Image search results for:\s*'([^']+)'/i);
  const query = queryMatch?.[1]?.trim() || "search";

  const lines = text.split("\n");
  const items: ImageSearchItem[] = [];
  let current: ImageSearchItem | null = null;

  for (const rawLine of lines) {
    const line = rawLine.trim();
    const titleMatch = line.match(/^-\s*\*\*(.+)\*\*$/);
    if (titleMatch) {
      if (current?.url) {
        items.push(current);
      }
      current = { title: titleMatch[1].trim(), url: "" };
      continue;
    }

    if (!current) {
      continue;
    }

    if (line.toLowerCase().startsWith("url:")) {
      const url = line.slice(4).trim();
      if (url.startsWith("http://") || url.startsWith("https://")) {
        current.url = url;
      }
      continue;
    }

    if (line.toLowerCase().startsWith("description:")) {
      current.description = line.slice(12).trim();
      continue;
    }
  }

  if (current?.url) {
    items.push(current);
  }

  if (items.length === 0) {
    return null;
  }

  return { query, items };
}

export function ThreadMessageImageSearchPanel({
  query,
  items,
}: {
  query: string;
  items: ImageSearchItem[];
}) {
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const loadSources = useDraftStore((s) => s.loadSources);
  const loadDrafts = useDraftStore((s) => s.loadDrafts);

  const [modalIndex, setModalIndex] = useState<number | null>(null);
  const [addingUrl, setAddingUrl] = useState<string | null>(null);
  const [addedUrls, setAddedUrls] = useState<Record<string, true>>({});
  const [errorByUrl, setErrorByUrl] = useState<Record<string, string>>({});

  const activeItem = useMemo(() => {
    if (modalIndex === null) {
      return null;
    }
    return items[modalIndex] ?? null;
  }, [items, modalIndex]);

  const addToSources = async (item: ImageSearchItem) => {
    if (!activeDraftId) {
      setErrorByUrl((s) => ({ ...s, [item.url]: "No active draft selected." }));
      return;
    }

    setAddingUrl(item.url);
    setErrorByUrl((s) => {
      const next = { ...s };
      delete next[item.url];
      return next;
    });

    try {
      await importMediaFromUrl(activeDraftId, item.url, item.title);
      await Promise.all([loadSources(activeDraftId), loadDrafts()]);
      setAddedUrls((s) => ({ ...s, [item.url]: true }));
    } catch (err) {
      const message = err instanceof Error ? err.message : "Failed to import image.";
      setErrorByUrl((s) => ({ ...s, [item.url]: message }));
    } finally {
      setAddingUrl(null);
    }
  };

  return (
    <div className="mt-2 rounded-lg border border-border bg-bg/40 p-3">
      <div className="mb-2 flex items-center justify-between">
        <div className="text-xs font-mono uppercase tracking-wider text-muted">Image Search</div>
        <div className="text-xs text-muted">{query}</div>
      </div>

      <div className="flex gap-3 overflow-x-auto pb-1">
        {items.map((item, index) => (
          <div
            key={`${item.url}-${index}`}
            className="w-56 shrink-0 rounded-md border border-border bg-panel p-2"
          >
            <button
              type="button"
              onClick={() => setModalIndex(index)}
              className="block w-full overflow-hidden rounded border border-border"
            >
              <img
                src={item.url}
                alt={item.title}
                className="h-32 w-full object-cover"
                loading="lazy"
              />
            </button>
            <div className="mt-2 text-xs font-semibold text-foreground line-clamp-2">{item.title}</div>
            {item.description ? (
              <div className="mt-1 text-[11px] text-muted line-clamp-2">{item.description}</div>
            ) : null}
            <button
              type="button"
              disabled={addingUrl === item.url || !!addedUrls[item.url]}
              onClick={() => addToSources(item)}
              className="mt-2 w-full rounded bg-accent px-2 py-1 text-xs font-medium text-bg disabled:opacity-50"
            >
              {addedUrls[item.url]
                ? "Added"
                : addingUrl === item.url
                ? "Adding..."
                : "Add to sources"}
            </button>
            {errorByUrl[item.url] ? (
              <div className="mt-1 text-[11px] text-red-300">{errorByUrl[item.url]}</div>
            ) : null}
          </div>
        ))}
      </div>

      {activeItem && modalIndex !== null ? (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 p-4">
          <div className="relative w-full max-w-5xl rounded-lg border border-border bg-panel p-3">
            <div className="mb-2 flex items-center justify-between">
              <div className="text-sm font-semibold text-foreground">{activeItem.title}</div>
              <button
                type="button"
                onClick={() => setModalIndex(null)}
                className="rounded border border-border px-2 py-1 text-xs text-muted"
              >
                Close
              </button>
            </div>
            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => setModalIndex((idx) => (idx === null ? 0 : (idx - 1 + items.length) % items.length))}
                className="rounded border border-border px-2 py-1 text-xs text-muted"
              >
                Prev
              </button>
              <div className="min-h-[20rem] flex-1 overflow-hidden rounded border border-border bg-bg">
                <img src={activeItem.url} alt={activeItem.title} className="h-full max-h-[70vh] w-full object-contain" />
              </div>
              <button
                type="button"
                onClick={() => setModalIndex((idx) => (idx === null ? 0 : (idx + 1) % items.length))}
                className="rounded border border-border px-2 py-1 text-xs text-muted"
              >
                Next
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
