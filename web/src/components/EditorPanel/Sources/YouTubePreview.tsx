import React from "react";

interface YouTubePreviewProps {
  reference: string;
}

export const YouTubePreview: React.FC<YouTubePreviewProps> = ({ reference }) => {
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
    <div className="h-full flex flex-col">
      {embedUrl ? (
        <div className="h-full w-full rounded-xl overflow-hidden bg-black border border-border/40">
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
    </div>
  );
};
