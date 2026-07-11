import { create } from "zustand";
import { fetchDrafts, createDraft, fetchDraft, patchDraft, createPlatformThread, patchPlatformThread, publishPlatformThread, fetchSources, uploadFile, uploadMedia, patchMediaAsset, deleteMediaAsset, type DraftDto, type PlatformThreadDto, type SourceDto, type MediaAssetDto } from "../api/drafts";
import { useChatStore } from "./chatStore";

interface DraftStore {
  drafts: DraftDto[];
  activeDraftId: string | null;
  activePlatform: string;
  sources: SourceDto[];
  loading: boolean;
  loadDrafts: () => Promise<void>;
  createDraft: (title?: string, content?: string, targetPlatform?: string) => Promise<DraftDto>;
  switchDraft: (id: string) => Promise<DraftDto>;
  updateDraftTitle: (id: string, title: string) => Promise<void>;
  saveDraftContent: (id: string, content: string) => Promise<void>;
  archiveDraft: (id: string) => Promise<void>;
  unarchiveDraft: (id: string) => Promise<void>;
  deleteDraft: (id: string) => Promise<void>;
  setActivePlatform: (platform: string) => void;
  createThreadVariant: (draftId: string, platform: string) => Promise<void>;
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
  sources: [],
  loading: false,

  loadDrafts: async () => {
    const drafts = await fetchDrafts();
    set({ drafts });
    for (const d of drafts) {
      if (d.chatHistory) {
        try {
          const parsed = JSON.parse(d.chatHistory);
          useChatStore.getState().saveMessages(d.id, parsed);
        } catch (e) {
          console.error("Failed to parse chat history for draft", d.id, e);
        }
      }
    }
  },

  createDraft: async (title, content, targetPlatform) => {
    const draft = await createDraft(title, content, targetPlatform);
    set((s) => ({ drafts: [draft, ...s.drafts], activeDraftId: draft.id }));
    return draft;
  },

  switchDraft: async (id) => {
    const draft = await fetchDraft(id);
    set((s) => ({
      activeDraftId: id,
      drafts: s.drafts.map((d) => (d.id === id ? draft : d)),
    }));
    if (draft.chatHistory) {
      try {
        const parsed = JSON.parse(draft.chatHistory);
        useChatStore.getState().saveMessages(id, parsed);
      } catch (e) {
        console.error("Failed to parse chat history for draft", id, e);
      }
    }
    await get().loadSources(id);
    return draft;
  },

  updateDraftTitle: async (id, title) => {
    const updated = await patchDraft(id, { title });
    set((s) => ({
      drafts: s.drafts.map((d) => (d.id === id ? updated : d)),
    }));
  },

  saveDraftContent: async (id, content) => {
    const updated = await patchDraft(id, { content });
    set((s) => ({
      drafts: s.drafts.map((d) => (d.id === id ? updated : d)),
    }));
  },

  archiveDraft: async (id) => {
    const updated = await patchDraft(id, { status: "Archived" });
    set((s) => ({
      drafts: s.drafts.map((d) => (d.id === id ? updated : d)),
    }));
  },

  unarchiveDraft: async (id) => {
    const updated = await patchDraft(id, { status: "Editing" });
    set((s) => ({
      drafts: s.drafts.map((d) => (d.id === id ? updated : d)),
    }));
  },

  deleteDraft: async (id) => {
    await patchDraft(id, { status: "Deleted" });
    set((s) => {
      const nextActiveId = s.activeDraftId === id
        ? s.drafts.find((d) => d.id !== id && d.status !== "Deleted")?.id ?? null
        : s.activeDraftId;
      return {
        drafts: s.drafts.filter((d) => d.id !== id),
        activeDraftId: nextActiveId,
      };
    });
  },

  setActivePlatform: (platform) => set({ activePlatform: platform }),

  createThreadVariant: async (draftId, platform) => {
    const thread = await createPlatformThread(draftId, platform);
    set((s) => ({
      drafts: s.drafts.map((d) =>
        d.id === draftId ? { ...d, threads: [...d.threads, thread] } : d
      ),
    }));
  },

  updateThreadStage: async (draftId, threadId, stage) => {
    const updated = await patchPlatformThread(draftId, threadId, { stage });
    set((s) => ({
      drafts: s.drafts.map((d) =>
        d.id === draftId
          ? {
              ...d,
              threads: d.threads.map((t) => (t.id === threadId ? updated : t)),
            }
          : d
      ),
    }));
  },

  updateThreadContent: async (draftId, threadId, content) => {
    const updated = await patchPlatformThread(draftId, threadId, { content });
    set((s) => ({
      drafts: s.drafts.map((d) =>
        d.id === draftId
          ? {
              ...d,
              threads: d.threads.map((t) => (t.id === threadId ? updated : t)),
            }
          : d
      ),
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
    const drafts = await fetchDrafts();
    set({ drafts });
    return res;
  },

  updateMediaAltText: async (mediaId, altText) => {
    const updated = await patchMediaAsset(mediaId, { altText });
    set((s) => ({
      drafts: s.drafts.map((d) => {
        if (d.mediaAssets) {
          return {
            ...d,
            mediaAssets: d.mediaAssets.map((m) => (m.id === mediaId ? updated : m)),
          };
        }
        return d;
      }),
    }));
  },

  deleteMediaAsset: async (mediaId) => {
    await deleteMediaAsset(mediaId);
    const drafts = await fetchDrafts();
    set({ drafts });
  },

  saveDraftChat: async (id, chatHistory) => {
    const updated = await patchDraft(id, { chatHistory });
    set((s) => ({
      drafts: s.drafts.map((d) => (d.id === id ? updated : d)),
    }));
  },
}));