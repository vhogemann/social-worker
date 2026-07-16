import { create } from "zustand";
import type { ExportedMessageRepository } from "@assistant-ui/react";

export interface ChatActivityCard {
  id: string;
  sourceKey?: string;
  title: string;
  message: string;
  kind: "error" | "info";
  createdAt: string;
}

interface ChatStore {
  messagesByDraft: Record<string, ExportedMessageRepository>;
  activityCardsByDraft: Record<string, ChatActivityCard[]>;
  saveMessages: (draftId: string, repo: ExportedMessageRepository) => void;
  loadMessages: (draftId: string) => ExportedMessageRepository | null;
  clearMessages: (draftId: string) => void;
  addActivityCard: (draftId: string, card: Omit<ChatActivityCard, "id" | "createdAt">) => void;
  clearActivityCards: (draftId: string) => void;
}

export const useChatStore = create<ChatStore>((set, get) => ({
  messagesByDraft: {},
  activityCardsByDraft: {},

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

  addActivityCard: (draftId, card) =>
    set((s) => {
      const existing = s.activityCardsByDraft[draftId] ?? [];
      if (card.sourceKey && existing.some((item) => item.sourceKey === card.sourceKey)) {
        return s;
      }

      const id = `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
      return {
        activityCardsByDraft: {
          ...s.activityCardsByDraft,
          [draftId]: [...existing, { ...card, id, createdAt: new Date().toISOString() }],
        },
      };
    }),

  clearActivityCards: (draftId) =>
    set((s) => {
      const next = { ...s.activityCardsByDraft };
      delete next[draftId];
      return { activityCardsByDraft: next };
    }),
}));