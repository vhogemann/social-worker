import { describe, it, expect, beforeEach } from "vitest";
import { useEditorStore } from "./editorStore";

function resetStore() {
  useEditorStore.setState({ doc: "", version: 0 });
}

describe("editorStore", () => {
  beforeEach(() => {
    resetStore();
  });

  it("has correct initial state", () => {
    const state = useEditorStore.getState();
    expect(state.doc).toBe("");
    expect(state.version).toBe(0);
  });

  describe("setDoc", () => {
    it("updates doc without incrementing version", () => {
      useEditorStore.getState().setDoc("Hello");

      const state = useEditorStore.getState();
      expect(state.doc).toBe("Hello");
      expect(state.version).toBe(0);
    });

    it("overwrites previous doc", () => {
      useEditorStore.getState().setDoc("First");
      useEditorStore.getState().setDoc("Second");

      expect(useEditorStore.getState().doc).toBe("Second");
    });
  });

  describe("applyExternal", () => {
    it("updates doc and increments version", () => {
      useEditorStore.getState().applyExternal("External content");

      const state = useEditorStore.getState();
      expect(state.doc).toBe("External content");
      expect(state.version).toBe(1);
    });

    it("increments version on each call", () => {
      useEditorStore.getState().applyExternal("First");
      useEditorStore.getState().applyExternal("Second");
      useEditorStore.getState().applyExternal("Third");

      const state = useEditorStore.getState();
      expect(state.doc).toBe("Third");
      expect(state.version).toBe(3);
    });
  });
});
