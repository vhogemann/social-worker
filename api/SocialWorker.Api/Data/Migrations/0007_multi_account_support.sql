-- 1. Replace unique index on Accounts from (UserId, Platform) to (UserId, Platform, Handle)
DROP INDEX IF EXISTS "IX_Accounts_UserId_Platform";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Accounts_UserId_Platform_Handle" ON "Accounts" ("UserId", "Platform", "Handle");

-- 2. Add TargetAccountId to Drafts
ALTER TABLE "Drafts" ADD COLUMN IF NOT EXISTS "TargetAccountId" uuid NULL;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Drafts_Accounts_TargetAccountId') THEN
        ALTER TABLE "Drafts" ADD CONSTRAINT "FK_Drafts_Accounts_TargetAccountId" 
            FOREIGN KEY ("TargetAccountId") REFERENCES "Accounts" ("Id") ON DELETE SET NULL;
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS "IX_Drafts_TargetAccountId" ON "Drafts" ("TargetAccountId");

-- 3. Add AccountId to PlatformThreads
ALTER TABLE "PlatformThreads" ADD COLUMN IF NOT EXISTS "AccountId" uuid NULL;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_PlatformThreads_Accounts_AccountId') THEN
        ALTER TABLE "PlatformThreads" ADD CONSTRAINT "FK_PlatformThreads_Accounts_AccountId" 
            FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE SET NULL;
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS "IX_PlatformThreads_AccountId" ON "PlatformThreads" ("AccountId");

-- 4. Add AccountId to AutomationWorkflowDestinations
ALTER TABLE "AutomationWorkflowDestinations" ADD COLUMN IF NOT EXISTS "AccountId" uuid NULL;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_AutomationWorkflowDestinations_Accounts_AccountId') THEN
        ALTER TABLE "AutomationWorkflowDestinations" ADD CONSTRAINT "FK_AutomationWorkflowDestinations_Accounts_AccountId" 
            FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE SET NULL;
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS "IX_AutomationWorkflowDestinations_AccountId" ON "AutomationWorkflowDestinations" ("AccountId");
