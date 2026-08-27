import { useEffect, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { useDraftStore } from "../../store/draftStore";
import { useEditorStore } from "../../store/editorStore";

export function useComposerDraftSync() {
  const { workflowId, draftId } = useParams<{ workflowId?: string; draftId?: string }>();
  const location = useLocation();
  const navigate = useNavigate();
  const [isReady, setIsReady] = useState(false);

  const loadDrafts = useDraftStore((s) => s.loadDrafts);
  const applyExternal = useEditorStore((s) => s.applyExternal);

  // 1. Initial load of drafts on mount
  useEffect(() => {
    let cancelled = false;
    void loadDrafts().then(() => {
      if (cancelled) return;

      const state = useDraftStore.getState();
      const initialDraftId = draftId ?? (state.drafts.length > 0 ? state.drafts[0].id : null);

      if (initialDraftId) {
        const draft = state.drafts.find((d) => d.id === initialDraftId);
        if (draft) {
          useDraftStore.setState({ activeDraftId: initialDraftId });
          applyExternal(draft.content ?? "");
        }
      }

      setIsReady(true);
    });

    return () => {
      cancelled = true;
    };
  }, [loadDrafts, applyExternal]);

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

  // 3. Synchronize draft active state when route draftId changes
  useEffect(() => {
    if (!isReady || !draftId) return;

    const state = useDraftStore.getState();
    const draft = state.drafts.find((d) => d.id === draftId);
    if (draft) {
      useDraftStore.setState({ activeDraftId: draftId });
      applyExternal(draft.content ?? "");
    } else {
      void useDraftStore
        .getState()
        .switchDraft(draftId)
        .then((loaded) => {
          applyExternal(loaded.content ?? "");
        })
        .catch(() => {
          navigate("/drafts", { replace: true });
        });
    }
  }, [draftId, isReady, applyExternal, navigate]);

  return { isReady };
}
