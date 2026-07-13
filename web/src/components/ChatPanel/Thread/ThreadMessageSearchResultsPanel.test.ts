import { describe, expect, it } from "vitest";
import { parseWebSearchResults } from "./ThreadMessageSearchResultsPanel";

describe("parseWebSearchResults", () => {
  it("parses JSON web search payload", () => {
    const text = JSON.stringify(
      {
        query: "social worker",
        usageNotes: ["note"],
        results: [
          {
            rank: 1,
            title: "Example Result",
            url: "https://example.com/article",
            snippet: "Summary text",
          },
        ],
      },
      null,
      2
    );

    const parsed = parseWebSearchResults(text);
    expect(parsed).not.toBeNull();
    expect(parsed?.query).toBe("social worker");
    expect(parsed?.results).toHaveLength(1);
    expect(parsed?.results[0].title).toBe("Example Result");
  });

  it("returns null for non-json text", () => {
    expect(parseWebSearchResults("hello world")).toBeNull();
  });
});
