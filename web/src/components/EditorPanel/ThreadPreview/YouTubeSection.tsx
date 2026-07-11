import React from "react";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { faPlay } from "@fortawesome/free-solid-svg-icons";
import { extractVideoId } from "./utils";

interface YouTubeSectionProps {
  youtubeUrls: string[];
}

export const YouTubeSection: React.FC<YouTubeSectionProps> = ({ youtubeUrls }) => {
  return (
    <>
      {youtubeUrls.map((url) => {
        const videoId = extractVideoId(url);
        if (!videoId) return null;

        const thumbnail = `https://img.youtube.com/vi/${videoId}/hqdefault.jpg`;
        return (
          <a
            key={url}
            href={url}
            target="_blank"
            rel="noopener noreferrer"
            className="mt-3 block overflow-hidden rounded-xl border border-zinc-200 dark:border-zinc-800 bg-zinc-50 dark:bg-zinc-900 hover:opacity-95 transition relative group/yt"
          >
            <div className="relative aspect-video w-full bg-black flex items-center justify-center">
              <img
                src={thumbnail}
                alt="YouTube Video Thumbnail"
                className="w-full h-full object-cover opacity-90 group-hover/yt:scale-105 transition duration-300"
              />
              <div className="absolute h-12 w-12 rounded-full bg-red-600/90 flex items-center justify-center shadow-lg group-hover/yt:bg-red-600 group-hover/yt:scale-110 transition duration-150">
                <FontAwesomeIcon icon={faPlay} className="w-6 h-6 text-white ml-0.5" />
              </div>
            </div>
            <div className="p-3 border-t border-zinc-200 dark:border-zinc-800">
              <div className="text-xs text-zinc-500 font-mono">youtube.com</div>
              <div className="text-sm font-semibold text-zinc-800 dark:text-zinc-200 truncate mt-0.5">
                Watch YouTube Video
              </div>
            </div>
          </a>
        );
      })}
    </>
  );
};
