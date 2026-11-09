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
