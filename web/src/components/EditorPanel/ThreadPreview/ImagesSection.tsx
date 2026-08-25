import React from "react";
import { useDraftStore } from "../../../store/draftStore";
import { AltTextEditor } from "./AltTextEditor";

interface ImagesSectionProps {
  images: { alt: string; id: string; raw: string }[];
  onRevert: (img: { alt: string; id: string; raw: string }) => Promise<void>;
}

export const ImagesSection: React.FC<ImagesSectionProps> = ({ images, onRevert }) => {
  const drafts = useDraftStore((s) => s.drafts);
  const activeDraftId = useDraftStore((s) => s.activeDraftId);
  const activeDraft = drafts.find((d) => d.id === activeDraftId);

  return (
    <>
      {images.map((img) => {
        const asset = activeDraft?.mediaAssets?.find((m) => m.id === img.id);
        const currentAlt = asset?.altText ?? img.alt ?? "";

        return (
          <div key={img.id} className="mt-3 relative group/img rounded-xl overflow-hidden border border-zinc-200 dark:border-zinc-800 bg-zinc-100 dark:bg-zinc-900">
            <img
              src={`/api/media/${img.id}`}
              alt={currentAlt}
              className="w-full h-auto max-h-[560px] object-cover block rounded-xl"
            />
            <AltTextEditor
              mediaId={img.id}
              initialAlt={currentAlt}
              onRevert={() => onRevert(img)}
            />
          </div>
        );
      })}
    </>
  );
};
