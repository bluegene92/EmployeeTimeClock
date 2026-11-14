-- TimeClock schema (PostgreSQL)
-- Punch times are local wall-clock times. They are not stored in UTC.
-- A shift never crosses midnight, so each punch belongs to one calendar day.

CREATE TABLE IF NOT EXISTS "Locations" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name" VARCHAR(200) NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT true,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Locations_Name" ON "Locations" (lower("Name"));

CREATE TABLE IF NOT EXISTS "Employees" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "FirstName" VARCHAR(100) NOT NULL,
    "LastName" VARCHAR(100) NOT NULL,
    "Email" VARCHAR(255) NOT NULL,
    "EmployeeNumber" VARCHAR(50) NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT true,
    "IsDeleted" BOOLEAN NOT NULL DEFAULT false,
    "DeletedAt" TIMESTAMPTZ NULL,
    "Pin" VARCHAR(4) NULL,
    "LocationId" UUID NULL REFERENCES "Locations"("Id") ON DELETE RESTRICT,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "CK_Employees_Pin" CHECK ("Pin" IS NULL OR "Pin" ~ '^[1-9][0-9]{3}$')
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Employees_EmployeeNumber_Open"
    ON "Employees" ("EmployeeNumber") WHERE "IsDeleted" = false;
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Employees_Pin_Open"
    ON "Employees" ("Pin") WHERE "IsDeleted" = false AND "Pin" IS NOT NULL;
-- NFC card UID read by the clock, as uppercase hex with no separators.
ALTER TABLE "Employees" ADD COLUMN IF NOT EXISTS "CardUid" VARCHAR(32) NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Employees_CardUid_Open"
    ON "Employees" ("CardUid") WHERE "IsDeleted" = false AND "CardUid" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_Employees_IsActive" ON "Employees" ("IsActive");
CREATE INDEX IF NOT EXISTS "IX_Employees_LocationId" ON "Employees" ("LocationId");

CREATE TABLE IF NOT EXISTS "TimePunches" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "EmployeeId" UUID NOT NULL REFERENCES "Employees"("Id") ON DELETE RESTRICT,
    "LocationId" UUID NULL REFERENCES "Locations"("Id") ON DELETE RESTRICT,
    "PunchType" INT NOT NULL,
    "PunchedAt" TIMESTAMP NOT NULL,
    "Source" VARCHAR(50) NOT NULL DEFAULT 'Web',
    "DeviceId" VARCHAR(100) NULL,
    CONSTRAINT "CK_TimePunches_PunchType" CHECK ("PunchType" IN (0, 1))
);
CREATE INDEX IF NOT EXISTS "IX_TimePunches_EmployeeId_PunchedAt" ON "TimePunches" ("EmployeeId", "PunchedAt");
CREATE INDEX IF NOT EXISTS "IX_TimePunches_PunchedAt" ON "TimePunches" ("PunchedAt");
CREATE INDEX IF NOT EXISTS "IX_TimePunches_LocationId" ON "TimePunches" ("LocationId");

CREATE TABLE IF NOT EXISTS "WageRates" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "EmployeeId" UUID NOT NULL REFERENCES "Employees"("Id") ON DELETE RESTRICT,
    "HourlyRate" DECIMAL(18, 4) NULL,
    "SalaryAmount" DECIMAL(18, 4) NULL,
    "RateType" INT NOT NULL,
    "EffectiveFrom" TIMESTAMPTZ NOT NULL,
    "EffectiveTo" TIMESTAMPTZ NULL,
    CONSTRAINT "CK_WageRates_RateType" CHECK ("RateType" IN (0, 1)),
    CONSTRAINT "CK_WageRates_Amount" CHECK (
        ("RateType" = 0 AND "HourlyRate" IS NOT NULL AND "HourlyRate" >= 0 AND "SalaryAmount" IS NULL)
        OR
        ("RateType" = 1 AND "SalaryAmount" IS NOT NULL AND "SalaryAmount" >= 0 AND "HourlyRate" IS NULL)
    )
);
CREATE UNIQUE INDEX IF NOT EXISTS "UX_WageRates_OneOpen"
    ON "WageRates" ("EmployeeId") WHERE "EffectiveTo" IS NULL;
CREATE INDEX IF NOT EXISTS "IX_WageRates_EmployeeId" ON "WageRates" ("EmployeeId", "EffectiveFrom" DESC);

-- A row exists only when an admin set a rate for that calendar day.
-- No row means "use the employee's current hourly rate."
CREATE TABLE IF NOT EXISTS "DayRateOverrides" (
    "EmployeeId" UUID NOT NULL REFERENCES "Employees"("Id") ON DELETE RESTRICT,
    "WorkDate" DATE NOT NULL,
    "HourlyRate" DECIMAL(18, 4) NOT NULL,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("EmployeeId", "WorkDate"),
    CONSTRAINT "CK_DayRateOverrides_Rate" CHECK ("HourlyRate" >= 0)
);

CREATE TABLE IF NOT EXISTS "PayPeriodVerifications" (
    "EmployeeId" UUID NOT NULL REFERENCES "Employees"("Id") ON DELETE RESTRICT,
    "PeriodStart" DATE NOT NULL,
    "VerifiedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("EmployeeId", "PeriodStart")
);

-- Written for all 14 days when a period is verified, including days with no punches.
-- HourlyRate is null when the employee was salaried at verification.
CREATE TABLE IF NOT EXISTS "FrozenDayRates" (
    "EmployeeId" UUID NOT NULL REFERENCES "Employees"("Id") ON DELETE RESTRICT,
    "WorkDate" DATE NOT NULL,
    "PeriodStart" DATE NOT NULL,
    "HourlyRate" DECIMAL(18, 4) NULL,
    PRIMARY KEY ("EmployeeId", "WorkDate"),
    CONSTRAINT "CK_FrozenDayRates_Rate" CHECK ("HourlyRate" IS NULL OR "HourlyRate" >= 0)
);
CREATE INDEX IF NOT EXISTS "IX_FrozenDayRates_Period"
    ON "FrozenDayRates" ("EmployeeId", "PeriodStart");

CREATE TABLE IF NOT EXISTS "PayPeriodSettings" (
    "Id" INT PRIMARY KEY,
    "AnchorDate" DATE NOT NULL,
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "CK_PayPeriodSettings_Singleton" CHECK ("Id" = 1)
);

INSERT INTO "PayPeriodSettings" ("Id", "AnchorDate")
VALUES (1, DATE '2020-01-01')
ON CONFLICT ("Id") DO NOTHING;

-- Scans that did not become a punch, for admins to review.
-- No foreign keys: a failure can name an employee or location that does not exist.
CREATE TABLE IF NOT EXISTS "ClockFailures" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Reason" VARCHAR(50) NOT NULL,
    "Message" VARCHAR(500) NOT NULL,
    "CardUid" VARCHAR(32) NULL,
    "EmployeeId" UUID NULL,
    "PunchType" INT NULL,
    "OccurredAt" TIMESTAMP NULL,
    "LocationId" UUID NULL,
    "Source" VARCHAR(50) NULL,
    "DeviceId" VARCHAR(100) NULL,
    "ReceivedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "ResolvedAt" TIMESTAMPTZ NULL
);
CREATE INDEX IF NOT EXISTS "IX_ClockFailures_ReceivedAt" ON "ClockFailures" ("ReceivedAt" DESC);
CREATE INDEX IF NOT EXISTS "IX_ClockFailures_Open" ON "ClockFailures" ("ReceivedAt" DESC) WHERE "ResolvedAt" IS NULL;

-- One row per physical clock, updated by its check-in every few seconds.
CREATE TABLE IF NOT EXISTS "ClockDevices" (
    "DeviceId" VARCHAR(100) PRIMARY KEY,
    "LocationId" UUID NULL,
    "EmployeeListVersion" VARCHAR(64) NULL,
    "PendingPunches" INT NOT NULL DEFAULT 0,
    "PendingFailures" INT NOT NULL DEFAULT 0,
    "FirmwareVersion" VARCHAR(50) NULL,
    "FirstSeenAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "LastSeenAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE "ClockDevices" ADD COLUMN IF NOT EXISTS "BootedAt" TIMESTAMPTZ NULL;
ALTER TABLE "ClockDevices" ADD COLUMN IF NOT EXISTS "WifiRssi" INT NULL;

-- A gap between check-ins longer than the online window, or a restart.
-- StartedAt is the last check-in before the gap, EndedAt the first one after it.
CREATE TABLE IF NOT EXISTS "ClockDeviceOutages" (
    "Id" BIGSERIAL PRIMARY KEY,
    "DeviceId" VARCHAR(100) NOT NULL,
    "StartedAt" TIMESTAMPTZ NOT NULL,
    "EndedAt" TIMESTAMPTZ NOT NULL,
    "Restarted" BOOLEAN NOT NULL DEFAULT false
);

CREATE INDEX IF NOT EXISTS "IX_ClockDeviceOutages_Device" ON "ClockDeviceOutages" ("DeviceId", "EndedAt" DESC);

-- Bumped by an admin "push" so every clock downloads the employee list again,
-- even when the list itself has not changed.
CREATE TABLE IF NOT EXISTS "EmployeeListSync" (
    "Id" INT PRIMARY KEY,
    "Generation" BIGINT NOT NULL DEFAULT 0,
    "PushedAt" TIMESTAMPTZ NULL,
    CONSTRAINT "CK_EmployeeListSync_Singleton" CHECK ("Id" = 1)
);

INSERT INTO "EmployeeListSync" ("Id", "Generation")
VALUES (1, 0)
ON CONFLICT ("Id") DO NOTHING;
