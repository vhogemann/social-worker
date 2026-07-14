import { useEffect, useRef } from "react";
import { useDataStreamRuntime } from "@assistant-ui/react-data-stream";
import { AssistantRuntimeProvider, type AssistantRuntime, useThread } from "@assistant-ui/react";
import { useEditorStore } from "../store/editorStore";
import { useDraftStore } from "../store/draftStore";
import { useChatStore } from "../store/chatStore";

import { useAuthStore } from "../store/authStore";

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

  const prevDraftIdRef = useRef<string | null>(activeDraftId);
  const prevIsRunningRef = useRef(isRunning);
  const runDraftIdRef = useRef<string | null>(null);

  useEffect(() => {
    const previousDraftId = prevDraftIdRef.current;

    if (previousDraftId && previousDraftId !== activeDraftId) {
      saveCurrentChat(previousDraftId);
      if (isRunning) {
        runtime.thread.cancelRun();
      }
    }

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

    prevDraftIdRef.current = activeDraftId;
  }, [activeDraftId, isRunning, runtime]);

  useEffect(() => {
    if (!prevIsRunningRef.current && isRunning) {
      runDraftIdRef.current = activeDraftId;
    }

    if (prevIsRunningRef.current && !isRunning) {
      const runDraftId = runDraftIdRef.current;
      if (runDraftId) {
        saveCurrentChat(runDraftId);
      }
      runDraftIdRef.current = null;
    }

    prevIsRunningRef.current = isRunning;
  }, [activeDraftId, isRunning]);

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