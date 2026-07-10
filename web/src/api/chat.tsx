import { useEffect, useRef } from "react";
import { useDataStreamRuntime } from "@assistant-ui/react-data-stream";
import { AssistantRuntimeProvider, type AssistantRuntime, useThread } from "@assistant-ui/react";
import { useEditorStore } from "../store/editorStore";
import { useDraftStore } from "../store/draftStore";
import { useChatStore } from "../store/chatStore";

import { useAuthStore } from "../store/authStore";

const SYSTEM_PROMPT =
  "You are a helpful assistant that helps the user draft social media threads. " +
  "When the user asks you to write or update content, call replace_editor_content with the full markdown. " +
  "Use --- on its own line to separate thread segments (each segment is one post).";

export function useChatRuntime() {
  const editorDoc = useEditorStore((s) => s.doc);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  return useDataStreamRuntime({
    api: "/api/chat",
    headers: async () => {
      const token = useAuthStore.getState().accessToken;
      return (token ? { Authorization: `Bearer ${token}` } : {}) as Record<string, string>;
    },
    body: {
      system: SYSTEM_PROMPT,
      editor: editorDoc,
      draftId: activeDraftId,
    },
    sendExtraMessageFields: true,
  });
}

let runtimeRef: AssistantRuntime | null = null;

export function saveCurrentChat(draftId: string) {
  if (!runtimeRef) return;
  const exported = runtimeRef.thread.export();
  useChatStore.getState().saveMessages(draftId, exported);
  useDraftStore.getState().saveDraftChat(draftId, JSON.stringify(exported));
}

export function restoreChat(draftId: string) {
  if (!runtimeRef) return;
  const saved = useChatStore.getState().loadMessages(draftId);
  if (saved) {
    runtimeRef.thread.import(saved);
  } else {
    runtimeRef.thread.reset();
  }
}

export function clearChat(draftId: string) {
  useChatStore.getState().clearMessages(draftId);
  if (runtimeRef) {
    runtimeRef.thread.reset();
  }
}

function ChatRuntimeManager({ runtime }: { runtime: AssistantRuntime }) {
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const isRunning = useThread((state) => state.isRunning);

  const isMounted = useRef(false);
  const prevDraftId = useRef(activeDraftId);

  if (prevDraftId.current !== activeDraftId) {
    isMounted.current = false;
    prevDraftId.current = activeDraftId;
  }

  useEffect(() => {
    if (activeDraftId) {
      const saved = useChatStore.getState().loadMessages(activeDraftId);
      if (saved) {
        runtime.thread.import(saved);
      } else {
        runtime.thread.reset();
      }
    } else {
      runtime.thread.reset();
    }
  }, [activeDraftId, runtime]);

  useEffect(() => {
    if (!isMounted.current) {
      isMounted.current = true;
      return;
    }
    if (activeDraftId) {
      saveCurrentChat(activeDraftId);
    }
  }, [isRunning]);

  runtimeRef = runtime;
  return null;
}

export function ChatProvider({ children }: { children: React.ReactNode }) {
  const runtime = useChatRuntime();
  return (
    <AssistantRuntimeProvider runtime={runtime}>
      <ChatRuntimeManager runtime={runtime} />
      {children}
    </AssistantRuntimeProvider>
  );
}