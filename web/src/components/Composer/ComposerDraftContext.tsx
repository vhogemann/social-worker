import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
} from "react";
import { useNavigate, useParams } from "react-router-dom";
import {
  fetchDraft,
  patchDraft,
  patchPlatformThread,
  publishPlatformThread,
  fetchSources,
  type DraftDto,
  type SourceDto,
  type PublishResult,
} from "../../api/drafts";
import { useDraftStore } from "../../store/draftStore";

export interface ChatActivityCard {
  id: string;
  sourceKey?: string;
  title: string;
  message: string;
  kind: "error" | "info";
  createdAt: string;
}

export interface ComposerDraftContextValue {
  activeDraft: DraftDto | null;
  canonicalDraft: DraftDto | null;
  workflowId?: string;
  activeDraftId: string;
  canonicalDraftId: string;
  doc: string;
  version: number;
  panelMode: "edit" | "preview";
  sources: SourceDto[];
  activityCards: ChatActivityCard[];
  isLoading: boolean;
  setDoc: (doc: string) => void;
  applyExternal: (doc: string) => void;
  setPanelMode: (mode: "edit" | "preview") => void;
  loadSources: () => Promise<void>;
  addActivityCard: (card: Omit<ChatActivityCard, "id" | "createdAt">) => void;
  clearActivityCards: () => void;
  saveContent: (content?: string) => Promise<void>;
  updateStage: (stage: string) => Promise<void>;
  publish: () => Promise<PublishResult>;
  refetchDraft: () => Promise<DraftDto | null>;
}

const ComposerDraftContext = createContext<ComposerDraftContextValue | null>(null);

export interface ComposerDraftProviderProps {
  children: React.ReactNode;
}

export const ComposerDraftProvider: React.FC<ComposerDraftProviderProps> = ({ children }) => {
  const { workflowId, draftId, variantId } = useParams<{
    workflowId?: string;
    draftId?: string;
    variantId?: string;
  }>();

  const navigate = useNavigate();
  const activeDraftId = variantId ?? draftId ?? "";
  const canonicalDraftId = draftId ?? activeDraftId;

  const [activeDraft, setActiveDraft] = useState<DraftDto | null>(null);
  const [canonicalDraft, setCanonicalDraft] = useState<DraftDto | null>(null);
  const [doc, setDocState] = useState<string>("");
  const [version, setVersion] = useState<number>(0);
  const [panelMode, setPanelMode] = useState<"edit" | "preview">("edit");
  const [sources, setSources] = useState<SourceDto[]>([]);
  const [activityCards, setActivityCards] = useState<ChatActivityCard[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  const docRef = useRef(doc);
  docRef.current = doc;

  const isDirtyRef = useRef(false);
  const activeDraftIdRef = useRef(activeDraftId);
  activeDraftIdRef.current = activeDraftId;

  // Load draft data whenever activeDraftId changes
  const loadDraftData = useCallback(async () => {
    if (!activeDraftId) return null;
    try {
      setIsLoading(true);
      const fetchedActive = await fetchDraft(activeDraftId);
      setActiveDraft(fetchedActive);
      setDocState(fetchedActive.content ?? "");
      setVersion((v) => v + 1);
      isDirtyRef.current = false;

      // If active draft is a variant, also fetch or set the canonical draft
      if (fetchedActive.canonicalDraftId) {
        if (fetchedActive.canonicalDraftId === canonicalDraftId && canonicalDraft?.id === canonicalDraftId) {
          // Canonical already present
        } else {
          const fetchedCanonical = await fetchDraft(fetchedActive.canonicalDraftId);
          setCanonicalDraft(fetchedCanonical);
        }
      } else {
        setCanonicalDraft(fetchedActive);
      }

      // Load sources for this draft
      const fetchedSources = await fetchSources(activeDraftId);
      setSources(fetchedSources);

      setIsLoading(false);
      return fetchedActive;
    } catch (err) {
      console.error("Failed to load draft in Composer:", err);
      setIsLoading(false);
      navigate("/drafts", { replace: true });
      return null;
    }
  }, [activeDraftId, canonicalDraftId, canonicalDraft?.id, navigate]);

  useEffect(() => {
    void loadDraftData();
  }, [loadDraftData]);

  // Periodic source polling if any source is processing
  useEffect(() => {
    const hasPending = sources.some((s) => s.title === "Fetching..." || s.processingStatus === "Processing");
    if (!hasPending || !activeDraftId) return;

    const interval = setInterval(() => {
      void fetchSources(activeDraftId).then(setSources).catch(() => {});
    }, 2000);

    return () => clearInterval(interval);
  }, [sources, activeDraftId]);

  // Debounced autosave
  const saveContent = useCallback(
    async (contentToSave?: string) => {
      const content = contentToSave ?? docRef.current;
      const targetId = activeDraftIdRef.current;
      if (!targetId) return;

      try {
        const updated = await patchDraft(targetId, { content });
        setActiveDraft(updated);
        isDirtyRef.current = false;
        useDraftStore.getState().loadDrafts().catch(() => {});
      } catch (err) {
        console.error("Failed to save draft content:", err);
      }
    },
    [],
  );

  const setDoc = useCallback((newDoc: string) => {
    setDocState(newDoc);
    isDirtyRef.current = true;
  }, []);

  const applyExternal = useCallback((newDoc: string) => {
    setDocState(newDoc);
    setVersion((v) => v + 1);
    isDirtyRef.current = true;
  }, []);

  // Autosave when dirty after 1.5s
  useEffect(() => {
    if (!isDirtyRef.current) return;
    const timer = setTimeout(() => {
      if (isDirtyRef.current) {
        void saveContent(doc);
      }
    }, 1500);
    return () => clearTimeout(timer);
  }, [doc, saveContent]);

  // Save on unmount
  useEffect(() => {
    return () => {
      if (isDirtyRef.current) {
        void saveContent(docRef.current);
      }
    };
  }, [saveContent]);

  const loadSources = useCallback(async () => {
    if (!activeDraftId) return;
    try {
      const fetched = await fetchSources(activeDraftId);
      setSources(fetched);
    } catch (err) {
      console.error("Failed to fetch sources:", err);
    }
  }, [activeDraftId]);

  const addActivityCard = useCallback((card: Omit<ChatActivityCard, "id" | "createdAt">) => {
    setActivityCards((prev) => {
      if (card.sourceKey && prev.some((item) => item.sourceKey === card.sourceKey)) {
        return prev;
      }
      const id = `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
      return [...prev, { ...card, id, createdAt: new Date().toISOString() }];
    });
  }, []);

  const clearActivityCards = useCallback(() => {
    setActivityCards([]);
  }, []);

  const updateStage = useCallback(
    async (stage: string) => {
      if (!activeDraftId || !activeDraft) return;
      const primaryThread = activeDraft.threads?.[0];
      if (!primaryThread) return;

      const updated = await patchPlatformThread(activeDraftId, primaryThread.id, { stage });
      setActiveDraft((prev) =>
        prev
          ? {
              ...prev,
              threads: prev.threads.map((t) => (t.id === updated.id ? updated : t)),
            }
          : prev
      );
      useDraftStore.getState().loadDrafts().catch(() => {});
    },
    [activeDraftId, activeDraft]
  );

  const publish = useCallback(async (): Promise<PublishResult> => {
    if (!activeDraftId || !activeDraft) {
      throw new Error("No active draft to publish.");
    }
    await saveContent(docRef.current);

    let threadId = activeDraft.threads?.[0]?.id;
    if (!threadId) {
      const fresh = await fetchDraft(activeDraftId);
      threadId = fresh.threads?.[0]?.id;
    }

    if (!threadId) {
      throw new Error("No platform thread available to publish.");
    }

    const result = await publishPlatformThread(activeDraftId, threadId);
    await loadDraftData();
    useDraftStore.getState().loadDrafts().catch(() => {});
    return result;
  }, [activeDraftId, activeDraft, saveContent, loadDraftData]);

  const value: ComposerDraftContextValue = {
    activeDraft,
    canonicalDraft,
    workflowId,
    activeDraftId,
    canonicalDraftId,
    doc,
    version,
    panelMode,
    sources,
    activityCards,
    isLoading,
    setDoc,
    applyExternal,
    setPanelMode,
    loadSources,
    addActivityCard,
    clearActivityCards,
    saveContent,
    updateStage,
    publish,
    refetchDraft: loadDraftData,
  };

  return (
    <ComposerDraftContext.Provider value={value}>
      {children}
    </ComposerDraftContext.Provider>
  );
};

export function useComposerDraft(): ComposerDraftContextValue {
  const context = useContext(ComposerDraftContext);
  if (!context) {
    throw new Error("useComposerDraft must be used within a ComposerDraftProvider");
  }
  return context;
}
