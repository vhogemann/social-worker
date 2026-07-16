import { create } from "zustand";

interface EditorState {
  doc: string;
  version: number;
  panelMode: "edit" | "preview";
  setDoc: (doc: string) => void;
  applyExternal: (doc: string) => void;
  setPanelMode: (mode: "edit" | "preview") => void;
}

export const useEditorStore = create<EditorState>((set) => ({
  doc: "",
  version: 0,
  panelMode: "edit",
  setDoc: (doc) => set({ doc }),
  applyExternal: (doc) =>
    set((s) => ({ doc, version: s.version + 1 })),
  setPanelMode: (panelMode) => set({ panelMode }),
}));