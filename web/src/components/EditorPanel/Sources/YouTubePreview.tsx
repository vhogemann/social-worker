import React from "react";

interface YouTubePreviewProps {
  reference: string;
  content: string | null;
}

export const YouTubePreview: React.FC<YouTubePreviewProps> = ({ reference, content }) => {
  const getYouTubeEmbedUrl = (url: string): string | null => {
    const regExp = /^.*(youtu.be\/|v\/|u\/\w\/|embed\/|watch\?v=|\&v=)([^#\&\?]*).*/;
    const match = url.match(regExp);
    if (match && match[2].length === 11) {
      return `https://www.youtube.com/embed/${match[2]}`;
    }
    return null;
  };

  const embedUrl = getYouTubeEmbedUrl(reference);

  return (
    <div className="space-y-4 h-full flex flex-col">
      {embedUrl ? (
        <div className="aspect-video w-full rounded-xl overflow-hidden bg-black shrink-0 border border-border/40">
          <iframe
            src={embedUrl}
            title="YouTube video player"
            className="w-full h-full border-0"
            allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
            allowFullScreen
          />
        </div>
      ) : (
        <div className="p-4 bg-zinc-900 border border-border rounded-xl text-center text-xs text-muted font-mono shrink-0">
          Invalid YouTube video link.
        </div>
      )}
      {content && (
        <div className="flex-1 min-h-0 flex flex-col">
          <h4 className="text-xs font-mono text-muted uppercase tracking-wider mb-2">Transcript / Description</h4>
          <pre className="flex-1 overflow-y-auto p-4 rounded-xl bg-bg/40 border border-border/40 text-xs text-zinc-300 whitespace-pre-wrap break-words font-mono leading-relaxed">
            {content}
          </pre>
        </div>
      )}
    </div>
  );
};
