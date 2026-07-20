import { useEffect, useMemo, useState } from "react";
import { useDraftStore } from "../../../store/draftStore";
import { useEditorStore } from "../../../store/editorStore";
import {
  fetchSourceDetail,
  fetchSourceStatus,
  retrySourceTranscription,
  deleteSource,
  linkSourceToDraft,
  searchSources,
  type SourceDto,
  type SourceDetailDto,
  type SourceSearchItemDto,
  type MediaAssetDto,
} from "../../../api/drafts";

export function useSourcesPanelManager() {
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const drafts = useDraftStore((s) => s.drafts);
  const sources = useDraftStore((s) => s.sources);
  const loadSources = useDraftStore((s) => s.loadSources);
  const uploadFileSource = useDraftStore((s) => s.uploadFileSource);
  const deleteMediaAsset = useDraftStore((s) => s.deleteMediaAsset);

  const [expanded, setExpanded] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [previewItem, setPreviewItem] = useState<{ kind: "source"; source: SourceDto } | { kind: "image"; asset: MediaAssetDto } | null>(null);
  const [previewDetail, setPreviewDetail] = useState<SourceDetailDto | null>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [libraryQuery, setLibraryQuery] = useState("");
  const [libraryResults, setLibraryResults] = useState<SourceSearchItemDto[]>([]);
  const [librarySearching, setLibrarySearching] = useState(false);
  const [libraryError, setLibraryError] = useState<string | null>(null);
  const [linkingSourceId, setLinkingSourceId] = useState<string | null>(null);
  const [retryingSourceId, setRetryingSourceId] = useState<string | null>(null);

  const activeDraft = useMemo(() => drafts.find((d) => d.id === activeDraftId), [drafts, activeDraftId]);
  const mediaAssets = activeDraft?.mediaAssets || [];

  useEffect(() => {
    if (activeDraftId) {
      loadSources(activeDraftId);
    }
  }, [activeDraftId, loadSources]);

  const hasFetchingSources = sources.some(s => s.title === "Fetching...");

  useEffect(() => {
    if (!hasFetchingSources || !activeDraftId) return;

    const interval = setInterval(() => {
      loadSources(activeDraftId);
    }, 2000);

    return () => clearInterval(interval);
  }, [hasFetchingSources, activeDraftId, loadSources]);

  useEffect(() => {
    if (!previewItem || previewItem.kind !== "source" || !activeDraftId) {
      setPreviewDetail(null);
      return;
    }

    setLoadingDetail(true);
    fetchSourceDetail(activeDraftId, previewItem.source.id)
      .then(setPreviewDetail)
      .catch((err) => console.error("Failed to load source details: ", err))
      .finally(() => setLoadingDetail(false));
  }, [previewItem, activeDraftId]);

  useEffect(() => {
    if (!previewItem || previewItem.kind !== "source") {
      return;
    }

    const status = previewDetail?.processingStatus || previewItem.source.processingStatus;
    if (status !== "Pending" && status !== "Processing") {
      return;
    }

    let cancelled = false;
    const syncStatus = async () => {
      try {
        const result = await fetchSourceStatus(previewItem.source.id);
        if (!result) {
          return;
        }
        if (cancelled) {
          return;
        }

        setPreviewDetail((current) => {
          if (!current) {
            return current;
          }

          return {
            ...current,
            summary: result.summary,
            processingStatus: result.processingStatus,
            youtubeVideoId: result.youtubeVideoId,
          };
        });
      } catch (err) {
        console.error("Failed to load source status:", err);
      }
    };

    void syncStatus();
    const interval = setInterval(syncStatus, 2000);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, [previewItem, previewDetail]);

  const handleRetryTranscription = async (source: SourceDto) => {
    if (!activeDraftId) {
      return;
    }

    setRetryingSourceId(source.id);
    setLibraryError(null);
    try {
      const status = await retrySourceTranscription(source.id);
      setPreviewDetail((current) => {
        if (!current || current.id !== source.id) {
          return current;
        }

        return {
          ...current,
          processingStatus: status.processingStatus,
          summary: status.summary,
          youtubeVideoId: status.youtubeVideoId,
        };
      });
      await loadSources(activeDraftId);
    } catch (err) {
      setLibraryError(err instanceof Error ? err.message : "Failed to retry transcription.");
    } finally {
      setRetryingSourceId(null);
    }
  };

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file || !activeDraftId) return;

    setUploading(true);
    try {
      const res = await uploadFileSource(activeDraftId, file);
      const currentDoc = useEditorStore.getState().doc;
      const separator = currentDoc ? "\n\n" : "";
      const newDoc = `${currentDoc}${separator}${res.markdownLink}`;
      useEditorStore.getState().setDoc(newDoc);
    } catch (err) {
      console.error("Failed to upload file source: ", err);
    } finally {
      setUploading(false);
    }
  };

  const handleDeleteSource = async (source: SourceDto) => {
    const currentDoc = useEditorStore.getState().doc;
    let nextDoc = currentDoc;

    if (source.kind === "Url") {
      const urlEscaped = source.reference.replace(/[-/\\^$*+?.()|[\]{}]/g, "\\$&");
      const markdownLinkRegex = new RegExp(`\\[[^\\]]*\\]\\(${urlEscaped}\\)`, "g");
      const nakedUrlRegex = new RegExp(urlEscaped, "g");
      nextDoc = nextDoc.replace(markdownLinkRegex, "").replace(nakedUrlRegex, "");
    } else if (source.kind === "YouTube") {
      const urlEscaped = source.reference.replace(/[-/\\^$*+?.()|[\]{}]/g, "\\$&");
      const markdownLinkRegex = new RegExp(`\\[[^\\]]*\\]\\(${urlEscaped}\\)`, "g");
      const youtubeEmbedRegex = new RegExp(`!\\[[^\\]]*\\]\\(${urlEscaped}\\)`, "g");
      const nakedUrlRegex = new RegExp(urlEscaped, "g");
      nextDoc = nextDoc
        .replace(youtubeEmbedRegex, "")
        .replace(markdownLinkRegex, "")
        .replace(nakedUrlRegex, "");
    } else if (source.kind === "File") {
      const fileRefRegex = new RegExp(`\\[[^\\]]*\\]\\(file://${source.id}\\)`, "g");
      nextDoc = nextDoc.replace(fileRefRegex, "");
    }

    useEditorStore.getState().setDoc(nextDoc.trim());

    if (activeDraftId) {
      try {
        await deleteSource(activeDraftId, source.id);
        loadSources(activeDraftId);
      } catch (err) {
        console.error("Failed to delete source:", err);
      }
    }
  };

  const handleInsertSource = (source: SourceDto) => {
    let markdown = "";
    if (source.kind === "Url") {
      const displayTitle = source.title && source.title !== "Fetching..." ? source.title : "Link";
      markdown = `[${displayTitle}](${source.reference})`;
    } else if (source.kind === "YouTube") {
      const displayTitle = source.title && source.title !== "Fetching..." ? source.title : "Video";
      markdown = `![${displayTitle}](${source.reference})`;
    } else if (source.kind === "File") {
      markdown = `[${source.title || "File"}](file://${source.id})`;
    }
    if (markdown) {
      window.dispatchEvent(new CustomEvent("editor-insert", { detail: markdown }));
    }
  };

  const handleInsertMedia = (asset: MediaAssetDto) => {
    const tag = `![${asset.fileName}](media://${asset.id})`;
    window.dispatchEvent(new CustomEvent("editor-insert", { detail: tag }));
  };

  const handleDeleteMedia = async (id: string) => {
    await deleteMediaAsset(id);
    const currentDoc = useEditorStore.getState().doc;
    const refRegex = new RegExp(`!\\[[^\\]]*\\]\\(media://${id}\\)`, "g");
    const nextDoc = currentDoc.replace(refRegex, "").trim();
    useEditorStore.getState().setDoc(nextDoc);
  };

  const handleSearchLibrary = async () => {
    const query = libraryQuery.trim();
    if (!query) {
      setLibraryResults([]);
      setLibraryError(null);
      return;
    }

    setLibrarySearching(true);
    setLibraryError(null);
    try {
      const result = await searchSources(query);
      setLibraryResults(result.items);
    } catch (err) {
      setLibraryError(err instanceof Error ? err.message : "Failed to search sources.");
    } finally {
      setLibrarySearching(false);
    }
  };

  const handleLinkSource = async (sourceId: string) => {
    if (!activeDraftId) {
      return;
    }

    setLinkingSourceId(sourceId);
    try {
      await linkSourceToDraft(activeDraftId, sourceId);
      await loadSources(activeDraftId);
    } catch (err) {
      setLibraryError(err instanceof Error ? err.message : "Failed to link source.");
    } finally {
      setLinkingSourceId(null);
    }
  };

  return {
    activeDraft,
    activeDraftId,
    sources,
    mediaAssets,
    expanded,
    uploading,
    previewItem,
    previewDetail,
    loadingDetail,
    libraryQuery,
    libraryResults,
    librarySearching,
    libraryError,
    linkingSourceId,
    retryingSourceId,
    setExpanded,
    setPreviewItem,
    setLibraryQuery,
    handleRetryTranscription,
    handleFileChange,
    handleDeleteSource,
    handleInsertSource,
    handleInsertMedia,
    handleDeleteMedia,
    handleSearchLibrary,
    handleLinkSource,
  };
}
