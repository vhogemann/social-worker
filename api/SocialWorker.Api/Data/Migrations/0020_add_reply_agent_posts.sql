ALTER TABLE "Posts" ALTER COLUMN "DraftId" DROP NOT NULL;
ALTER TABLE "Posts" ALTER COLUMN "PlatformThreadId" DROP NOT NULL;

ALTER TABLE "Posts" ADD COLUMN IF NOT EXISTS "UserId" uuid NULL;
ALTER TABLE "Posts" ADD COLUMN IF NOT EXISTS "IsExternal" boolean NOT NULL DEFAULT false;
ALTER TABLE "Posts" ADD COLUMN IF NOT EXISTS "AuthorHandle" character varying(255) NULL;

UPDATE "Posts" p
SET "UserId" = d."UserId"
FROM "Drafts" d
WHERE p."DraftId" = d."Id" AND p."UserId" IS NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Posts_Users_UserId'
    ) THEN
        ALTER TABLE "Posts"
            ADD CONSTRAINT "FK_Posts_Users_UserId"
            FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_Posts_UserId" ON "Posts" ("UserId");

DROP INDEX IF EXISTS "IX_Posts_PlatformThreadId_SegmentIndex";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Posts_PlatformThreadId_SegmentIndex"
    ON "Posts" ("PlatformThreadId", "SegmentIndex")
    WHERE "PlatformThreadId" IS NOT NULL;

CREATE TABLE IF NOT EXISTS "ReplyAgentPosts" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "ReplyAgentId" uuid NOT NULL,
    "PostId" uuid NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "AssignedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    "LastPolledAt" timestamp with time zone NULL,
    CONSTRAINT "FK_ReplyAgentPosts_ReplyAgents" FOREIGN KEY ("ReplyAgentId")
        REFERENCES "ReplyAgents" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ReplyAgentPosts_Posts" FOREIGN KEY ("PostId")
        REFERENCES "Posts" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReplyAgentPosts_ReplyAgentId_PostId"
    ON "ReplyAgentPosts" ("ReplyAgentId", "PostId");
