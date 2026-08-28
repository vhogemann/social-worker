import { useEffect, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { useDraftStore } from "../../store/draftStore";
import { useEditorStore } from "../../store/editorStore";

export function useComposerDraftSync() {
  const { workflowId, draftId, variantId } = useParams<{
    workflowId?: string;
    draftId?: string;
    variantId?: string;
  }>();
  const location = useLocation();
  const navigate = useNavigate();
  const [isReady, setIsReady] = useState(false);

  const loadDrafts = useDraftStore((s) => s.loadDrafts);
  const applyExternal = useEditorStore((s) => s.applyExternal);

  const activeTargetId = variantId ?? draftId;

  // 1. Initial load of drafts on mount
  useEffect(() => {
    let cancelled = false;
    void loadDrafts().then(() => {
      if (cancelled) return;

      const state = useDraftStore.getState();
      const initialDraftId = activeTargetId ?? (state.drafts.length > 0 ? state.drafts[0].id : null);

      if (initialDraftId) {
        // Search among top-level drafts or nested variants
        let foundDraft = state.drafts.find((d) => d.id === initialDraftId);
        if (!foundDraft) {
          for (const d of state.drafts) {
            const v = d.variants?.find((varDraft) => varDraft.id === initialDraftId);
            if (v) {
              foundDraft = v;
              break;
            }
          }
        }

        if (foundDraft) {
          useDraftStore.setState({ activeDraftId: initialDraftId });
          applyExternal(foundDraft.content ?? "");
        }
      }

      setIsReady(true);
    });

    return () => {
      cancelled = true;
    };
  }, [loadDrafts, applyExternal, activeTargetId]);

  // 2. Handle "/workflows/:workflowId/draft" and legacy "/composer" creation redirects
  useEffect(() => {
    if (!isReady) return;

    if (workflowId && !draftId && location.pathname.endsWith("/draft")) {
      const state = useDraftStore.getState();
      void state
        .createDraft(undefined, undefined, undefined, undefined, workflowId)
        .then((newDraft) => {
          navigate(`/workflows/${workflowId}/draft/${newDraft.id}`, { replace: true });
        });
      return;
    }

    if (location.pathname === "/composer") {
      const state = useDraftStore.getState();
      let targetId = state.activeDraftId;
      if (!targetId && state.drafts.length > 0) {
        targetId = state.drafts[0].id;
      }
      if (!targetId) {
        void state.createDraft().then((draft) => {
          navigate(`/draft/${draft.id}`, { replace: true });
        });
      } else {
        navigate(`/draft/${targetId}`, { replace: true });
      }
    }
  }, [isReady, location.pathname, workflowId, draftId, navigate]);

  // 3. Synchronize draft active state when route activeTargetId changes
  useEffect(() => {
    if (!isReady || !activeTargetId) return;

    const state = useDraftStore.getState();
    let draft = state.drafts.find((d) => d.id === activeTargetId);
    if (!draft) {
      for (const d of state.drafts) {
        const v = d.variants?.find((varDraft) => varDraft.id === activeTargetId);
        if (v) {
          draft = v;
          break;
        }
      }
    }

    if (draft) {
      useDraftStore.setState({ activeDraftId: activeTargetId });
      applyExternal(draft.content ?? "");
    } else {
      void useDraftStore
        .getState()
        .switchDraft(activeTargetId)
        .then((loaded) => {
          applyExternal(loaded.content ?? "");
        })
        .catch(() => {
          navigate("/drafts", { replace: true });
        });
    }
  }, [activeTargetId, isReady, applyExternal, navigate]);

  return {
    isReady,
    workflowId,
    canonicalDraftId: draftId,
    variantId,
    activeDraftId: activeTargetId,
  };
}
