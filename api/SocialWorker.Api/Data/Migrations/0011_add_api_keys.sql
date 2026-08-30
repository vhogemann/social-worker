CREATE TABLE IF NOT EXISTS "ApiKeys" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Name" character varying(150) NOT NULL,
    "KeyPrefix" character varying(32) NOT NULL,
    "KeyHash" character varying(128) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "LastUsedAt" timestamp with time zone NULL,
    "ExpiresAt" timestamp with time zone NULL,
    "IsRevoked" boolean NOT NULL DEFAULT FALSE,
    "Scopes" character varying(500) NOT NULL DEFAULT '*',
    CONSTRAINT "PK_ApiKeys" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ApiKeys_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ApiKeys_KeyHash" ON "ApiKeys" ("KeyHash");
CREATE INDEX IF NOT EXISTS "IX_ApiKeys_UserId" ON "ApiKeys" ("UserId");
