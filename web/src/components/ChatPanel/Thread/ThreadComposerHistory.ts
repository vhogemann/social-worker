export type PromptHistoryState = {
  history: string[];
  historyIndex: number | null;
  draftBuffer: string;
  composerText: string;
};

export type PromptHistoryDirection = "up" | "down";

export type PromptHistoryResult = {
  consumed: boolean;
  nextText: string;
  nextHistoryIndex: number | null;
  nextDraftBuffer: string;
};

export function navigatePromptHistory(
  direction: PromptHistoryDirection,
  state: PromptHistoryState
): PromptHistoryResult {
  const { history, historyIndex, draftBuffer, composerText } = state;

  if (history.length === 0) {
    return {
      consumed: false,
      nextText: composerText,
      nextHistoryIndex: historyIndex,
      nextDraftBuffer: draftBuffer,
    };
  }

  if (direction === "up") {
    if (historyIndex === null) {
      const nextHistoryIndex = history.length - 1;
      return {
        consumed: true,
        nextText: history[nextHistoryIndex],
        nextHistoryIndex,
        nextDraftBuffer: composerText,
      };
    }

    const nextHistoryIndex = Math.max(0, historyIndex - 1);
    return {
      consumed: true,
      nextText: history[nextHistoryIndex],
      nextHistoryIndex,
      nextDraftBuffer: draftBuffer,
    };
  }

  if (historyIndex === null) {
    return {
      consumed: false,
      nextText: composerText,
      nextHistoryIndex: historyIndex,
      nextDraftBuffer: draftBuffer,
    };
  }

  const nextHistoryIndex = historyIndex + 1;
  if (nextHistoryIndex >= history.length) {
    return {
      consumed: true,
      nextText: draftBuffer,
      nextHistoryIndex: null,
      nextDraftBuffer: draftBuffer,
    };
  }

  return {
    consumed: true,
    nextText: history[nextHistoryIndex],
    nextHistoryIndex,
    nextDraftBuffer: draftBuffer,
  };
}
