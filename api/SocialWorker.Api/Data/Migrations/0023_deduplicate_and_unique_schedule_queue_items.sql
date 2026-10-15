-- 1. Deduplicate active ScheduleQueueItems keeping the earliest created item per (ScheduleId, PlatformThreadId)
DELETE FROM "ScheduleQueueItems"
WHERE "Id" IN (
    SELECT "Id"
    FROM (
        SELECT "Id",
               ROW_NUMBER() OVER (
                   PARTITION BY "ScheduleId", "PlatformThreadId" 
                   ORDER BY "CreatedAt" ASC, "Id" ASC
               ) AS rn
        FROM "ScheduleQueueItems"
        WHERE "Status" IN ('Queued', 'Dispatched')
    ) sub
    WHERE sub.rn > 1
);

-- 2. Add partial unique index on (ScheduleId, PlatformThreadId) for active statuses
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ScheduleQueueItems_Active_Schedule_Thread" 
ON "ScheduleQueueItems" ("ScheduleId", "PlatformThreadId") 
WHERE "Status" IN ('Queued', 'Dispatched');
