import { describe, it, expect, beforeEach, vi } from "vitest";
import { useDraftStore } from "../store/draftStore";

globalThis.fetch = vi.fn();

describe("draftStore", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useDraftStore.setState({
      drafts: [],
      activeDraftId: null,
      activePlatform: "Bluesky",
      loading: false,
    });
  });

  it("should load drafts correctly", async () => {
    const mockDrafts = [
      { id: "1", title: "First Draft", status: "Editing", content: "some text", threads: [], createdAt: "", updatedAt: "" }
    ];
    (globalThis.fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => mockDrafts,
    });

    await useDraftStore.getState().loadDrafts();
    expect(useDraftStore.getState().drafts).toEqual(mockDrafts);
  });

  it("should create draft and set activeDraftId", async () => {
    const mockNewDraft = {
      id: "2",
      title: "New Draft",
      status: "Editing",
      content: "",
      threads: [{ id: "t1", draftId: "2", platform: "Bluesky", stage: "Draft", content: "" }],
      createdAt: "",
      updatedAt: ""
    };
    (globalThis.fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 201,
      json: async () => mockNewDraft,
    });

    await useDraftStore.getState().createDraft();
    expect(useDraftStore.getState().activeDraftId).toBe("2");
    expect(useDraftStore.getState().drafts[0]).toEqual(mockNewDraft);
  });

  it("should create platform thread variant correctly", async () => {
    const initialDraft = { id: "3", title: "Test Draft", status: "Editing", content: "", threads: [], createdAt: "", updatedAt: "" };
    useDraftStore.setState({ drafts: [initialDraft], activeDraftId: "3" });

    const mockThread = { id: "t2", draftId: "3", platform: "Twitter", stage: "Draft", content: "" };
    (globalThis.fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 201,
      json: async () => mockThread,
    });

    await useDraftStore.getState().createThreadVariant("3", "Twitter");
    expect(useDraftStore.getState().drafts[0].threads).toEqual([mockThread]);
  });

  it("should update thread stage correctly", async () => {
    const mockThread = { id: "t2", draftId: "3", platform: "Twitter", stage: "Draft", content: "" };
    const initialDraft = { id: "3", title: "Test Draft", status: "Editing", content: "", threads: [mockThread], createdAt: "", updatedAt: "" };
    useDraftStore.setState({ drafts: [initialDraft], activeDraftId: "3" });

    const mockUpdatedThread = { ...mockThread, stage: "Ready" };
    (globalThis.fetch as any).mockResolvedValueOnce({
      ok: true,
      status: 200,
      json: async () => mockUpdatedThread,
    });

    await useDraftStore.getState().updateThreadStage("3", "t2", "Ready");
    expect(useDraftStore.getState().drafts[0].threads[0].stage).toBe("Ready");
  });
});
