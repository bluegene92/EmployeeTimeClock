-- TimeClock Database Schema (PostgreSQL)
-- Run this script against your PostgreSQL database to create the initial schema.
-- Uses 4-digit PIN for employee clock-in.

-- Employees
CREATE TABLE IF NOT EXISTS "Employees" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "FirstName" VARCHAR(100) NOT NULL,
    "LastName" VARCHAR(100) NOT NULL,
    "Email" VARCHAR(255) NOT NULL,
    "EmployeeNumber" VARCHAR(50) NOT NULL UNIQUE,
    "IsActive" BOOLEAN NOT NULL DEFAULT true,
    "PinHash" VARCHAR(255) NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_Employees_EmployeeNumber" ON "Employees"("EmployeeNumber");
CREATE INDEX IF NOT EXISTS "IX_Employees_IsActive" ON "Employees"("IsActive");

-- Time Punches
CREATE TABLE IF NOT EXISTS "TimePunches" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "EmployeeId" UUID NOT NULL REFERENCES "Employees"("Id") ON DELETE CASCADE,
    "PunchType" INT NOT NULL,
    "PunchedAt" TIMESTAMPTZ NOT NULL,
    "Source" VARCHAR(50) NOT NULL DEFAULT 'Desktop',
    "DeviceId" VARCHAR(100) NULL
);
CREATE INDEX IF NOT EXISTS "IX_TimePunches_EmployeeId_PunchedAt" ON "TimePunches"("EmployeeId", "PunchedAt");
CREATE INDEX IF NOT EXISTS "IX_TimePunches_PunchedAt" ON "TimePunches"("PunchedAt");

-- Wage Rates
CREATE TABLE IF NOT EXISTS "WageRates" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "EmployeeId" UUID NOT NULL REFERENCES "Employees"("Id") ON DELETE CASCADE,
    "HourlyRate" DECIMAL(18, 4) NOT NULL DEFAULT 0,
    "SalaryAmount" DECIMAL(18, 4) NULL,
    "RateType" INT NOT NULL,
    "EffectiveFrom" TIMESTAMPTZ NOT NULL,
    "EffectiveTo" TIMESTAMPTZ NULL
);
CREATE INDEX IF NOT EXISTS "IX_WageRates_EmployeeId" ON "WageRates"("EmployeeId");
