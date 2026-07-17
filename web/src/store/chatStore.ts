import { create } from "zustand";
import { ExportedMessageRepository as ExportedMessageRepositoryUtils, type ExportedMessageRepository } from "@assistant-ui/react";

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

function normalizeMessageRepository(repo: unknown): ExportedMessageRepository | null {
  const now = Date.now();
  const normalizeThreadMessageLike = (value: unknown, index: number): Record<string, unknown> | null => {
    if (!value || typeof value !== "object") return null;
    const message = value as Record<string, unknown>;
    const rawContent = Array.isArray(message.content)
      ? message.content
      : typeof message.content === "string"
        ? [{ type: "text", text: message.content }]
        : [];

    const normalizedContent = rawContent
      .filter((part): part is Record<string, unknown> => !!part && typeof part === "object")
      .filter((part) => typeof part.type === "string")
      .map((part) => {
        if (part.type === "text") {
          return {
            ...part,
            text: typeof part.text === "string" ? part.text : "",
          };
        }
        return part;
      });

    return {
      ...message,
      role: typeof message.role === "string" ? message.role : "assistant",
      content: normalizedContent,
      id:
        typeof message.id === "string" && message.id.length > 0
          ? message.id
          : `legacy-${now}-${index}`,
    };
  };

  const normalizeLegacyMessages = (values: unknown[]) => {
    return values
      .map((value, index) => normalizeThreadMessageLike(value, index))
      .filter((value): value is Record<string, unknown> => !!value);
  };

  const normalizeRepositoryItems = (values: unknown[]): ExportedMessageRepository["messages"] => {
    const looksLikeRepositoryItems = values.every(
      (value) => !!value && typeof value === "object" && "message" in (value as Record<string, unknown>)
    );

    if (!looksLikeRepositoryItems) {
      const normalizedMessages = normalizeLegacyMessages(values);
      return ExportedMessageRepositoryUtils.fromArray(normalizedMessages as any[]).messages;
    }

    return values
      .map((value, index) => {
        const item = value as Record<string, unknown>;
        const normalizedMessage = normalizeThreadMessageLike(item.message, index);
        if (!normalizedMessage) return null;
        const parentId = typeof item.parentId === "string" ? item.parentId : null;
        return { message: normalizedMessage as any, parentId };
      })
      .filter((value): value is ExportedMessageRepository["messages"][number] => !!value);
  };

  const candidate = Array.isArray(repo) ? { messages: repo } : repo;
  if (!candidate || typeof candidate !== "object") return null;

  const rawMessages = (candidate as { messages?: unknown }).messages;
  if (!Array.isArray(rawMessages)) return null;

  const normalizedItems = normalizeRepositoryItems(rawMessages);

  return {
    messages: normalizedItems,
  };
}

export const useChatStore = create<ChatStore>((set, get) => ({
  messagesByDraft: {},
  activityCardsByDraft: {},

  saveMessages: (draftId, repo) => {
    const normalized = normalizeMessageRepository(repo);
    if (!normalized) return;
    set((s) => ({
      messagesByDraft: { ...s.messagesByDraft, [draftId]: normalized },
    }));
  },

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