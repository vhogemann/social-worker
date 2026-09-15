-- Migration 0014: Add UserId to Templates for per-user scoping
ALTER TABLE "Templates" ADD COLUMN IF NOT EXISTS "UserId" uuid NULL;

-- Backfill any existing templates to the first admin user
UPDATE "Templates"
SET "UserId" = (
    SELECT "Id" FROM "Users" WHERE "Role" = 'Admin' ORDER BY "CreatedAt" LIMIT 1
)
WHERE "UserId" IS NULL;

-- If there were templates but no admin user existed, assign to the earliest user
UPDATE "Templates"
SET "UserId" = (
    SELECT "Id" FROM "Users" ORDER BY "CreatedAt" LIMIT 1
)
WHERE "UserId" IS NULL;

-- If there are still templates without a user, remove them
DELETE FROM "Templates" WHERE "UserId" IS NULL;

-- Make UserId NOT NULL and add foreign key and index
ALTER TABLE "Templates" ALTER COLUMN "UserId" SET NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'FK_Templates_Users_UserId'
    ) THEN
        ALTER TABLE "Templates"
        ADD CONSTRAINT "FK_Templates_Users_UserId"
        FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_Templates_UserId_Name" ON "Templates" ("UserId", "Name");
