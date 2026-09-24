import { describe, it, expect } from "vitest";
import { sanitizeChatHistory } from "./chat";

describe("sanitizeChatHistory", () => {
  it("returns null or undefined as-is", () => {
    expect(sanitizeChatHistory(null)).toBeNull();
    expect(sanitizeChatHistory(undefined)).toBeUndefined();
  });

  it("returns invalid shapes unchanged", () => {
    expect(sanitizeChatHistory("plain text")).toBe("plain text");
    expect(sanitizeChatHistory(123)).toBe(123);
    expect(sanitizeChatHistory({})).toEqual({});
  });

  it("converts running message status and running content parts to complete", () => {
    const rawHistory = {
      headId: "msg-2",
      messages: [
        {
          message: {
            id: "msg-1",
            role: "user",
            content: [{ type: "text", text: "Hello" }],
          },
          parentId: null,
        },
        {
          message: {
            id: "msg-2",
            role: "assistant",
            status: { type: "running" },
            content: [
              {
                type: "tool-call",
                toolCallId: "call-1",
                toolName: "fetch_source",
                state: "result",
                status: { type: "running" },
                result: "Some result",
              },
              {
                type: "text",
                text: "Drafting response...",
                status: { type: "running" },
              },
              {
                type: "text",
                text: "",
                status: { type: "running" },
              },
            ],
            metadata: {
              steps: [
                {
                  state: "running",
                  messageId: "msg-2",
                  isContinued: true,
                },
              ],
            },
          },
          parentId: "msg-1",
        },
      ],
    };

    const sanitized = sanitizeChatHistory(rawHistory) as any;

    expect(sanitized.messages[1].message.status).toEqual({
      type: "complete",
      reason: "stop",
    });

    const content = sanitized.messages[1].message.content;
    expect(content[0].status).toEqual({ type: "complete", reason: "stop" });
    expect(content[1].status).toEqual({ type: "complete", reason: "stop" });

    // Empty running text should be stripped or completed
    const emptyParts = content.filter(
      (c: any) => c.type === "text" && c.text === "" && c.status?.type === "running"
    );
    expect(emptyParts.length).toBe(0);

    const steps = sanitized.messages[1].message.metadata.steps;
    expect(steps[0].state).toBe("finished");
    expect(steps[0].finishReason).toBe("stop");
  });
});
