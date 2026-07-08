import { create } from "zustand";

interface EditorState {
  doc: string;
  version: number;
  setDoc: (doc: string) => void;
  applyExternal: (doc: string) => void;
}

export const useEditorStore = create<EditorState>((set) => ({
  doc: "",
  version: 0,
  setDoc: (doc) => set({ doc }),
  applyExternal: (doc) =>
    set((s) => ({ doc, version: s.version + 1 })),
}));