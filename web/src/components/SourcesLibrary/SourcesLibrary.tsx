import React, { useEffect, useState, useMemo } from "react";
import { Link } from "react-router-dom";
import { useDraftStore } from "../../store/draftStore";
import {
  searchSources,
  fetchSourceById,
  linkSourceToDraft,
  retrySourceTranscription,
  type SourceSearchItemDto,
  type SourceDetailDto
} from "../../api/drafts";
import { SourcePreviewModal } from "../EditorPanel/Sources/SourcePreviewModal";

export const SourcesLibrary: React.FC = () => {
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const drafts = useDraftStore((s) => s.drafts);
  const activeDraft = useMemo(() => drafts.find((d) => d.id === activeDraftId), [drafts, activeDraftId]);
  const activeDraftSources = useDraftStore((s) => s.sources);
  const loadSources = useDraftStore((s) => s.loadSources);

  // Search/filter state
  const [query, setQuery] = useState("");
  const [kind, setKind] = useState<string>("all");
  const [addedAfter, setAddedAfter] = useState("");
  const [addedBefore, setAddedBefore] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 12;

  // Results state
  const [items, setItems] = useState<SourceSearchItemDto[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Detail/Preview state
  const [selectedSource, setSelectedSource] = useState<SourceSearchItemDto | null>(null);
  const [detailItem, setDetailItem] = useState<SourceDetailDto | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [retryingSourceId, setRetryingSourceId] = useState<string | null>(null);
  const [linkingSourceId, setLinkingSourceId] = useState<string | null>(null);

  const fetchResults = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await searchSources(
        query,
        page,
        pageSize,
        kind === "all" ? undefined : kind,
        addedAfter ? new Date(addedAfter).toISOString() : undefined,
        addedBefore ? new Date(addedBefore).toISOString() : undefined
      );
      setItems(res.items);
      setTotal(res.total);
    } catch (err: any) {
      setError(err?.message || "Failed to search sources.");
    } finally {
      setLoading(false);
    }
  };

  // Fetch when page or filters change
  useEffect(() => {
    void fetchResults();
  }, [page, kind, addedAfter, addedBefore]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setPage(1);
    void fetchResults();
  };

  const handleResetFilters = () => {
    setQuery("");
    setKind("all");
    setAddedAfter("");
    setAddedBefore("");
    setPage(1);
  };

  // Load active draft sources on mount to ensure accurate "Linked" check
  useEffect(() => {
    if (activeDraftId) {
      void loadSources(activeDraftId);
    }
  }, [activeDraftId, loadSources]);

  // Open detail preview
  const handleOpenPreview = async (item: SourceSearchItemDto) => {
    setSelectedSource(item);
    setLoadingDetail(true);
    setDetailItem(null);
    try {
      const detail = await fetchSourceById(item.id);
      setDetailItem(detail);
    } catch (err) {
      console.error("Failed to load source detail", err);
    } finally {
      setLoadingDetail(false);
    }
  };

  const handleLinkSource = async (sourceId: string) => {
    if (!activeDraftId) return;
    setLinkingSourceId(sourceId);
    try {
      await linkSourceToDraft(activeDraftId, sourceId);
      // Reload active draft sources
      await loadSources(activeDraftId);
    } catch (err) {
      console.error("Failed to link source", err);
    } finally {
      setLinkingSourceId(null);
    }
  };

  const handleRetryTranscription = async (source: SourceSearchItemDto) => {
    setRetryingSourceId(source.id);
    try {
      await retrySourceTranscription(source.id);
      // reload detail if currently open
      if (selectedSource?.id === source.id) {
        const detail = await fetchSourceById(source.id);
        setDetailItem(detail);
      }
      void fetchResults();
    } catch (err) {
      console.error("Failed to retry transcription", err);
    } finally {
      setRetryingSourceId(null);
    }
  };

  const totalPages = Math.ceil(total / pageSize) || 1;

  return (
    <div className="flex-1 min-h-0 flex flex-col bg-bg text-foreground overflow-y-auto p-6 font-sans">
      <div className="max-w-6xl w-full mx-auto flex flex-col h-full">
        {/* Header */}
        <div className="flex items-center justify-between mb-6 shrink-0">
          <div>
            <h1 className="text-xl font-bold text-foreground">Sources Library</h1>
            <p className="text-xs text-muted mt-1">Browse and search reusable reference materials across all drafts</p>
          </div>
          <Link
            to="/"
            className="flex items-center gap-2 px-4 py-2 bg-panel border border-border hover:bg-zinc-800 text-xs font-semibold rounded-lg shadow-sm transition text-zinc-300"
          >
            &larr; Back to Composer
          </Link>
        </div>

        {/* Search & Filter Form */}
        <form onSubmit={handleSearchSubmit} className="bg-panel border border-border rounded-xl p-4 mb-6 shrink-0 grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
          <div className="md:col-span-2">
            <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Keyword Search</label>
            <input
              type="text"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Search content, titles, or URLs..."
              className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
            />
          </div>

          <div>
            <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Source Type</label>
            <select
              value={kind}
              onChange={(e) => {
                setKind(e.target.value);
                setPage(1);
              }}
              className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
            >
              <option value="all">All Types</option>
              <option value="Url">Web URL</option>
              <option value="YouTube">YouTube Video</option>
              <option value="File">Document / File</option>
            </select>
          </div>

          <div className="flex gap-2">
            <button
              type="submit"
              className="flex-1 rounded-lg bg-accent text-bg px-3 py-2 text-xs font-bold transition hover:opacity-90"
            >
              Search
            </button>
            <button
              type="button"
              onClick={handleResetFilters}
              className="rounded-lg border border-border px-3 py-2 text-xs font-semibold text-muted transition hover:bg-zinc-800"
            >
              Reset
            </button>
          </div>

          <div>
            <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Added After</label>
            <input
              type="date"
              value={addedAfter}
              onChange={(e) => {
                setAddedAfter(e.target.value);
                setPage(1);
              }}
              className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
            />
          </div>

          <div>
            <label className="block text-[10px] font-mono uppercase tracking-wider text-muted mb-1.5">Added Before</label>
            <input
              type="date"
              value={addedBefore}
              onChange={(e) => {
                setAddedBefore(e.target.value);
                setPage(1);
              }}
              className="w-full rounded-lg border border-border bg-bg px-3 py-2 text-xs text-foreground outline-none focus:border-accent"
            />
          </div>
        </form>

        {/* Results Info */}
        <div className="flex items-center justify-between text-xs text-muted mb-4 shrink-0 font-mono">
          <div>
            {loading ? "Searching library..." : `Found ${total} source(s)`}
          </div>
          <div>
            Page {page} of {totalPages}
          </div>
        </div>

        {/* Results list */}
        <div className="flex-1 min-h-0 overflow-y-auto mb-6">
          {error && (
            <div className="p-4 bg-red-950/20 border border-red-900/50 rounded-xl text-xs text-red-400 font-mono mb-4">
              {error}
            </div>
          )}

          {loading ? (
            <div className="flex flex-col items-center justify-center py-20 gap-3 text-muted">
              <span className="w-8 h-8 border-4 border-muted border-t-accent rounded-full animate-spin" />
              <span className="text-xs font-mono">Searching library...</span>
            </div>
          ) : items.length === 0 ? (
            <div className="text-center py-20 border border-dashed border-border rounded-xl text-muted text-sm italic">
              No matching sources found in the library. Try adjusting your query or filters.
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {items.map((item) => {
                const isLinked = activeDraftSources.some((s) => s.id === item.id);
                return (
                  <div
                    key={item.id}
                    onClick={() => void handleOpenPreview(item)}
                    className="bg-panel border border-border hover:border-zinc-700 rounded-xl p-4 flex flex-col justify-between cursor-pointer transition select-none group relative"
                  >
                    <div>
                      {/* Top bar with type tag & date */}
                      <div className="flex items-center justify-between gap-2 mb-2">
                        <span className="text-[9px] font-mono uppercase tracking-wider bg-bg border border-border px-2 py-0.5 rounded-full text-accent">
                          {item.kind}
                        </span>
                        <span className="text-[10px] text-muted font-mono">
                          {new Date(item.addedAt).toLocaleDateString()}
                        </span>
                      </div>

                      <h3 className="text-xs font-bold text-foreground line-clamp-2 group-hover:text-accent transition mb-1">
                        {item.title || item.reference}
                      </h3>
                      <p className="text-[10px] text-muted truncate font-mono mb-3">
                        {item.reference}
                      </p>

                      {item.summary && (
                        <p className="text-[11px] text-zinc-400 line-clamp-3 leading-relaxed mb-4">
                          {item.summary}
                        </p>
                      )}
                    </div>

                    {/* Footer link action */}
                    <div className="mt-auto pt-3 border-t border-border/40 flex items-center justify-between gap-3">
                      <span className="text-[10px] text-indigo-500 font-semibold group-hover:underline">
                        View detail &rarr;
                      </span>

                      {activeDraftId && (
                        <button
                          onClick={(e) => {
                            e.stopPropagation();
                            void handleLinkSource(item.id);
                          }}
                          disabled={isLinked || linkingSourceId === item.id}
                          className="px-2.5 py-1 text-[10px] font-bold rounded-lg border border-border bg-bg hover:bg-zinc-800 text-zinc-300 disabled:opacity-40 transition"
                        >
                          {isLinked ? "Linked" : linkingSourceId === item.id ? "Linking..." : "Link to Draft"}
                        </button>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Pagination bar */}
        {totalPages > 1 && (
          <div className="flex items-center justify-center gap-4 shrink-0 py-4 border-t border-border/40">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
              className="px-4 py-2 border border-border bg-panel hover:bg-zinc-800 rounded-lg text-xs font-semibold disabled:opacity-40 transition"
            >
              &larr; Previous
            </button>
            <span className="text-xs text-muted font-mono">
              Page {page} of {totalPages}
            </span>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page === totalPages}
              className="px-4 py-2 border border-border bg-panel hover:bg-zinc-800 rounded-lg text-xs font-semibold disabled:opacity-40 transition"
            >
              Next &rarr;
            </button>
          </div>
        )}
      </div>

      {/* Detail Preview Modal */}
      {selectedSource && (
        <SourcePreviewModal
          item={{
            kind: "source",
            source: {
              id: selectedSource.id,
              draftId: activeDraftId || "",
              kind: selectedSource.kind,
              reference: selectedSource.reference,
              title: selectedSource.title,
              summary: selectedSource.summary,
              transcriptStatus: selectedSource.transcriptStatus,
              addedAt: selectedSource.addedAt
            },
            detail: detailItem,
            loading: loadingDetail
          }}
          canRetryTranscription={
            selectedSource.kind === "YouTube" &&
            (detailItem?.transcriptStatus || selectedSource.transcriptStatus) !== "Processing"
          }
          retryingTranscription={retryingSourceId === selectedSource.id}
          onRetryTranscription={() => void handleRetryTranscription(selectedSource)}
          onClose={() => {
            setSelectedSource(null);
            setDetailItem(null);
          }}
        />
      )}
    </div>
  );
};
