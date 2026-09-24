import React, { useCallback, useEffect, useMemo, useRef } from "react";
import { useDataStreamRuntime } from "@assistant-ui/react-data-stream";
import { AssistantRuntimeProvider, type AssistantRuntime, useThread } from "@assistant-ui/react";
import { useAuthStore } from "../store/authStore";
import { patchDraft } from "./drafts";

export interface ScopedChatProviderProps {
  draftId: string;
  initialChatHistory?: string | null;
  editorDoc: string;
  children: React.ReactNode;
}

export function sanitizeChatHistory<T>(data: T): T {
  if (!data || typeof data !== "object") return data;

  const raw = data as Record<string, any>;
  if (!Array.isArray(raw.messages)) return data;

  const sanitizedMessages = raw.messages.map((entry: any) => {
    if (!entry || !entry.message || typeof entry.message !== "object") return entry;

    const msg = { ...entry.message };

    if (msg.status?.type === "running") {
      msg.status = { type: "complete", reason: "stop" };
    }

    if (Array.isArray(msg.content)) {
      msg.content = msg.content
        .filter((part: any) => !(part?.type === "text" && part.text === "" && part.status?.type === "running"))
        .map((part: any) => {
          if (part && typeof part === "object" && part.status?.type === "running") {
            return {
              ...part,
              status: { type: "complete", reason: "stop" },
            };
          }
          return part;
        });
    }

    if (msg.metadata && typeof msg.metadata === "object" && Array.isArray(msg.metadata.steps)) {
      msg.metadata = {
        ...msg.metadata,
        steps: msg.metadata.steps.map((step: any) => {
          if (step && typeof step === "object" && step.state === "running") {
            return {
              ...step,
              state: "finished",
              finishReason: step.finishReason || "stop",
            };
          }
          return step;
        }),
      };
    }

    return {
      ...entry,
      message: msg,
    };
  });

  return {
    ...raw,
    messages: sanitizedMessages,
  } as T;
}

function ChatRuntimeInner({
  draftId,
  initialChatHistory,
  runtime,
  children,
}: {
  draftId: string;
  initialChatHistory?: string | null;
  runtime: AssistantRuntime;
  children: React.ReactNode;
}) {
  const isRunning = useThread((state) => state.isRunning);
  const prevIsRunningRef = useRef(isRunning);
  const initializedRef = useRef(false);

  // Initialize chat history on mount
  useEffect(() => {
    if (initializedRef.current) return;
    initializedRef.current = true;

    if (initialChatHistory) {
      try {
        const parsed = JSON.parse(initialChatHistory);
        const sanitized = sanitizeChatHistory(parsed);
        runtime.thread.import(sanitized);
      } catch (err) {
        console.error("Failed to parse initial chat history for draft", draftId, err);
      }
    }
  }, [draftId, initialChatHistory, runtime]);

  // Autosave when chat run finishes
  useEffect(() => {
    if (prevIsRunningRef.current && !isRunning) {
      const exported = runtime.thread.export();
      const sanitized = sanitizeChatHistory(exported);
      void patchDraft(draftId, { chatHistory: JSON.stringify(sanitized) }).catch((err) => {
        console.error("Failed to auto-save chat history for draft", draftId, err);
      });
    }
    prevIsRunningRef.current = isRunning;
  }, [draftId, isRunning, runtime]);

  // Save when unmounting
  useEffect(() => {
    return () => {
      try {
        const exported = runtime.thread.export();
        if (exported && exported.messages && exported.messages.length > 0) {
          const sanitized = sanitizeChatHistory(exported);
          void patchDraft(draftId, { chatHistory: JSON.stringify(sanitized) }).catch(() => {});
        }
      } catch {
        // Ignored
      }
    };
  }, [draftId, runtime]);

  return <>{children}</>;
}

export const ScopedChatProvider: React.FC<ScopedChatProviderProps> = ({
  draftId,
  initialChatHistory,
  editorDoc,
  children,
}) => {
  const headers = useCallback(async () => {
    const token = useAuthStore.getState().accessToken;
    return (token ? { Authorization: `Bearer ${token}` } : {}) as Record<string, string>;
  }, []);

  const body = useMemo(
    () => ({
      editor: editorDoc,
      draftId,
    }),
    [editorDoc, draftId]
  );

  const runtime = useDataStreamRuntime({
    api: "/api/chat",
    headers,
    body,
    sendExtraMessageFields: true,
  }) as unknown as AssistantRuntime;

  return (
    <AssistantRuntimeProvider runtime={runtime}>
      <ChatRuntimeInner draftId={draftId} initialChatHistory={initialChatHistory} runtime={runtime}>
        {children}
      </ChatRuntimeInner>
    </AssistantRuntimeProvider>
  );
};