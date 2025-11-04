-- FMS Monitor - SQL Server Database Schema

-- Tạo database
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'FmsMonitor')
BEGIN
    CREATE DATABASE FmsMonitor;
END
GO

USE FmsMonitor;
GO

-- Bảng MachineAmperage: Lưu dữ liệu Ampe của máy
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MachineAmperage')
BEGIN
    CREATE TABLE MachineAmperage (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        MachineCode NVARCHAR(50) NOT NULL,
        MachineName NVARCHAR(200) NOT NULL,
        AmperageValue DECIMAL(10, 2) NOT NULL,
        RecordedAt DATETIME2 NOT NULL,
        Status NVARCHAR(50) NULL,
        Notes NVARCHAR(500) NULL,

        INDEX IX_MachineAmperage_MachineCode (MachineCode),
        INDEX IX_MachineAmperage_RecordedAt (RecordedAt),
        INDEX IX_MachineAmperage_MachineCode_RecordedAt (MachineCode, RecordedAt DESC)
    );

    PRINT 'Đã tạo bảng MachineAmperage';
END
GO

-- Bảng MonitoringConfiguration: Cấu hình giám sát cho từng máy
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MonitoringConfiguration')
BEGIN
    CREATE TABLE MonitoringConfiguration (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        MachineCode NVARCHAR(50) NOT NULL UNIQUE,
        MachineName NVARCHAR(200) NOT NULL,
        MinAmperage DECIMAL(10, 2) NOT NULL,
        MaxAmperage DECIMAL(10, 2) NOT NULL,
        FluctuationMinAmperage DECIMAL(10, 2) NOT NULL,
        FluctuationMaxAmperage DECIMAL(10, 2) NOT NULL,
        MonitoringWindowMinutes INT NOT NULL DEFAULT 5,
        AllowedFluctuationCount INT NOT NULL DEFAULT 1,
        DataFetchIntervalSeconds INT NOT NULL DEFAULT 60,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,

        INDEX IX_MonitoringConfiguration_IsActive (IsActive),
        INDEX IX_MonitoringConfiguration_MachineCode (MachineCode)
    );

    PRINT 'Đã tạo bảng MonitoringConfiguration';
END
GO

-- Bảng Alert: Cảnh báo
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Alert')
BEGIN
    CREATE TABLE Alert (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        MachineCode NVARCHAR(50) NOT NULL,
        MachineName NVARCHAR(200) NOT NULL,
        Level INT NOT NULL, -- 0=Info, 1=Warning, 2=Critical, 3=Emergency
        Type INT NOT NULL, -- 1=BelowMinimum, 2=AboveMaximum, 3=AbnormalFluctuation, 4=NoData
        CurrentValue DECIMAL(10, 2) NOT NULL,
        Message NVARCHAR(1000) NOT NULL,
        AiAnalysis NVARCHAR(MAX) NULL,
        DetectedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        IsNotified BIT NOT NULL DEFAULT 0,
        NotifiedAt DATETIME2 NULL,
        IsResolved BIT NOT NULL DEFAULT 0,
        ResolvedAt DATETIME2 NULL,
        ResolvedBy NVARCHAR(200) NULL,

        INDEX IX_Alert_MachineCode (MachineCode),
        INDEX IX_Alert_DetectedAt (DetectedAt DESC),
        INDEX IX_Alert_IsResolved (IsResolved),
        INDEX IX_Alert_Level (Level)
    );

    PRINT 'Đã tạo bảng Alert';
END
GO

-- Bảng EmailRecipient: Người nhận email cảnh báo
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EmailRecipient')
BEGIN
    CREATE TABLE EmailRecipient (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL,
        Email NVARCHAR(200) NOT NULL,
        Role NVARCHAR(100) NOT NULL, -- Nhân viên trực tiếp, Trưởng ca, Quản lý
        MinAlertLevel INT NOT NULL DEFAULT 1, -- Mức cảnh báo tối thiểu để nhận email
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        INDEX IX_EmailRecipient_IsActive (IsActive),
        INDEX IX_EmailRecipient_MinAlertLevel (MinAlertLevel)
    );

    PRINT 'Đã tạo bảng EmailRecipient';
END
GO

-- Thêm dữ liệu mẫu cho MonitoringConfiguration
IF NOT EXISTS (SELECT * FROM MonitoringConfiguration WHERE MachineCode = 'MC01')
BEGIN
    INSERT INTO MonitoringConfiguration
    (MachineCode, MachineName, MinAmperage, MaxAmperage, FluctuationMinAmperage, FluctuationMaxAmperage, MonitoringWindowMinutes, AllowedFluctuationCount, DataFetchIntervalSeconds, IsActive, CreatedAt)
    VALUES
    ('MC01', N'Máy CNC 01', 100, 130, 115, 125, 5, 1, 60, 1, GETUTCDATE()),
    ('MC02', N'Máy CNC 02', 90, 120, 100, 110, 5, 1, 60, 1, GETUTCDATE()),
    ('MC03', N'Máy Tiện 01', 80, 110, 85, 105, 5, 1, 60, 1, GETUTCDATE());

    PRINT 'Đã thêm dữ liệu mẫu MonitoringConfiguration';
END
GO

-- Thêm dữ liệu mẫu cho EmailRecipient
IF NOT EXISTS (SELECT * FROM EmailRecipient WHERE Email = 'tranvana@gmail.com')
BEGIN
    INSERT INTO EmailRecipient (Name, Email, Role, MinAlertLevel, IsActive, CreatedAt)
    VALUES
    ('Trần Văn A', 'tranvana@gmail.com', N'Nhân viên trực tiếp', 1, 1, GETUTCDATE()),
    ('Trần Văn B', 'tranvanb@gmail.com', N'Trưởng ca', 2, 1, GETUTCDATE()),
    ('Trần Văn C', 'tranvanc@gmail.com', N'Quản lý', 2, 1, GETUTCDATE());

    PRINT 'Đã thêm dữ liệu mẫu EmailRecipient';
END
GO

-- Thêm dữ liệu test cho MachineAmperage
IF NOT EXISTS (SELECT TOP 1 * FROM MachineAmperage)
BEGIN
    DECLARE @i INT = 0;
    DECLARE @machineCode NVARCHAR(50);
    DECLARE @machineName NVARCHAR(200);
    DECLARE @baseValue DECIMAL(10,2);

    WHILE @i < 100
    BEGIN
        -- Random machine
        SET @machineCode = CASE (@i % 3)
            WHEN 0 THEN 'MC01'
            WHEN 1 THEN 'MC02'
            ELSE 'MC03'
        END;

        SET @machineName = CASE (@i % 3)
            WHEN 0 THEN N'Máy CNC 01'
            WHEN 1 THEN N'Máy CNC 02'
            ELSE N'Máy Tiện 01'
        END;

        SET @baseValue = CASE (@i % 3)
            WHEN 0 THEN 115 + (RAND() * 10 - 5)  -- MC01: 110-120
            WHEN 1 THEN 105 + (RAND() * 10 - 5)  -- MC02: 100-110
            ELSE 95 + (RAND() * 10 - 5)          -- MC03: 90-100
        END;

        INSERT INTO MachineAmperage (MachineCode, MachineName, AmperageValue, RecordedAt, Status)
        VALUES (@machineCode, @machineName, @baseValue, DATEADD(MINUTE, -@i, GETUTCDATE()), 'Normal');

        SET @i = @i + 1;
    END

    PRINT 'Đã thêm dữ liệu test MachineAmperage';
END
GO

PRINT 'Schema setup hoàn tất!';
