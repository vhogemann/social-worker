import ReactMarkdown from "react-markdown";
import { ThreadMessageImageSearchPanel, parseImageSearchResult } from "./ThreadMessageImageSearchPanel";
import { resolveMediaUri, rewriteMediaUris } from "./ThreadMessageMedia";
import { parsePostPreview, ThreadMessagePostPreview } from "./ThreadMessagePostPreview";
import { ThreadMessageSearchResultsPanel, parseWebSearchResults } from "./ThreadMessageSearchResultsPanel";
import { parseValidationReport, ThreadMessageValidationReport } from "./ThreadMessageValidationReport";

export function ThreadMessageTextPart({ text }: { text: string }) {
  const parsedSearchResults = parseWebSearchResults(text);
  if (parsedSearchResults) {
    return (
      <ThreadMessageSearchResultsPanel
        query={parsedSearchResults.query}
        results={parsedSearchResults.results}
      />
    );
  }

  const parsedImageSearch = parseImageSearchResult(text);

  if (parsedImageSearch) {
    return (
      <ThreadMessageImageSearchPanel
        query={parsedImageSearch.query}
        items={parsedImageSearch.items}
      />
    );
  }

  const parsedPostPreview = parsePostPreview(text);

  const parsedValidationReport = parseValidationReport(text);
  if (parsedValidationReport) {
    return <ThreadMessageValidationReport chips={parsedValidationReport.chips} />;
  }

  if (parsedPostPreview) {
    return <ThreadMessagePostPreview posts={parsedPostPreview.posts} />;
  }

  return (
    <div className="prose prose-invert prose-sm max-w-none">
      <ReactMarkdown
        components={{
          img: ({ src, alt }) => (
            <img
              src={resolveMediaUri(src)}
              alt={alt ?? ""}
              loading="lazy"
              className="max-w-full rounded-md border border-border"
            />
          ),
          a: ({ href, children }) => (
            <a href={resolveMediaUri(href)} target="_blank" rel="noreferrer">
              {children}
            </a>
          ),
        }}
      >
        {rewriteMediaUris(text)}
      </ReactMarkdown>
    </div>
  );
}
