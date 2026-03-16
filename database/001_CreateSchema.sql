-- TimeClock Database Schema
-- Run this script against your SQL Server database to create the initial schema.

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'TimeClock')
BEGIN
    CREATE DATABASE TimeClock;
END
GO

USE TimeClock;
GO

-- Employees
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Employees')
BEGIN
    CREATE TABLE Employees (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(255) NOT NULL,
        EmployeeNumber NVARCHAR(50) NOT NULL UNIQUE,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
    CREATE INDEX IX_Employees_EmployeeNumber ON Employees(EmployeeNumber);
    CREATE INDEX IX_Employees_IsActive ON Employees(IsActive);
END
GO

-- NFC Cards
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NfcCards')
BEGIN
    CREATE TABLE NfcCards (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        CardUid NVARCHAR(100) NOT NULL UNIQUE,
        EmployeeId UNIQUEIDENTIFIER NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        RegisteredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        LastUsedAt DATETIME2 NULL,
        CONSTRAINT FK_NfcCards_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_NfcCards_CardUid ON NfcCards(CardUid) WHERE IsActive = 1;
    CREATE INDEX IX_NfcCards_EmployeeId ON NfcCards(EmployeeId);
END
GO

-- Time Punches
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TimePunches')
BEGIN
    CREATE TABLE TimePunches (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        EmployeeId UNIQUEIDENTIFIER NOT NULL,
        PunchType INT NOT NULL, -- 0 = In, 1 = Out
        PunchedAt DATETIME2 NOT NULL,
        Source NVARCHAR(50) NOT NULL DEFAULT 'Desktop',
        DeviceId NVARCHAR(100) NULL,
        RawCardUid NVARCHAR(100) NULL,
        CONSTRAINT FK_TimePunches_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_TimePunches_EmployeeId_PunchedAt ON TimePunches(EmployeeId, PunchedAt);
    CREATE INDEX IX_TimePunches_PunchedAt ON TimePunches(PunchedAt);
END
GO

-- Wage Rates
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WageRates')
BEGIN
    CREATE TABLE WageRates (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        EmployeeId UNIQUEIDENTIFIER NOT NULL,
        HourlyRate DECIMAL(18, 4) NOT NULL DEFAULT 0,
        SalaryAmount DECIMAL(18, 4) NULL,
        RateType INT NOT NULL, -- 0 = Hourly, 1 = Salary
        EffectiveFrom DATETIME2 NOT NULL,
        EffectiveTo DATETIME2 NULL,
        CONSTRAINT FK_WageRates_Employees FOREIGN KEY (EmployeeId) REFERENCES Employees(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_WageRates_EmployeeId ON WageRates(EmployeeId);
END
GO
