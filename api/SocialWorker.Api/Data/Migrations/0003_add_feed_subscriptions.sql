CREATE TABLE IF NOT EXISTS "FeedSubscriptions" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "FeedUrl" character varying(500) NOT NULL,
    "WebsiteUrl" character varying(500) NULL,
    "InstructionPrompt" text NOT NULL,
    "AutoPublish" boolean NOT NULL DEFAULT FALSE,
    "LastPolledAt" timestamp with time zone NULL,
    "IncludeFilters" character varying(500) NULL,
    "ExcludeFilters" character varying(500) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_FeedSubscriptions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_FeedSubscriptions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_FeedSubscriptions_UserId" ON "FeedSubscriptions" ("UserId");
