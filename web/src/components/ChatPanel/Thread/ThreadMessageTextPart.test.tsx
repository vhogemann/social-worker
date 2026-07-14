import { describe, expect, it } from "vitest";
import { parsePostPreview } from "./ThreadMessagePostPreview";
import { resolveMediaUri, rewriteMediaUris } from "./ThreadMessageMedia";

describe("resolveMediaUri", () => {
  it("maps media:// GUID URIs to api media endpoints", () => {
    expect(resolveMediaUri("media://123e4567-e89b-12d3-a456-426614174000")).toBe(
      "/api/media/123e4567-e89b-12d3-a456-426614174000"
    );
  });

  it("keeps non-media URIs unchanged", () => {
    expect(resolveMediaUri("https://example.com/a.jpg")).toBe("https://example.com/a.jpg");
  });

  it("rewrites media URIs in markdown text", () => {
    const text = "![alt](media://123e4567-e89b-12d3-a456-426614174000)";
    expect(rewriteMediaUris(text)).toContain("/api/media/123e4567-e89b-12d3-a456-426614174000");
  });
});

describe("parsePostPreview", () => {
  it("parses multi-post thread content separated by ---", () => {
    const text = "Post one\n\n![alt](media://123e4567-e89b-12d3-a456-426614174000)\n\n---\n\nPost two";
    const parsed = parsePostPreview(text);

    expect(parsed).not.toBeNull();
    expect(parsed?.posts).toHaveLength(2);
  });

  it("returns null when content is a single post", () => {
    const parsed = parsePostPreview("Single post only");
    expect(parsed).toBeNull();
  });

  it("excludes assistant intro and trailing publication commentary around numbered posts", () => {
    const text = [
      "The Bluesky thread is drafted, validated, and set up with all constraints met! 🌿✨",
      "",
      "Here is the finalized two-post thread:",
      "",
      "**Post 1:**",
      "First post body",
      "",
      "**Post 2:**",
      "Second post body",
      "",
      "---",
      "",
      "The thread is ready for publication on Bluesky.",
      "Let me know if you would like me to publish it, or if you need any adjustments!",
    ].join("\n");

    const parsed = parsePostPreview(text);

    expect(parsed).not.toBeNull();
    expect(parsed?.posts).toEqual(["First post body", "Second post body"]);
  });
});
