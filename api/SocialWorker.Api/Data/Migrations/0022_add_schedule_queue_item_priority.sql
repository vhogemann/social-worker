ALTER TABLE "ScheduleQueueItems" ADD COLUMN IF NOT EXISTS "PriorityScore" integer NULL;
CREATE INDEX IF NOT EXISTS "IX_ScheduleQueueItems_ScheduleId_PriorityScore" ON "ScheduleQueueItems" ("ScheduleId", "PriorityScore");

-- Backfill variant drafts from their canonical drafts
UPDATE "Drafts" v
SET "RelevanceScore" = cd."RelevanceScore",
    "DiscardReason" = cd."DiscardReason"
FROM "Drafts" cd
WHERE v."CanonicalDraftId" = cd."Id"
  AND v."RelevanceScore" IS NULL
  AND cd."RelevanceScore" IS NOT NULL;

-- Backfill PriorityScore on ScheduleQueueItems from draft or canonical draft, fallback to 7
UPDATE "ScheduleQueueItems" q
SET "PriorityScore" = COALESCE(d."RelevanceScore", cd."RelevanceScore", 7)
FROM "Drafts" d
LEFT JOIN "Drafts" cd ON cd."Id" = d."CanonicalDraftId"
WHERE q."DraftId" = d."Id"
  AND q."PriorityScore" IS NULL;
