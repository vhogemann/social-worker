import { useState } from "react";
import { importSourceFromUrl } from "../../../api/drafts";
import { useDraftStore } from "../../../store/draftStore";

export type SearchResultItem = {
  rank?: number;
  title: string;
  url: string;
  snippet?: string;
};

export type ParsedSearchResults = {
  query: string;
  results: SearchResultItem[];
};

export function parseWebSearchResults(text: string): ParsedSearchResults | null {
  const trimmed = text.trim();
  if (!trimmed.startsWith("{")) {
    return null;
  }

  try {
    const payload = JSON.parse(trimmed) as {
      query?: string;
      results?: Array<{ rank?: number; title?: string; url?: string; snippet?: string }>;
    };

    if (!payload || !Array.isArray(payload.results) || payload.results.length === 0) {
      return null;
    }

    const results = payload.results
      .filter((r) => typeof r.url === "string" && /^(https?:)\/\//i.test(r.url) && typeof r.title === "string")
      .map((r) => ({
        rank: r.rank,
        title: r.title as string,
        url: r.url as string,
        snippet: r.snippet,
      }));

    if (results.length === 0) {
      return null;
    }

    return {
      query: payload.query?.trim() || "search",
      results,
    };
  } catch {
    return null;
  }
}

export function ThreadMessageSearchResultsPanel({
  query,
  results,
}: {
  query: string;
  results: SearchResultItem[];
}) {
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const loadSources = useDraftStore((s) => s.loadSources);

  const [addingUrl, setAddingUrl] = useState<string | null>(null);
  const [addedUrls, setAddedUrls] = useState<Record<string, true>>({});
  const [errorByUrl, setErrorByUrl] = useState<Record<string, string>>({});

  const addToSources = async (result: SearchResultItem) => {
    if (!activeDraftId) {
      setErrorByUrl((s) => ({ ...s, [result.url]: "No active draft selected." }));
      return;
    }

    setAddingUrl(result.url);
    setErrorByUrl((s) => {
      const next = { ...s };
      delete next[result.url];
      return next;
    });

    try {
      await importSourceFromUrl(activeDraftId, result.url, result.title);
      await loadSources(activeDraftId);
      setAddedUrls((s) => ({ ...s, [result.url]: true }));
    } catch (err) {
      const message = err instanceof Error ? err.message : "Failed to add source.";
      setErrorByUrl((s) => ({ ...s, [result.url]: message }));
    } finally {
      setAddingUrl(null);
    }
  };

  return (
    <div className="mt-2 rounded-lg border border-border bg-bg/40 p-3">
      <div className="mb-2 flex items-center justify-between">
        <div className="text-xs font-mono uppercase tracking-wider text-muted">Web Search</div>
        <div className="text-xs text-muted">{query}</div>
      </div>

      <div className="space-y-2">
        {results.map((result, index) => (
          <div key={`${result.url}-${index}`} className="rounded-md border border-border bg-panel p-2">
            <div className="flex items-start justify-between gap-2">
              <a
                href={result.url}
                target="_blank"
                rel="noreferrer"
                className="text-sm font-semibold text-foreground hover:text-accent"
              >
                {result.rank ? `${result.rank}. ` : ""}
                {result.title}
              </a>
              <button
                type="button"
                disabled={addingUrl === result.url || !!addedUrls[result.url]}
                onClick={() => addToSources(result)}
                className="rounded bg-accent px-2 py-1 text-xs font-medium text-bg disabled:opacity-50"
              >
                {addedUrls[result.url] ? "Added" : addingUrl === result.url ? "Adding..." : "Add to sources"}
              </button>
            </div>
            <div className="mt-1 break-all text-[11px] text-muted">{result.url}</div>
            {result.snippet ? <div className="mt-1 text-xs text-muted">{result.snippet}</div> : null}
            {errorByUrl[result.url] ? (
              <div className="mt-1 text-[11px] text-red-300">{errorByUrl[result.url]}</div>
            ) : null}
          </div>
        ))}
      </div>
    </div>
  );
}
