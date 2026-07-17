import { describe, it, expect, beforeEach } from "vitest";
import { useChatStore } from "./chatStore";
import type { ExportedMessageRepository } from "@assistant-ui/react";

function makeRepo(id: string): ExportedMessageRepository {
  return {
    messages: [
      {
        parentId: null,
        message: { id, role: "user", content: [{ type: "text", text: "hello" }] } as any,
      },
    ],
  };
}

function makeLegacyRepo(): unknown {
  return {
    messages: [
      {
        id: "dfd0a36e-1ec3-4c7b-af62-b994861ad2a8",
        role: "user",
        content: [{ type: "text", text: "Please draft a thread from this source" }],
      },
      {
        id: "15fcfa90-cbba-43e0-8d00-11d02a714775",
        role: "assistant",
        content: [{ type: "text", text: "I encountered an issue trying to link the source" }],
      },
    ],
  };
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

    it("normalizes legacy message array repositories", () => {
      useChatStore.getState().saveMessages("draft-legacy", makeLegacyRepo() as ExportedMessageRepository);

      const stored = useChatStore.getState().loadMessages("draft-legacy");
      expect(stored).not.toBeNull();
      expect(stored?.messages).toHaveLength(2);
      expect(stored?.messages[0]?.message.role).toBe("user");
      expect(stored?.messages[1]?.message.role).toBe("assistant");
      expect(stored?.messages[0]?.message.content[0]?.type).toBe("text");
      expect(stored?.messages[1]?.message.content[0]?.type).toBe("text");
    });

    it("drops malformed content parts without type", () => {
      const malformed = {
        messages: [
          {
            id: "m1",
            role: "assistant",
            content: [{ text: "missing type" }, { type: "text", text: "valid" }],
          },
        ],
      };

      useChatStore.getState().saveMessages("draft-malformed", malformed as unknown as ExportedMessageRepository);
      const stored = useChatStore.getState().loadMessages("draft-malformed");

      expect(stored?.messages).toHaveLength(1);
      expect(stored?.messages[0]?.message.content).toHaveLength(1);
      expect(stored?.messages[0]?.message.content[0]?.type).toBe("text");
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
