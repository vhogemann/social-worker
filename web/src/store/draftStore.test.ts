import { describe, it, expect, vi, beforeEach, type Mock } from "vitest";
import { useDraftStore } from "./draftStore";
import { useChatStore } from "./chatStore";

vi.mock("../api/drafts", () => ({
  fetchDrafts: vi.fn(),
  createDraft: vi.fn(),
  createReplyDraftFromBlueskyPostUrl: vi.fn(),
  fetchDraft: vi.fn(),
  patchDraft: vi.fn(),
  createPlatformThread: vi.fn(),
  patchPlatformThread: vi.fn(),
  publishPlatformThread: vi.fn(),
  fetchSources: vi.fn(),
  uploadFile: vi.fn(),
  uploadMedia: vi.fn(),
  patchMediaAsset: vi.fn(),
  deleteMediaAsset: vi.fn(),
  fetchPlatformCapabilities: vi.fn(),
}));

import * as draftsApi from "../api/drafts";
const mockFetchDrafts = draftsApi.fetchDrafts as Mock;
const mockCreateDraft = draftsApi.createDraft as Mock;
const mockCreateReplyDraftFromBlueskyPostUrl = draftsApi.createReplyDraftFromBlueskyPostUrl as Mock;
const mockFetchDraft = draftsApi.fetchDraft as Mock;
const mockPatchDraft = draftsApi.patchDraft as Mock;
const mockCreatePlatformThread = draftsApi.createPlatformThread as Mock;
const mockPatchPlatformThread = draftsApi.patchPlatformThread as Mock;
const mockPublishPlatformThread = draftsApi.publishPlatformThread as Mock;
const mockFetchSources = draftsApi.fetchSources as Mock;
const mockUploadFile = draftsApi.uploadFile as Mock;
const mockUploadMedia = draftsApi.uploadMedia as Mock;
const mockPatchMediaAsset = draftsApi.patchMediaAsset as Mock;
const mockDeleteMediaAsset = draftsApi.deleteMediaAsset as Mock;
const mockFetchPlatformCapabilities = draftsApi.fetchPlatformCapabilities as Mock;

const makeDraft = (id = "d1", overrides = {}) => ({
  id,
  title: "Test Draft",
  status: "Draft",
  content: "Hello",
  targetPlatform: "Bluesky",
  canonicalDraftId: null,
  threads: [],
  sources: [],
  mediaAssets: [],
  chatHistory: null,
  chatSummary: null,
  lastSummarizedMessageCount: 0,
  createdAt: "2026-01-01",
  updatedAt: "2026-01-01",
  ...overrides,
});

const makeThread = (id = "t1") => ({
  id,
  draftId: "d1",
  platform: "Bluesky",
  stage: "Draft",
  content: null,
  posts: [],
});

function resetStore() {
  useDraftStore.setState({
    drafts: [],
    activeDraftId: null,
    activePlatform: "Bluesky",
    platformCapabilities: [],
    sources: [],
    loading: false,
  });
}

describe("draftStore", () => {
  beforeEach(() => {
    resetStore();
    vi.clearAllMocks();
    mockFetchSources.mockResolvedValue([]);
    mockFetchPlatformCapabilities.mockResolvedValue([]);
  });

  describe("loadDrafts", () => {
    it("fetches drafts and sets them in state", async () => {
      mockFetchDrafts.mockResolvedValueOnce([makeDraft("d1"), makeDraft("d2")]);

      await useDraftStore.getState().loadDrafts();

      expect(useDraftStore.getState().drafts).toHaveLength(2);
    });

    it("parses chatHistory and calls chatStore saveMessages", async () => {
      const messages = [{ id: "m1", role: "user" }];
      mockFetchDrafts.mockResolvedValueOnce([makeDraft("d1", { chatHistory: JSON.stringify(messages) })]);

      await useDraftStore.getState().loadDrafts();

      const saved = useChatStore.getState().loadMessages("d1");
      expect(saved).toEqual(messages);
    });

    it("ignores invalid chatHistory without throwing", async () => {
      mockFetchDrafts.mockResolvedValueOnce([makeDraft("d1", { chatHistory: "not-json" })]);
      await expect(useDraftStore.getState().loadDrafts()).resolves.toBeUndefined();
    });
  });

  describe("createDraft", () => {
    it("prepends draft to list and sets activeDraftId", async () => {
      useDraftStore.setState({ drafts: [makeDraft("d0")] });
      mockCreateDraft.mockResolvedValueOnce(makeDraft("d-new"));

      const result = await useDraftStore.getState().createDraft("New Draft");

      const state = useDraftStore.getState();
      expect(state.drafts[0].id).toBe("d-new");
      expect(state.activeDraftId).toBe("d-new");
      expect(result.id).toBe("d-new");
    });
  });

  describe("createReplyDraftFromBlueskyPostUrl", () => {
    it("creates draft, sets reply target from URL, and activates returned draft", async () => {
      useDraftStore.setState({ drafts: [makeDraft("d0")] });
      mockCreateReplyDraftFromBlueskyPostUrl.mockResolvedValueOnce(
        makeDraft("d-new", {
          blueskyReplyTarget: {
            replyRootUri: "at://did:plc:root/app.bsky.feed.post/1",
            replyRootCid: "root-cid",
            replyParentUri: "at://did:plc:parent/app.bsky.feed.post/2",
            replyParentCid: "parent-cid",
            replyParentUrl: "https://bsky.app/profile/test/post/2",
            replyParentAuthor: "test",
            replyParentText: "hello",
            replyParentAvatarUrl: "https://cdn.bsky.app/avatar.jpg",
          },
        })
      );

      const result = await useDraftStore.getState().createReplyDraftFromBlueskyPostUrl("https://bsky.app/profile/test/post/2");

  expect(mockCreateReplyDraftFromBlueskyPostUrl).toHaveBeenCalledWith("https://bsky.app/profile/test/post/2", undefined, undefined);
      expect(useDraftStore.getState().activeDraftId).toBe("d-new");
      expect(useDraftStore.getState().drafts[0].id).toBe("d-new");
      expect(result.id).toBe("d-new");
    });
  });

  describe("switchDraft", () => {
    it("fetches draft, updates list, sets activeDraftId, and loads sources", async () => {
      const draft = makeDraft("d1");
      useDraftStore.setState({ drafts: [draft] });
      mockFetchDraft.mockResolvedValueOnce({ ...draft, title: "Loaded" });

      await useDraftStore.getState().switchDraft("d1");

      const state = useDraftStore.getState();
      expect(state.activeDraftId).toBe("d1");
      expect(state.drafts[0].title).toBe("Loaded");
      expect(mockFetchSources).toHaveBeenCalledWith("d1");
    });

    it("parses chatHistory when switching", async () => {
      const msgs = [{ id: "m1" }];
      mockFetchDraft.mockResolvedValueOnce(makeDraft("d1", { chatHistory: JSON.stringify(msgs) }));

      await useDraftStore.getState().switchDraft("d1");

      expect(useChatStore.getState().loadMessages("d1")).toEqual(msgs);
    });
  });

  describe("updateDraftTitle", () => {
    it("patches title and updates draft in list", async () => {
      useDraftStore.setState({ drafts: [makeDraft("d1")] });
      mockPatchDraft.mockResolvedValueOnce(makeDraft("d1", { title: "New Title" }));

      await useDraftStore.getState().updateDraftTitle("d1", "New Title");

      expect(useDraftStore.getState().drafts[0].title).toBe("New Title");
    });
  });

  describe("saveDraftContent", () => {
    it("patches content and updates draft in list", async () => {
      useDraftStore.setState({ drafts: [makeDraft("d1")] });
      mockPatchDraft.mockResolvedValueOnce(makeDraft("d1", { content: "Updated" }));

      await useDraftStore.getState().saveDraftContent("d1", "Updated");

      expect(useDraftStore.getState().drafts[0].content).toBe("Updated");
    });
  });

  describe("archiveDraft / unarchiveDraft", () => {
    it("archiveDraft patches status to Archived", async () => {
      useDraftStore.setState({ drafts: [makeDraft("d1")] });
      mockPatchDraft.mockResolvedValueOnce(makeDraft("d1", { status: "Archived" }));

      await useDraftStore.getState().archiveDraft("d1");

      expect(useDraftStore.getState().drafts[0].status).toBe("Archived");
    });

    it("unarchiveDraft patches status to Editing", async () => {
      useDraftStore.setState({ drafts: [makeDraft("d1", { status: "Archived" })] });
      mockPatchDraft.mockResolvedValueOnce(makeDraft("d1", { status: "Editing" }));

      await useDraftStore.getState().unarchiveDraft("d1");

      expect(useDraftStore.getState().drafts[0].status).toBe("Editing");
    });
  });

  describe("deleteDraft", () => {
    it("removes draft from list", async () => {
      useDraftStore.setState({ drafts: [makeDraft("d1"), makeDraft("d2")], activeDraftId: "d2" });
      mockPatchDraft.mockResolvedValueOnce(undefined);

      await useDraftStore.getState().deleteDraft("d1");

      const state = useDraftStore.getState();
      expect(state.drafts).toHaveLength(1);
      expect(state.drafts[0].id).toBe("d2");
    });

    it("sets activeDraftId to next non-deleted draft when active draft is deleted", async () => {
      useDraftStore.setState({ drafts: [makeDraft("d1"), makeDraft("d2")], activeDraftId: "d1" });
      mockPatchDraft.mockResolvedValueOnce(undefined);

      await useDraftStore.getState().deleteDraft("d1");

      expect(useDraftStore.getState().activeDraftId).toBe("d2");
    });

    it("sets activeDraftId to null when last draft is deleted", async () => {
      useDraftStore.setState({ drafts: [makeDraft("d1")], activeDraftId: "d1" });
      mockPatchDraft.mockResolvedValueOnce(undefined);

      await useDraftStore.getState().deleteDraft("d1");

      expect(useDraftStore.getState().activeDraftId).toBeNull();
    });
  });

  describe("setActivePlatform", () => {
    it("updates activePlatform", () => {
      useDraftStore.getState().setActivePlatform("Twitter");
      expect(useDraftStore.getState().activePlatform).toBe("Twitter");
    });
  });

  describe("createThreadVariant", () => {
    it("calls createPlatformThread and appends thread to draft", async () => {
      useDraftStore.setState({ drafts: [makeDraft("d1")] });
      mockCreatePlatformThread.mockResolvedValueOnce(makeThread("t-new"));

      await useDraftStore.getState().createThreadVariant("d1", "Twitter");

      const draft = useDraftStore.getState().drafts[0];
      expect(draft.threads).toHaveLength(1);
      expect(draft.threads[0].id).toBe("t-new");
    });
  });

  describe("updateThreadStage", () => {
    it("patches thread and updates in draft", async () => {
      useDraftStore.setState({ drafts: [{ ...makeDraft("d1"), threads: [makeThread("t1")] }] });
      mockPatchPlatformThread.mockResolvedValueOnce({ ...makeThread("t1"), stage: "Ready" });

      await useDraftStore.getState().updateThreadStage("d1", "t1", "Ready");

      const thread = useDraftStore.getState().drafts[0].threads[0];
      expect(thread.stage).toBe("Ready");
    });
  });

  describe("publishThread", () => {
    it("publishes thread and refreshes draft", async () => {
      useDraftStore.setState({ drafts: [makeDraft("d1")] });
      mockPublishPlatformThread.mockResolvedValueOnce({ success: true, posts: [] });
      mockFetchDraft.mockResolvedValueOnce(makeDraft("d1"));

      const result = await useDraftStore.getState().publishThread("d1", "t1");

      expect(result.success).toBe(true);
      expect(mockFetchDraft).toHaveBeenCalledWith("d1");
    });
  });

  describe("loadSources", () => {
    it("fetches sources and sets them in state", async () => {
      const sources = [{ id: "s1", draftId: "d1", kind: "Url", reference: "https://x.com", title: null, addedAt: "2026-01-01" }];
      mockFetchSources.mockResolvedValueOnce(sources);

      await useDraftStore.getState().loadSources("d1");

      expect(useDraftStore.getState().sources).toHaveLength(1);
    });
  });

  describe("platform capabilities", () => {
    it("loads platform capabilities into store", async () => {
      mockFetchPlatformCapabilities.mockResolvedValueOnce([
        { platform: "Bluesky", supportsReplyTarget: true },
        { platform: "Twitter", supportsReplyTarget: false },
      ]);

      await useDraftStore.getState().loadPlatformCapabilities();

      expect(useDraftStore.getState().platformCapabilities).toHaveLength(2);
    });

    it("returns true only when platform supports reply target", () => {
      useDraftStore.setState({
        platformCapabilities: [
          { platform: "Bluesky", supportsReplyTarget: true },
          { platform: "Twitter", supportsReplyTarget: false },
        ],
      });

      expect(useDraftStore.getState().supportsReplyTargetForPlatform("Bluesky")).toBe(true);
      expect(useDraftStore.getState().supportsReplyTargetForPlatform("Twitter")).toBe(false);
      expect(useDraftStore.getState().supportsReplyTargetForPlatform(null)).toBe(false);
    });
  });

  describe("uploadFileSource", () => {
    it("calls uploadFile and reloads sources", async () => {
      mockUploadFile.mockResolvedValueOnce({ sourceId: "s1", markdownLink: "[x](url)" });

      const result = await useDraftStore.getState().uploadFileSource("d1", new File([""], "f.txt"));

      expect(result.sourceId).toBe("s1");
      expect(mockFetchSources).toHaveBeenCalledWith("d1");
    });
  });

  describe("updateMediaAltText", () => {
    it("patches media asset and updates in draft", async () => {
      const asset = { id: "m1", draftId: "d1", fileName: "img.png", mimeType: "image/png", altText: "new alt", filePath: "/f", sizeBytes: 100, width: 10, height: 10, createdAt: "2026-01-01" };
      useDraftStore.setState({ drafts: [{ ...makeDraft("d1"), mediaAssets: [{ ...asset, altText: "old" }] }] });
      mockPatchMediaAsset.mockResolvedValueOnce(asset);

      await useDraftStore.getState().updateMediaAltText("m1", "new alt");

      const ma = useDraftStore.getState().drafts[0].mediaAssets![0];
      expect(ma.altText).toBe("new alt");
    });
  });

  describe("saveDraftChat", () => {
    it("patches chatHistory and updates draft", async () => {
      useDraftStore.setState({ drafts: [makeDraft("d1")] });
      const history = JSON.stringify([{ id: "m1" }]);
      mockPatchDraft.mockResolvedValueOnce(makeDraft("d1", { chatHistory: history }));

      await useDraftStore.getState().saveDraftChat("d1", history);

      expect(useDraftStore.getState().drafts[0].chatHistory).toBe(history);
    });
  });
});
