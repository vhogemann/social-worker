import { describe, expect, it } from "vitest";
import { parseImageSearchResult } from "./ThreadMessageImageSearchPanel";

describe("parseImageSearchResult", () => {
  it("parses image search markdown-style output", () => {
    const text = [
      "Image search results for: 'pineapple':",
      "",
      "- **Pineapple close-up**",
      "  URL: https://images.example.com/pineapple.jpg",
      "  Description: Bright yellow pineapple on table",
      "",
      "- **Tropical fruit basket**",
      "  URL: https://images.example.com/tropical.webp",
      "",
    ].join("\n");

    const parsed = parseImageSearchResult(text);

    expect(parsed).not.toBeNull();
    expect(parsed?.query).toBe("pineapple");
    expect(parsed?.items).toHaveLength(2);
    expect(parsed?.items[0]).toEqual({
      title: "Pineapple close-up",
      url: "https://images.example.com/pineapple.jpg",
      description: "Bright yellow pineapple on table",
    });
    expect(parsed?.items[1]).toEqual({
      title: "Tropical fruit basket",
      url: "https://images.example.com/tropical.webp",
    });
  });

  it("returns null for non-image-search text", () => {
    const parsed = parseImageSearchResult("Hello from assistant.");
    expect(parsed).toBeNull();
  });
});
