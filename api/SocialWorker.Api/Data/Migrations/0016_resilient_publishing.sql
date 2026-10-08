ALTER TABLE "Posts"
    ADD COLUMN IF NOT EXISTS "PublishedText" text NULL,
    ADD COLUMN IF NOT EXISTS "RemoteCid" character varying(255) NULL;

ALTER TABLE "PlatformThreads"
    ADD COLUMN IF NOT EXISTS "LastPublishError" text NULL;
