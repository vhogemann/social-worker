import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faArrowLeft, faBookOpen, faSpinner } from "@fortawesome/free-solid-svg-icons";
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
import { SourcesFilterBar } from "./SourcesFilterBar";
import { SourceGridCard } from "./SourceGridCard";
import { Pagination } from "../common/Pagination";
import { AlertBanner } from "../common/AlertBanner";
import { EmptyState } from "../common/EmptyState";

export const SourcesLibrary: React.FC = () => {
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
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
            className="flex items-center gap-2 px-4 py-2 bg-panel border border-border hover:bg-border/60 text-xs font-semibold rounded-lg shadow-sm transition text-muted hover:text-foreground"
          >
            <FontAwesomeIcon icon={faArrowLeft} />
            <span>Back to Composer</span>
          </Link>
        </div>

        {/* Search & Filter Form */}
        <SourcesFilterBar
          query={query}
          kind={kind}
          addedAfter={addedAfter}
          addedBefore={addedBefore}
          onChangeQuery={setQuery}
          onChangeKind={(val) => {
            setKind(val);
            setPage(1);
          }}
          onChangeAddedAfter={(val) => {
            setAddedAfter(val);
            setPage(1);
          }}
          onChangeAddedBefore={(val) => {
            setAddedBefore(val);
            setPage(1);
          }}
          onSubmit={handleSearchSubmit}
          onReset={handleResetFilters}
        />

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
            <AlertBanner variant="error" message={error} onDismiss={() => setError(null)} className="mb-4" />
          )}

          {loading ? (
            <div className="flex flex-col items-center justify-center py-20 gap-3 text-muted">
              <FontAwesomeIcon icon={faSpinner} spin className="text-2xl" />
              <span className="text-xs font-mono">Searching library...</span>
            </div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={faBookOpen}
              title="No Matching Sources"
              description="No sources found matching your query or filters."
            />
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {items.map((item) => (
                <SourceGridCard
                  key={item.id}
                  item={item}
                  isLinked={activeDraftSources.some((s) => s.id === item.id)}
                  isLinking={linkingSourceId === item.id}
                  hasActiveDraft={Boolean(activeDraftId)}
                  onOpenPreview={handleOpenPreview}
                  onLinkSource={handleLinkSource}
                />
              ))}
            </div>
          )}
        </div>

        {/* Pagination bar */}
        <Pagination
          currentPage={page}
          totalPages={totalPages}
          totalItems={total}
          onPageChange={setPage}
          className="pb-4"
        />
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
              processingStatus: selectedSource.processingStatus,
              addedAt: selectedSource.addedAt
            },
            detail: detailItem,
            loading: loadingDetail
          }}
          canRetryTranscription={
            selectedSource.kind === "YouTube" &&
            (detailItem?.processingStatus || selectedSource.processingStatus) !== "Processing"
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
