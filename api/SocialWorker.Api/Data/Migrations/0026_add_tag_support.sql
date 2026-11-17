ALTER TABLE "Accounts" ADD COLUMN IF NOT EXISTS "Tags" text NOT NULL DEFAULT '[]';
ALTER TABLE "AutomationWorkflows" ADD COLUMN IF NOT EXISTS "Tags" text NOT NULL DEFAULT '[]';
ALTER TABLE "Schedules" ADD COLUMN IF NOT EXISTS "Tags" text NOT NULL DEFAULT '[]';
ALTER TABLE "Drafts" ADD COLUMN IF NOT EXISTS "Tags" text NOT NULL DEFAULT '[]';

UPDATE "Accounts" SET "Tags" = '["default","personal"]' WHERE "Handle" IN (
    'victor.hogemann.com',
    'vhogemann',
    '@vhogemann',
    'urn:li:person:hlBZtop6wj',
    'hlBZtop6wj'
);

UPDATE "Accounts" SET "Tags" = '["bot","science"]' WHERE "Handle" LIKE '%-bot.bsky.social' AND "Handle" NOT LIKE 'ltt-%' AND "Handle" NOT LIKE 'sloppyjoe%';
UPDATE "Accounts" SET "Tags" = '["bot","ltt"]' WHERE "Handle" LIKE 'ltt-%';
UPDATE "Accounts" SET "Tags" = '["bot"]' WHERE "Handle" LIKE 'sloppyjoe%';
