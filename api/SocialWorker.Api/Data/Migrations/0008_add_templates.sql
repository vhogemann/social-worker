-- Create Templates table for declarative image-engine templates
CREATE TABLE IF NOT EXISTS "Templates" (
    "Id" uuid NOT NULL,
    "Name" character varying(150) NOT NULL,
    "Description" character varying(1000) NULL,
    "Width" integer NOT NULL DEFAULT 1080,
    "Height" integer NOT NULL DEFAULT 1080,
    "Schema" text NOT NULL,
    "ThumbnailMediaAssetId" uuid NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Templates" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Templates_MediaAssets_ThumbnailMediaAssetId" FOREIGN KEY ("ThumbnailMediaAssetId") REFERENCES "MediaAssets" ("Id") ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS "IX_Templates_Name" ON "Templates" ("Name");
CREATE INDEX IF NOT EXISTS "IX_Templates_ThumbnailMediaAssetId" ON "Templates" ("ThumbnailMediaAssetId");
