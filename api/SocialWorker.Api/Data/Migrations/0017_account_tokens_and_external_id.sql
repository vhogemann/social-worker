ALTER TABLE "Accounts"
    ADD COLUMN IF NOT EXISTS "ExternalId" character varying(255) NULL,
    ADD COLUMN IF NOT EXISTS "TokenExpiresAt" timestamp with time zone NULL;

CREATE INDEX IF NOT EXISTS "IX_Accounts_UserId_Platform_ExternalId"
    ON "Accounts" ("UserId", "Platform", "ExternalId");
