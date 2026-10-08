CREATE TABLE IF NOT EXISTS "OAuthStates" (
    "Nonce" character varying(128) NOT NULL,
    "UserId" uuid NOT NULL,
    "Provider" character varying(50) NOT NULL,
    "PayloadJson" text NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_OAuthStates" PRIMARY KEY ("Nonce"),
    CONSTRAINT "FK_OAuthStates_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_OAuthStates_UserId_Provider" ON "OAuthStates" ("UserId", "Provider");
CREATE INDEX IF NOT EXISTS "IX_OAuthStates_ExpiresAt" ON "OAuthStates" ("ExpiresAt");
