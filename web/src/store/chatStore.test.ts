import { describe, it, expect, beforeEach } from "vitest";
import { useChatStore } from "./chatStore";
import type { ExportedMessageRepository } from "@assistant-ui/react";

function makeRepo(id: string): ExportedMessageRepository {
  return { messages: [{ id, role: "user", content: [{ type: "text", text: "hello" }] }] } as unknown as ExportedMessageRepository;
}

function resetStore() {
  useChatStore.setState({ messagesByDraft: {} });
}

describe("chatStore", () => {
  beforeEach(() => {
    resetStore();
  });

  it("has empty messagesByDraft initially", () => {
    expect(useChatStore.getState().messagesByDraft).toEqual({});
  });

  describe("saveMessages", () => {
    it("stores repository keyed by draftId", () => {
      const repo = makeRepo("m1");
      useChatStore.getState().saveMessages("draft-1", repo);

      expect(useChatStore.getState().messagesByDraft["draft-1"]).toEqual(repo);
    });

    it("overwrites existing messages for same draftId", () => {
      useChatStore.getState().saveMessages("draft-1", makeRepo("m1"));
      const updated = makeRepo("m2");
      useChatStore.getState().saveMessages("draft-1", updated);

      expect(useChatStore.getState().messagesByDraft["draft-1"]).toEqual(updated);
    });

    it("stores messages for multiple drafts independently", () => {
      const r1 = makeRepo("m1");
      const r2 = makeRepo("m2");
      useChatStore.getState().saveMessages("draft-1", r1);
      useChatStore.getState().saveMessages("draft-2", r2);

      expect(useChatStore.getState().messagesByDraft["draft-1"]).toEqual(r1);
      expect(useChatStore.getState().messagesByDraft["draft-2"]).toEqual(r2);
    });
  });

  describe("loadMessages", () => {
    it("returns stored repository for draftId", () => {
      const repo = makeRepo("m1");
      useChatStore.getState().saveMessages("draft-1", repo);

      expect(useChatStore.getState().loadMessages("draft-1")).toEqual(repo);
    });

    it("returns null for unknown draftId", () => {
      expect(useChatStore.getState().loadMessages("unknown")).toBeNull();
    });
  });

  describe("clearMessages", () => {
    it("removes messages for the given draftId", () => {
      useChatStore.getState().saveMessages("draft-1", makeRepo("m1"));
      useChatStore.getState().clearMessages("draft-1");

      expect(useChatStore.getState().loadMessages("draft-1")).toBeNull();
    });

    it("does not affect messages for other draftIds", () => {
      const r1 = makeRepo("m1");
      useChatStore.getState().saveMessages("draft-1", r1);
      useChatStore.getState().saveMessages("draft-2", makeRepo("m2"));
      useChatStore.getState().clearMessages("draft-2");

      expect(useChatStore.getState().loadMessages("draft-1")).toEqual(r1);
      expect(useChatStore.getState().loadMessages("draft-2")).toBeNull();
    });

    it("is a no-op for unknown draftId", () => {
      useChatStore.getState().saveMessages("draft-1", makeRepo("m1"));
      useChatStore.getState().clearMessages("unknown");

      expect(Object.keys(useChatStore.getState().messagesByDraft)).toHaveLength(1);
    });
  });
});
