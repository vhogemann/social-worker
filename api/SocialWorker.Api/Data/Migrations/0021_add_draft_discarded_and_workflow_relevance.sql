ALTER TABLE "Drafts" ADD COLUMN IF NOT EXISTS "DiscardReason" text NULL;
ALTER TABLE "Drafts" ADD COLUMN IF NOT EXISTS "RelevanceScore" integer NULL;
ALTER TABLE "AutomationWorkflows" ADD COLUMN IF NOT EXISTS "MinRelevanceScore" integer NOT NULL DEFAULT 7;
