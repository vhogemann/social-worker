-- Make DraftId column in MediaAssets nullable to support standalone/template assets
ALTER TABLE "MediaAssets" ALTER COLUMN "DraftId" DROP NOT NULL;
