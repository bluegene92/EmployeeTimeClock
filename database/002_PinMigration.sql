-- Migration: Replace NFC with 4-digit PIN
-- Run this after 001_CreateSchema.sql if you have existing data.
-- For fresh installs, use the updated 001_CreateSchema.sql.

USE TimeClock;
GO

-- Add PinHash column to Employees (nullable - employees may not have PIN set yet)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Employees') AND name = 'PinHash')
BEGIN
    ALTER TABLE Employees ADD PinHash NVARCHAR(255) NULL;
END
GO

-- Drop NFC tables if they exist (run only if migrating from NFC)
-- IF EXISTS (SELECT * FROM sys.tables WHERE name = 'NfcCards')
-- BEGIN
--     DROP TABLE NfcCards;
-- END
-- GO
