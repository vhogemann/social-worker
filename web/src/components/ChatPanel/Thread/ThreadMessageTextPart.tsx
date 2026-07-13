import ReactMarkdown from "react-markdown";
import { ThreadMessageImageSearchPanel, parseImageSearchResult } from "./ThreadMessageImageSearchPanel";
import { ThreadMessageSearchResultsPanel, parseWebSearchResults } from "./ThreadMessageSearchResultsPanel";

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

  return (
    <div className="prose prose-invert prose-sm max-w-none">
      <ReactMarkdown>{text}</ReactMarkdown>
    </div>
  );
}
