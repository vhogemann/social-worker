import { create } from "zustand";
import {
  fetchDrafts,
  createDraft,
  createReplyDraftFromBlueskyPostUrl,
  fetchDraft,
  patchDraft,
  createPlatformThread,
  patchPlatformThread,
  publishPlatformThread,
  fetchSources,
  uploadFile,
  uploadMedia,
  patchMediaAsset,
  deleteMediaAsset,
  fetchPlatformCapabilities,
  type DraftDto,
  type PlatformThreadDto,
  type SourceDto,
  type MediaAssetDto,
  type PlatformCapabilityDto,
} from "../api/drafts";

export function findDraft(drafts: DraftDto[], id: string | null | undefined): DraftDto | undefined {
  if (!id) return undefined;
  for (const d of drafts) {
    if (d.id === id) return d;
    if (d.variants) {
      const v = d.variants.find((variant) => variant.id === id);
      if (v) return v;
    }
  }
  return undefined;
}

function updateDraftInTree(drafts: DraftDto[], updated: DraftDto): DraftDto[] {
  const isTopLevel = drafts.some((d) => d.id === updated.id);
  if (isTopLevel) {
    return drafts.map((d) => (d.id === updated.id ? { ...d, ...updated } : d));
  }
  return drafts.map((d) => {
    if (d.id === updated.canonicalDraftId || d.variants?.some((v) => v.id === updated.id)) {
      const updatedVariants = (d.variants ?? []).map((v) =>
        v.id === updated.id ? { ...v, ...updated } : v
      );
      return { ...d, variants: updatedVariants };
    }
    return d;
  });
}

interface DraftStore {
  drafts: DraftDto[];
  activeDraftId: string | null;
  activePlatform: string;
  platformCapabilities: PlatformCapabilityDto[];
  sources: SourceDto[];
  loading: boolean;
  loadDrafts: () => Promise<void>;
  loadPlatformCapabilities: () => Promise<void>;
  supportsReplyTargetForPlatform: (platform: string | null | undefined) => boolean;
  createDraft: (
    title?: string,
    content?: string,
    targetPlatform?: string,
    targetAccountId?: string,
    automationWorkflowId?: string
  ) => Promise<DraftDto>;
  createReplyDraftFromBlueskyPostUrl: (
    url: string,
    title?: string,
    content?: string
  ) => Promise<DraftDto>;
  switchDraft: (id: string) => Promise<DraftDto>;
  updateDraftTitle: (id: string, title: string) => Promise<void>;
  saveDraftContent: (id: string, content: string) => Promise<void>;
  archiveDraft: (id: string) => Promise<void>;
  unarchiveDraft: (id: string) => Promise<void>;
  deleteDraft: (id: string) => Promise<void>;
  setActivePlatform: (platform: string) => void;
  createThreadVariant: (draftId: string, platform: string, accountId?: string) => Promise<void>;
  updateThreadStage: (draftId: string, threadId: string, stage: string) => Promise<void>;
  updateThreadContent: (draftId: string, threadId: string, content: string) => Promise<void>;
  publishThread: (draftId: string, threadId: string) => Promise<any>;
  loadSources: (draftId: string) => Promise<void>;
  uploadFileSource: (draftId: string, file: File) => Promise<{ sourceId: string; markdownLink: string }>;
  uploadMediaAsset: (draftId: string, file: File) => Promise<{ id: string; markdownTag: string }>;
  updateMediaAltText: (mediaId: string, altText: string) => Promise<void>;
  deleteMediaAsset: (mediaId: string) => Promise<void>;
  saveDraftChat: (id: string, chatHistory: string) => Promise<void>;
}

export const useDraftStore = create<DraftStore>((set, get) => ({
  drafts: [],
  activeDraftId: null,
  activePlatform: "Bluesky",
  platformCapabilities: [],
  sources: [],
  loading: false,

  loadDrafts: async () => {
    const drafts = await fetchDrafts();
    set({ drafts });
  },

  loadPlatformCapabilities: async () => {
    const platformCapabilities = await fetchPlatformCapabilities();
    set({ platformCapabilities });
  },

  supportsReplyTargetForPlatform: (platform) => {
    if (!platform) return false;
    const capabilities = get().platformCapabilities;
    return capabilities.some(
      (c) => c.platform.toLowerCase() === platform.toLowerCase() && c.supportsReplyTarget
    );
  },

  createDraft: async (title, content, targetPlatform, targetAccountId, automationWorkflowId) => {
    const draft = await createDraft(
      title,
      content,
      targetPlatform,
      targetAccountId,
      automationWorkflowId
    );
    set((s) => ({ drafts: [draft, ...s.drafts], activeDraftId: draft.id }));
    return draft;
  },

  createReplyDraftFromBlueskyPostUrl: async (url, title, content) => {
    const updatedDraft = await createReplyDraftFromBlueskyPostUrl(url, title, content);
    set((s) => ({
      drafts: [updatedDraft, ...s.drafts.filter((d) => d.id !== updatedDraft.id)],
      activeDraftId: updatedDraft.id,
    }));
    return updatedDraft;
  },

  switchDraft: async (id) => {
    const draft = await fetchDraft(id);
    set((s) => ({
      activeDraftId: id,
      drafts: updateDraftInTree(s.drafts, draft),
    }));
    await get().loadSources(id);
    return draft;
  },

  updateDraftTitle: async (id, title) => {
    const updated = await patchDraft(id, { title });
    set((s) => ({
      drafts: updateDraftInTree(s.drafts, updated),
    }));
  },

  saveDraftContent: async (id, content) => {
    const updated = await patchDraft(id, { content });
    set((s) => ({
      drafts: updateDraftInTree(s.drafts, updated),
    }));
  },

  archiveDraft: async (id) => {
    const updated = await patchDraft(id, { status: "Archived" });
    set((s) => ({
      drafts: updateDraftInTree(s.drafts, updated),
    }));
  },

  unarchiveDraft: async (id) => {
    const updated = await patchDraft(id, { status: "Editing" });
    set((s) => ({
      drafts: updateDraftInTree(s.drafts, updated),
    }));
  },

  deleteDraft: async (id) => {
    await patchDraft(id, { status: "Deleted" });
    set((s) => {
      const nextActiveId =
        s.activeDraftId === id
          ? s.drafts.find((d) => d.id !== id && d.status !== "Deleted")?.id ?? null
          : s.activeDraftId;
      return {
        drafts: s.drafts
          .filter((d) => d.id !== id)
          .map((d) => ({
            ...d,
            variants: d.variants?.filter((v) => v.id !== id),
          })),
        activeDraftId: nextActiveId,
      };
    });
  },

  setActivePlatform: (platform) => set({ activePlatform: platform }),

  createThreadVariant: async (draftId, platform, accountId) => {
    const thread = await createPlatformThread(draftId, platform, accountId);
    set((s) => ({
      drafts: s.drafts.map((d) =>
        d.id === draftId ? { ...d, threads: [...d.threads, thread] } : d
      ),
    }));
  },

  updateThreadStage: async (draftId, threadId, stage) => {
    const updated = await patchPlatformThread(draftId, threadId, { stage });
    set((s) => ({
      drafts: s.drafts.map((d) => {
        if (d.id === draftId) {
          return {
            ...d,
            threads: d.threads.map((t) => (t.id === threadId ? updated : t)),
          };
        }
        if (d.variants) {
          return {
            ...d,
            variants: d.variants.map((v) =>
              v.id === draftId
                ? {
                    ...v,
                    threads: v.threads.map((t) => (t.id === threadId ? updated : t)),
                  }
                : v
            ),
          };
        }
        return d;
      }),
    }));
  },

  updateThreadContent: async (draftId, threadId, content) => {
    const updated = await patchPlatformThread(draftId, threadId, { content });
    set((s) => ({
      drafts: s.drafts.map((d) => {
        if (d.id === draftId) {
          return {
            ...d,
            threads: d.threads.map((t) => (t.id === threadId ? updated : t)),
          };
        }
        if (d.variants) {
          return {
            ...d,
            variants: d.variants.map((v) =>
              v.id === draftId
                ? {
                    ...v,
                    threads: v.threads.map((t) => (t.id === threadId ? updated : t)),
                  }
                : v
            ),
          };
        }
        return d;
      }),
    }));
  },

  publishThread: async (draftId, threadId) => {
    const result = await publishPlatformThread(draftId, threadId);
    await get().switchDraft(draftId);
    return result;
  },

  loadSources: async (draftId) => {
    const sources = await fetchSources(draftId);
    set({ sources });
  },

  uploadFileSource: async (draftId, file) => {
    const res = await uploadFile(draftId, file);
    await get().loadSources(draftId);
    return res;
  },

  uploadMediaAsset: async (draftId, file) => {
    const res = await uploadMedia(draftId, file);
    await get().switchDraft(draftId);
    return res;
  },

  updateMediaAltText: async (mediaId, altText) => {
    await patchMediaAsset(mediaId, { altText });
    set((s) => ({
      drafts: s.drafts.map((d) => ({
        ...d,
        mediaAssets: d.mediaAssets?.map((ma) => (ma.id === mediaId ? { ...ma, altText } : ma)),
        variants: d.variants?.map((v) => ({
          ...v,
          mediaAssets: v.mediaAssets?.map((ma) => (ma.id === mediaId ? { ...ma, altText } : ma)),
        })),
      })),
    }));
  },

  deleteMediaAsset: async (mediaId) => {
    await deleteMediaAsset(mediaId);
    set((s) => ({
      drafts: s.drafts.map((d) => ({
        ...d,
        mediaAssets: d.mediaAssets?.filter((ma) => ma.id !== mediaId),
        variants: d.variants?.map((v) => ({
          ...v,
          mediaAssets: v.mediaAssets?.filter((ma) => ma.id !== mediaId),
        })),
      })),
    }));
  },

  saveDraftChat: async (id, chatHistory) => {
    const updated = await patchDraft(id, { chatHistory });
    set((s) => ({
      drafts: updateDraftInTree(s.drafts, updated),
    }));
  },
}));