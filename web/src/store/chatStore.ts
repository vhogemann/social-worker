import { create } from "zustand";
import type { ExportedMessageRepository } from "@assistant-ui/react";

interface ChatStore {
  messagesByDraft: Record<string, ExportedMessageRepository>;
  saveMessages: (draftId: string, repo: ExportedMessageRepository) => void;
  loadMessages: (draftId: string) => ExportedMessageRepository | null;
  clearMessages: (draftId: string) => void;
}

export const useChatStore = create<ChatStore>((set, get) => ({
  messagesByDraft: {},

  saveMessages: (draftId, repo) =>
    set((s) => ({
      messagesByDraft: { ...s.messagesByDraft, [draftId]: repo },
    })),

  loadMessages: (draftId) => get().messagesByDraft[draftId] ?? null,

  clearMessages: (draftId) =>
    set((s) => {
      const next = { ...s.messagesByDraft };
      delete next[draftId];
      return { messagesByDraft: next };
    }),
}));