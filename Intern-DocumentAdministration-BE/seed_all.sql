SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE [master];
GO

IF DB_ID(N'DocumentManagementDb') IS NULL
BEGIN
    PRINT N'Creating database DocumentManagementDb...';
    EXEC(N'CREATE DATABASE [DocumentManagementDb]');
END
ELSE
BEGIN
    PRINT N'Database DocumentManagementDb already exists.';
END
GO

USE [DocumentManagementDb];
GO


IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'auth')
    EXEC(N'CREATE SCHEMA auth');
GO
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'partner')
    EXEC(N'CREATE SCHEMA partner');
GO
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'document')
    EXEC(N'CREATE SCHEMA document');
GO
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'files')
    EXEC(N'CREATE SCHEMA files');
GO
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'notification')
    EXEC(N'CREATE SCHEMA notification');
GO
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'emailworker')
    EXEC(N'CREATE SCHEMA emailworker');
GO

IF OBJECT_ID(N'auth.Departments', N'U') IS NULL
BEGIN
    CREATE TABLE auth.Departments
    (
        Id        UNIQUEIDENTIFIER NOT NULL,
        Name      NVARCHAR(MAX)    NOT NULL,
        Code      NVARCHAR(450)    NOT NULL,
        IsActive  BIT              NOT NULL,
        CreatedAt DATETIME2        NOT NULL CONSTRAINT DF_Departments_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2        NULL,
        CONSTRAINT PK_Departments PRIMARY KEY (Id)
    );
END
GO

IF OBJECT_ID(N'auth.Roles', N'U') IS NULL
BEGIN
    CREATE TABLE auth.Roles
    (
        Id          UNIQUEIDENTIFIER NOT NULL,
        Name        NVARCHAR(450)    NOT NULL,
        Description NVARCHAR(MAX)    NULL,
        CONSTRAINT PK_Roles PRIMARY KEY (Id)
    );
END
GO

IF OBJECT_ID(N'auth.Users', N'U') IS NULL
BEGIN
    CREATE TABLE auth.Users
    (
        Id           UNIQUEIDENTIFIER NOT NULL,
        Username     NVARCHAR(450)    NOT NULL,
        PasswordHash NVARCHAR(MAX)    NOT NULL,
        FullName     NVARCHAR(MAX)    NOT NULL,
        Email        NVARCHAR(MAX)    NULL,
        Phone        NVARCHAR(MAX)    NULL,
        DepartmentId UNIQUEIDENTIFIER NULL,
        IsActive     BIT              NOT NULL,
        LastLoginAt  DATETIME2        NULL,
        CreatedAt    DATETIME2        NOT NULL,
        UpdatedAt    DATETIME2        NULL,
        CONSTRAINT PK_Users PRIMARY KEY (Id),
        CONSTRAINT FK_Users_Departments_DepartmentId
            FOREIGN KEY (DepartmentId)
            REFERENCES auth.Departments(Id)
            ON DELETE SET NULL
    );
END
GO

IF OBJECT_ID(N'auth.RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE auth.RefreshTokens
    (
        Id        UNIQUEIDENTIFIER NOT NULL,
        UserId    UNIQUEIDENTIFIER NOT NULL,
        Token     NVARCHAR(450)    NOT NULL,
        ExpiresAt DATETIME2        NOT NULL,
        RevokedAt DATETIME2        NULL,
        CreatedAt DATETIME2        NOT NULL,
        CONSTRAINT PK_RefreshTokens PRIMARY KEY (Id),
        CONSTRAINT FK_RefreshTokens_Users_UserId
            FOREIGN KEY (UserId)
            REFERENCES auth.Users(Id)
            ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'auth.UserRoles', N'U') IS NULL
BEGIN
    CREATE TABLE auth.UserRoles
    (
        UserId UNIQUEIDENTIFIER NOT NULL,
        RoleId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_UserRoles PRIMARY KEY (UserId, RoleId),
        CONSTRAINT FK_UserRoles_Users_UserId
            FOREIGN KEY (UserId)
            REFERENCES auth.Users(Id)
            ON DELETE CASCADE,
        CONSTRAINT FK_UserRoles_Roles_RoleId
            FOREIGN KEY (RoleId)
            REFERENCES auth.Roles(Id)
            ON DELETE CASCADE
    );
END
GO

-- Upgrade an older auth schema without deleting existing rows.
IF COL_LENGTH('auth.Departments', 'CreatedAt') IS NULL
    ALTER TABLE auth.Departments ADD CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Departments_CreatedAt_Upgrade DEFAULT SYSUTCDATETIME() WITH VALUES;
GO
IF COL_LENGTH('auth.Departments', 'UpdatedAt') IS NULL
    ALTER TABLE auth.Departments ADD UpdatedAt DATETIME2 NULL;
GO
IF COL_LENGTH('auth.Users', 'UpdatedAt') IS NULL
    ALTER TABLE auth.Users ADD UpdatedAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'auth.Departments') AND name = N'IX_Departments_Code')
    CREATE UNIQUE INDEX IX_Departments_Code ON auth.Departments(Code);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'auth.Roles') AND name = N'IX_Roles_Name')
    CREATE UNIQUE INDEX IX_Roles_Name ON auth.Roles(Name);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'auth.Users') AND name = N'IX_Users_Username')
    CREATE UNIQUE INDEX IX_Users_Username ON auth.Users(Username);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'auth.Users') AND name = N'IX_Users_DepartmentId')
    CREATE INDEX IX_Users_DepartmentId ON auth.Users(DepartmentId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'auth.RefreshTokens') AND name = N'IX_RefreshTokens_Token')
    CREATE UNIQUE INDEX IX_RefreshTokens_Token ON auth.RefreshTokens(Token);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'auth.RefreshTokens') AND name = N'IX_RefreshTokens_UserId')
    CREATE INDEX IX_RefreshTokens_UserId ON auth.RefreshTokens(UserId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'auth.UserRoles') AND name = N'IX_UserRoles_RoleId')
    CREATE INDEX IX_UserRoles_RoleId ON auth.UserRoles(RoleId);
GO

IF OBJECT_ID(N'partner.Partners', N'U') IS NULL
BEGIN
    CREATE TABLE partner.Partners
    (
        Id              UNIQUEIDENTIFIER NOT NULL,
        FullName        NVARCHAR(MAX)    NOT NULL,
        ShortName       NVARCHAR(450)    NOT NULL,
        EntityType      NVARCHAR(450)    NOT NULL,
        Email           NVARCHAR(MAX)    NULL,
        Phone           NVARCHAR(MAX)    NULL,
        Address         NVARCHAR(MAX)    NULL,
        TaxCode         NVARCHAR(450)    NULL,
        IsActive        BIT              NOT NULL,
        IsDeleted       BIT              NOT NULL,
        DeletedAt       DATETIME2        NULL,
        CreatedAt       DATETIME2        NOT NULL,
        UpdatedAt       DATETIME2        NULL,
        CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_Partners PRIMARY KEY (Id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'partner.Partners') AND name = N'IX_Partners_EntityType')
    CREATE INDEX IX_Partners_EntityType ON partner.Partners(EntityType);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'partner.Partners') AND name = N'IX_Partners_IsDeleted_IsActive')
    CREATE INDEX IX_Partners_IsDeleted_IsActive ON partner.Partners(IsDeleted, IsActive);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'partner.Partners') AND name = N'IX_Partners_ShortName')
    CREATE UNIQUE INDEX IX_Partners_ShortName ON partner.Partners(ShortName);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'partner.Partners') AND name = N'IX_Partners_TaxCode')
    CREATE UNIQUE INDEX IX_Partners_TaxCode ON partner.Partners(TaxCode) WHERE TaxCode IS NOT NULL;
GO

IF OBJECT_ID(N'document.Documents', N'U') IS NULL
BEGIN
    CREATE TABLE document.Documents
    (
        Id                 UNIQUEIDENTIFIER NOT NULL,
        DocumentNumber     NVARCHAR(450)    NOT NULL,
        DocType            NVARCHAR(450)    NOT NULL,
        Status             NVARCHAR(450)    NOT NULL,
        Title              NVARCHAR(MAX)    NOT NULL,
        Summary            NVARCHAR(MAX)    NULL,
        PartnerId          UNIQUEIDENTIFIER NULL,
        SenderDepartmentId UNIQUEIDENTIFIER NULL,
        CreatedByUserId    UNIQUEIDENTIFIER NOT NULL,
        ReceivedAt         DATETIME2        NULL,
        DistributedAt      DATETIME2        NULL,
        CreatedAt          DATETIME2        NOT NULL,
        UpdatedAt          DATETIME2        NULL,
        IsDeleted          BIT              NOT NULL CONSTRAINT DF_Documents_IsDeleted DEFAULT 0,
        DeletedAt          DATETIME2        NULL,
        CONSTRAINT PK_Documents PRIMARY KEY (Id)
    );
END
GO

IF COL_LENGTH('document.Documents', 'IsDeleted') IS NULL
    ALTER TABLE document.Documents ADD IsDeleted BIT NOT NULL
        CONSTRAINT DF_Documents_IsDeleted_Upgrade DEFAULT 0 WITH VALUES;
GO
IF COL_LENGTH('document.Documents', 'DeletedAt') IS NULL
    ALTER TABLE document.Documents ADD DeletedAt DATETIME2 NULL;
GO

IF OBJECT_ID(N'document.DocumentNumberCounters', N'U') IS NULL
BEGIN
    CREATE TABLE document.DocumentNumberCounters
    (
        DocType      NVARCHAR(450) NOT NULL,
        [Year]       INT           NOT NULL,
        CurrentValue INT           NOT NULL,
        CONSTRAINT PK_DocumentNumberCounters PRIMARY KEY (DocType, [Year])
    );
END
GO

IF OBJECT_ID(N'document.DocumentAttachments', N'U') IS NULL
BEGIN
    CREATE TABLE document.DocumentAttachments
    (
        Id             UNIQUEIDENTIFIER NOT NULL,
        DocumentId     UNIQUEIDENTIFIER NOT NULL,
        FileId         UNIQUEIDENTIFIER NOT NULL,
        AttachmentType NVARCHAR(MAX)    NULL,
        CreatedAt      DATETIME2        NOT NULL,
        CONSTRAINT PK_DocumentAttachments PRIMARY KEY (Id),
        CONSTRAINT FK_DocumentAttachments_Documents_DocumentId
            FOREIGN KEY (DocumentId)
            REFERENCES document.Documents(Id)
            ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'document.DocumentDepartmentAccess', N'U') IS NULL
BEGIN
    CREATE TABLE document.DocumentDepartmentAccess
    (
        DocumentId      UNIQUEIDENTIFIER NOT NULL,
        DepartmentId    UNIQUEIDENTIFIER NOT NULL,
        AssignedAt      DATETIME2        NOT NULL,
        AssignedByUserId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_DocumentDepartmentAccess PRIMARY KEY (DocumentId, DepartmentId),
        CONSTRAINT FK_DocumentDepartmentAccess_Documents_DocumentId
            FOREIGN KEY (DocumentId)
            REFERENCES document.Documents(Id)
            ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'document.DocumentStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE document.DocumentStatusHistory
    (
        Id              UNIQUEIDENTIFIER NOT NULL,
        DocumentId      UNIQUEIDENTIFIER NOT NULL,
        OldStatus       NVARCHAR(MAX)    NULL,
        NewStatus       NVARCHAR(MAX)    NOT NULL,
        ChangedByUserId UNIQUEIDENTIFIER NOT NULL,
        ChangedAt       DATETIME2        NOT NULL,
        Note            NVARCHAR(MAX)    NULL,
        CONSTRAINT PK_DocumentStatusHistory PRIMARY KEY (Id),
        CONSTRAINT FK_DocumentStatusHistory_Documents_DocumentId
            FOREIGN KEY (DocumentId)
            REFERENCES document.Documents(Id)
            ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'document.Documents') AND name = N'IX_Documents_DocumentNumber')
    CREATE UNIQUE INDEX IX_Documents_DocumentNumber ON document.Documents(DocumentNumber);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'document.Documents') AND name = N'IX_Documents_PartnerId')
    CREATE INDEX IX_Documents_PartnerId ON document.Documents(PartnerId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'document.Documents') AND name = N'IX_Documents_DocType_Status')
    CREATE INDEX IX_Documents_DocType_Status ON document.Documents(DocType, Status);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'document.DocumentAttachments') AND name = N'IX_DocumentAttachments_DocumentId')
    CREATE INDEX IX_DocumentAttachments_DocumentId ON document.DocumentAttachments(DocumentId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'document.DocumentStatusHistory') AND name = N'IX_DocumentStatusHistory_DocumentId')
    CREATE INDEX IX_DocumentStatusHistory_DocumentId ON document.DocumentStatusHistory(DocumentId);
GO

IF OBJECT_ID(N'files.Files', N'U') IS NULL
BEGIN
    CREATE TABLE files.Files
    (
        Id               UNIQUEIDENTIFIER NOT NULL,
        OriginalName     NVARCHAR(MAX)    NOT NULL,
        StoragePath      NVARCHAR(MAX)    NOT NULL,
        ContentType      NVARCHAR(MAX)    NOT NULL,
        SizeBytes        BIGINT           NOT NULL,
        UploadedByUserId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt        DATETIME2        NOT NULL,
        CONSTRAINT PK_Files PRIMARY KEY (Id)
    );
END
GO

IF OBJECT_ID(N'notification.NotificationLogs', N'U') IS NULL
BEGIN
    CREATE TABLE notification.NotificationLogs
    (
        Id                UNIQUEIDENTIFIER NOT NULL,
        RecipientUserId   UNIQUEIDENTIFIER NOT NULL,
        RecipientEmail    NVARCHAR(200)    NOT NULL,
        Subject           NVARCHAR(300)    NOT NULL,
        RelatedDocumentId UNIQUEIDENTIFIER NULL,
        Status            NVARCHAR(20)     NOT NULL,
        SentAt            DATETIME2        NOT NULL,
        ErrorMessage      NVARCHAR(1000)   NULL,
        CONSTRAINT PK_NotificationLogs PRIMARY KEY (Id)
    );
END
GO

IF OBJECT_ID(N'emailworker.EmailImapSettings', N'U') IS NULL
BEGIN
    CREATE TABLE emailworker.EmailImapSettings
    (
        Id                      INT            NOT NULL,
        ImapHost                NVARCHAR(200)  NOT NULL CONSTRAINT DF_EmailImapSettings_ImapHost DEFAULT N'',
        ImapPort                INT            NOT NULL CONSTRAINT DF_EmailImapSettings_ImapPort DEFAULT 993,
        UseSsl                  BIT            NOT NULL CONSTRAINT DF_EmailImapSettings_UseSsl DEFAULT 1,
        EmailAddress            NVARCHAR(200)  NOT NULL CONSTRAINT DF_EmailImapSettings_EmailAddress DEFAULT N'',
        AppPassword             NVARCHAR(500)  NOT NULL CONSTRAINT DF_EmailImapSettings_AppPassword DEFAULT N'',
        WhitelistedDomains      NVARCHAR(1000) NOT NULL CONSTRAINT DF_EmailImapSettings_WhitelistedDomains DEFAULT N'',
        AutoScanIntervalMinutes INT            NOT NULL CONSTRAINT DF_EmailImapSettings_AutoScan DEFAULT 60,
        UpdatedAt               DATETIME2      NOT NULL CONSTRAINT DF_EmailImapSettings_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_EmailImapSettings PRIMARY KEY (Id)
    );
END
GO

IF OBJECT_ID(N'emailworker.EmailScanLogs', N'U') IS NULL
BEGIN
    CREATE TABLE emailworker.EmailScanLogs
    (
        Id                    UNIQUEIDENTIFIER NOT NULL,
        StartedAt             DATETIME2        NOT NULL,
        FinishedAt            DATETIME2        NULL,
        EmailsScanned         INT              NOT NULL CONSTRAINT DF_EmailScanLogs_EmailsScanned DEFAULT 0,
        TotalEmails           INT              NOT NULL CONSTRAINT DF_EmailScanLogs_TotalEmails DEFAULT 0,
        DocumentsCreated      INT              NOT NULL CONSTRAINT DF_EmailScanLogs_DocumentsCreated DEFAULT 0,
        ReadyForIntakeCount   INT              NOT NULL CONSTRAINT DF_EmailScanLogs_ReadyForIntakeCount DEFAULT 0,
        SkippedCount          INT              NOT NULL CONSTRAINT DF_EmailScanLogs_SkippedCount DEFAULT 0,
        FailedCount           INT              NOT NULL CONSTRAINT DF_EmailScanLogs_FailedCount DEFAULT 0,
        CurrentEmailSubject   NVARCHAR(1000)   NULL,
        CurrentSenderEmail    NVARCHAR(320)    NULL,
        Success               BIT              NOT NULL CONSTRAINT DF_EmailScanLogs_Success DEFAULT 0,
        ErrorMessage          NVARCHAR(2000)   NULL,
        TriggerType           NVARCHAR(20)     NOT NULL CONSTRAINT DF_EmailScanLogs_TriggerType DEFAULT N'Scheduled',
        CONSTRAINT PK_EmailScanLogs PRIMARY KEY (Id)
    );
END
GO

IF COL_LENGTH('emailworker.EmailScanLogs', 'TotalEmails') IS NULL
    ALTER TABLE emailworker.EmailScanLogs ADD TotalEmails INT NOT NULL
        CONSTRAINT DF_EmailScanLogs_TotalEmails_Upgrade DEFAULT 0 WITH VALUES;
GO
IF COL_LENGTH('emailworker.EmailScanLogs', 'ReadyForIntakeCount') IS NULL
    ALTER TABLE emailworker.EmailScanLogs ADD ReadyForIntakeCount INT NOT NULL
        CONSTRAINT DF_EmailScanLogs_ReadyForIntakeCount_Upgrade DEFAULT 0 WITH VALUES;
GO
IF COL_LENGTH('emailworker.EmailScanLogs', 'SkippedCount') IS NULL
    ALTER TABLE emailworker.EmailScanLogs ADD SkippedCount INT NOT NULL
        CONSTRAINT DF_EmailScanLogs_SkippedCount_Upgrade DEFAULT 0 WITH VALUES;
GO
IF COL_LENGTH('emailworker.EmailScanLogs', 'FailedCount') IS NULL
    ALTER TABLE emailworker.EmailScanLogs ADD FailedCount INT NOT NULL
        CONSTRAINT DF_EmailScanLogs_FailedCount_Upgrade DEFAULT 0 WITH VALUES;
GO
IF COL_LENGTH('emailworker.EmailScanLogs', 'CurrentEmailSubject') IS NULL
    ALTER TABLE emailworker.EmailScanLogs ADD CurrentEmailSubject NVARCHAR(1000) NULL;
GO
IF COL_LENGTH('emailworker.EmailScanLogs', 'CurrentSenderEmail') IS NULL
    ALTER TABLE emailworker.EmailScanLogs ADD CurrentSenderEmail NVARCHAR(320) NULL;
GO

IF OBJECT_ID(N'emailworker.EmailScanItemLogs', N'U') IS NULL
BEGIN
    CREATE TABLE emailworker.EmailScanItemLogs
    (
        Id                       UNIQUEIDENTIFIER NOT NULL,
        ScanLogId                UNIQUEIDENTIFIER NOT NULL,
        ReceivedAt               DATETIME2        NOT NULL,
        SenderEmail              NVARCHAR(320)    NOT NULL,
        Subject                  NVARCHAR(1000)   NULL,
        AttachmentName           NVARCHAR(500)    NULL,
        FileId                   UNIQUEIDENTIFIER NULL,
        PartnerId                UNIQUEIDENTIFIER NULL,
        ExtractedReferenceNumber NVARCHAR(200)    NULL,
        ExtractedSubject         NVARCHAR(1000)   NULL,
        DocumentId               NVARCHAR(100)    NULL,
        Status                   NVARCHAR(50)     NOT NULL CONSTRAINT DF_EmailScanItemLogs_Status DEFAULT N'Pending',
        ErrorMessage             NVARCHAR(2000)   NULL,
        ProcessedAt              DATETIME2        NULL,
        IntakeConfirmedAt        DATETIME2        NULL,
        CONSTRAINT PK_EmailScanItemLogs PRIMARY KEY (Id),
        CONSTRAINT FK_EmailScanItemLogs_EmailScanLogs
            FOREIGN KEY (ScanLogId)
            REFERENCES emailworker.EmailScanLogs(Id)
            ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'emailworker.EmailScanItemLogs') AND name = N'IX_EmailScanItemLogs_ScanLogId')
    CREATE INDEX IX_EmailScanItemLogs_ScanLogId ON emailworker.EmailScanItemLogs(ScanLogId);
GO

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.__EFMigrationsHistory
    (
        MigrationId    NVARCHAR(150) NOT NULL,
        ProductVersion NVARCHAR(32)  NOT NULL,
        CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY (MigrationId)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260728074907_InitialCreate')
    INSERT INTO dbo.__EFMigrationsHistory(MigrationId, ProductVersion) VALUES (N'20260728074907_InitialCreate', N'10.0.3');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260810130346_ImproveAuthModel')
    INSERT INTO dbo.__EFMigrationsHistory(MigrationId, ProductVersion) VALUES (N'20260810130346_ImproveAuthModel', N'10.0.3');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260730115658_InitialCreate')
    INSERT INTO dbo.__EFMigrationsHistory(MigrationId, ProductVersion) VALUES (N'20260730115658_InitialCreate', N'10.0.3');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260806072636_AddSoftDelete')
    INSERT INTO dbo.__EFMigrationsHistory(MigrationId, ProductVersion) VALUES (N'20260806072636_AddSoftDelete', N'10.0.3');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260810113448_InitialCreate')
    INSERT INTO dbo.__EFMigrationsHistory(MigrationId, ProductVersion) VALUES (N'20260810113448_InitialCreate', N'10.0.3');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260806035121_InitialFileSchema')
    INSERT INTO dbo.__EFMigrationsHistory(MigrationId, ProductVersion) VALUES (N'20260806035121_InitialFileSchema', N'8.0.8');
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20260812103619_InitialNotificationSchema')
    INSERT INTO dbo.__EFMigrationsHistory(MigrationId, ProductVersion) VALUES (N'20260812103619_InitialNotificationSchema', N'9.0.0');
GO

DELETE ur FROM auth.UserRoles ur
JOIN auth.Roles r ON r.Id = ur.RoleId
WHERE r.Name IN (N'Thư Ký GĐ', N'Thư Ký Phòng', N'Nhân Viên');

DELETE FROM auth.Roles WHERE Name IN (N'Thư Ký GĐ', N'Thư Ký Phòng', N'Nhân Viên');

-- 1. Roles
IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE Name = N'Admin')
    INSERT INTO auth.Roles (Id, Name, Description)
    VALUES (NEWID(), N'Admin', N'Toàn quyền hệ thống');

IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE Name = N'SecretaryDirector')
    INSERT INTO auth.Roles (Id, Name, Description)
    VALUES (NEWID(), N'SecretaryDirector', N'Thư ký Giám đốc - duyệt/điều phối toàn bộ CV');

IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE Name = N'Secretary')
    INSERT INTO auth.Roles (Id, Name, Description)
    VALUES (NEWID(), N'Secretary', N'Thư ký phòng ban - tạo CV đi/nội bộ, xem CV phòng mình');

IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE Name = N'Staff')
    INSERT INTO auth.Roles (Id, Name, Description)
    VALUES (NEWID(), N'Staff', N'Nhân viên - chỉ xem CV phòng mình');

-- 2. Department mặc định cho cả team
IF NOT EXISTS (SELECT 1 FROM auth.Departments WHERE Code = 'DEV')
    INSERT INTO auth.Departments (Id, Name, Code, IsActive, CreatedAt)
    VALUES (NEWID(), 'Development Team', 'DEV', 1, GETUTCDATE());

DECLARE @DeptId UNIQUEIDENTIFIER = (SELECT Id FROM auth.Departments WHERE Code = 'DEV');
DECLARE @PasswordHash NVARCHAR(200) = '$2a$11$aUDLep9rnuEtYUdMvj6f8.I/nM2PWEjtsa2L/SwFBIInQKnVzGNBO';

-- 3. Users
DECLARE @U1 UNIQUEIDENTIFIER = NEWID(); -- Chau Gia Vinh
DECLARE @U2 UNIQUEIDENTIFIER = NEWID(); -- Dang Thanh Thi
DECLARE @U3 UNIQUEIDENTIFIER = NEWID(); -- Nguyen Quoc Thanh
DECLARE @U4 UNIQUEIDENTIFIER = NEWID(); -- Nguyen Trong Nguyen
DECLARE @U5 UNIQUEIDENTIFIER = NEWID(); -- Nguyen Tan Loc

IF NOT EXISTS (SELECT 1 FROM auth.Users WHERE Username = 'vinhgiachau')
INSERT INTO auth.Users (Id, Username, PasswordHash, FullName, Email, DepartmentId, IsActive, CreatedAt)
VALUES (@U1, 'vinhgiachau', @PasswordHash, N'Châu Gia Vinh', 'vinhgiachau@gmail.com', @DeptId, 1, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM auth.Users WHERE Username = 'dangthanhthi213')
INSERT INTO auth.Users (Id, Username, PasswordHash, FullName, Email, DepartmentId, IsActive, CreatedAt)
VALUES (@U2, 'dangthanhthi213', @PasswordHash, N'Đặng Thành Thi', 'dangthanhthi213@gmail.com', @DeptId, 1, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM auth.Users WHERE Username = '28122004thanh102')
INSERT INTO auth.Users (Id, Username, PasswordHash, FullName, Email, DepartmentId, IsActive, CreatedAt)
VALUES (@U3, '28122004thanh102', @PasswordHash, N'Nguyễn Quốc Thành', '28122004thanh102@gmail.com', @DeptId, 1, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM auth.Users WHERE Username = 'nguyentrongnguyen3004')
INSERT INTO auth.Users (Id, Username, PasswordHash, FullName, Email, DepartmentId, IsActive, CreatedAt)
VALUES (@U4, 'nguyentrongnguyen3004', @PasswordHash, N'Nguyễn Trọng Nguyên', 'nguyentrongnguyen3004@gmail.com', @DeptId, 1, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM auth.Users WHERE Username = 'nguyentanloc24052005')
INSERT INTO auth.Users (Id, Username, PasswordHash, FullName, Email, DepartmentId, IsActive, CreatedAt)
VALUES (@U5, 'nguyentanloc24052005', @PasswordHash, N'Nguyễn Tấn Lộc', 'nguyentanloc24052005@gmail.com', @DeptId, 1, GETUTCDATE());

-- 4. Gán Role tương ứng (tiếng Anh)
INSERT INTO auth.UserRoles (UserId, RoleId)
SELECT u.Id, r.Id FROM auth.Users u, auth.Roles r
WHERE u.Username = 'vinhgiachau' AND r.Name = N'Admin'
  AND NOT EXISTS (SELECT 1 FROM auth.UserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);

INSERT INTO auth.UserRoles (UserId, RoleId)
SELECT u.Id, r.Id FROM auth.Users u, auth.Roles r
WHERE u.Username = 'dangthanhthi213' AND r.Name = N'SecretaryDirector'
  AND NOT EXISTS (SELECT 1 FROM auth.UserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);

INSERT INTO auth.UserRoles (UserId, RoleId)
SELECT u.Id, r.Id FROM auth.Users u, auth.Roles r
WHERE u.Username = '28122004thanh102' AND r.Name = N'Staff'
  AND NOT EXISTS (SELECT 1 FROM auth.UserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);

INSERT INTO auth.UserRoles (UserId, RoleId)
SELECT u.Id, r.Id FROM auth.Users u, auth.Roles r
WHERE u.Username = 'nguyentrongnguyen3004' AND r.Name = N'Staff'
  AND NOT EXISTS (SELECT 1 FROM auth.UserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);

INSERT INTO auth.UserRoles (UserId, RoleId)
SELECT u.Id, r.Id FROM auth.Users u, auth.Roles r
WHERE u.Username = 'nguyentanloc24052005' AND r.Name = N'Secretary'
  AND NOT EXISTS (SELECT 1 FROM auth.UserRoles ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);

-- 5. Kiểm tra kết quả (mỗi user phải chỉ có đúng 1 dòng)
SELECT u.Username, u.FullName, u.Email, r.Name AS Role
FROM auth.Users u
LEFT JOIN auth.UserRoles ur ON ur.UserId = u.Id
LEFT JOIN auth.Roles r ON r.Id = ur.RoleId
ORDER BY u.CreatedAt;

IF NOT EXISTS (SELECT 1 FROM auth.Departments WHERE Code = N'MGM')
    INSERT INTO auth.Departments (Id, Name, Code, IsActive, CreatedAt)
    VALUES (NEWID(), N'Management', N'MGM', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM auth.Departments WHERE Code = N'PRD')
    INSERT INTO auth.Departments (Id, Name, Code, IsActive, CreatedAt)
    VALUES (NEWID(), N'Production', N'PRD', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM auth.Departments WHERE Code = N'HSE')
    INSERT INTO auth.Departments (Id, Name, Code, IsActive, CreatedAt)
    VALUES (NEWID(), N'HSE', N'HSE', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM auth.Departments WHERE Code = N'PRJ')
    INSERT INTO auth.Departments (Id, Name, Code, IsActive, CreatedAt)
    VALUES (NEWID(), N'Project', N'PRJ', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM auth.Departments WHERE Code = N'SUB')
    INSERT INTO auth.Departments (Id, Name, Code, IsActive, CreatedAt)
    VALUES (NEWID(), N'Subsurface', N'SUB', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM auth.Departments WHERE Code = N'FIN')
    INSERT INTO auth.Departments (Id, Name, Code, IsActive, CreatedAt)
    VALUES (NEWID(), N'Finance', N'FIN', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM auth.Departments WHERE Code = N'ADM')
    INSERT INTO auth.Departments (Id, Name, Code, IsActive, CreatedAt)
    VALUES (NEWID(), N'Administration', N'ADM', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM auth.Departments WHERE Code = N'C&P')
    INSERT INTO auth.Departments (Id, Name, Code, IsActive, CreatedAt)
    VALUES (NEWID(), N'C&P', N'C&P', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM auth.Departments WHERE Code = N'DRL')
    INSERT INTO auth.Departments (Id, Name, Code, IsActive, CreatedAt)
    VALUES (NEWID(), N'Drilling', N'DRL', 1, GETUTCDATE());

DECLARE @SeedUserId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Users WHERE Username = 'vinhgiachau');
IF @SeedUserId IS NULL SET @SeedUserId = NEWID();

IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVEP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'HLHV Partners', N'PVEP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 17:26:42', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVEP2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'HL Partners', N'PVEP2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 17:27:14', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVEP3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'HV Partners', N'PVEP3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 17:29:03', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PetroVietnam')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vietnam National Oil & Gas Group', N'PetroVietnam', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 17:29:31', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVEP4')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PetroVietnam Exploration & Production Corporation', N'PVEP4', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 17:29:53', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TLJOC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Thang Long JOC', N'TLJOC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 17:30:20', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SOCO')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'SOCO Vietnam Ltd.', N'SOCO', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 19:32:54', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'OPECO')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'OPECO Vietnam Ltd.', N'OPECO', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 19:33:31', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PTTEP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PTTEP Hoang Long Ltd.', N'PTTEP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 19:34:17', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PTTEP2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PTTEP Hoan Vu Ltd.', N'PTTEP2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 19:34:41', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVN & PVEP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'HL Partners & PVN', N'PVN & PVEP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 19:38:39', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVN & PVEP2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'HV Partners & PVN', N'PVN & PVEP2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 19:40:23', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVN & PVEP3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'HLHV Partners & PVN', N'PVN & PVEP3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-05 19:42:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VSP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'VietsovPetro', N'VSP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-06 13:24:09', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVGas', N'P', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-06 13:52:00', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PQ POC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phu Quoc POC', N'PQ POC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-06 13:54:40', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVDrilling', N'P2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-06 15:15:35', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVDTM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVD Tubulars Management Co., Ltd.', N'PVDTM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-07 18:40:49', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'S')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'SOCO', N'S', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-10 09:08:21', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'HS Tech')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Hoang Son Tech', N'HS Tech', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-10 10:12:57', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'V')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'VPI', N'V', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-10 15:38:04', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PSP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phuoc Son & Partners', N'PSP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-10 15:39:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVD Baker Hughes')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PV Drilling - Baker Hughes Well Technical Services Joint Venture Company Limited', N'PVD Baker Hughes', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-10 16:40:42', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CTL')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Công ty Luật', N'CTL', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-11 16:04:29', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'GPWS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Global Petrowell Service Company Limited', N'GPWS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-12 09:17:00', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VietsovPetro - OGPE')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'VietsovPetro - Oil and Gas Production Enterprise', N'VietsovPetro - OGPE', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-12 10:03:43', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Halliburton')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Halliburton International GmbH', N'Halliburton', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-12 10:05:48', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'POH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PV Oil HCM', N'POH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-12 10:46:33', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'POVT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PV Oil Vung Tau', N'POVT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-12 10:47:28', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PPS', N'P3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-12 14:28:20', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVD Tech', N'PT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-13 16:06:25', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'NV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Nhan Viet', N'NV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-14 13:37:01', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVEP - HCM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Chi nhánh Tổng công ty TDKT tại TP. HCM', N'PVEP - HCM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-17 16:39:09', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BHXH TP.HCM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bảo Hiem Xã hội TPHCM', N'BHXH TP.HCM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-18 09:29:17', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'MIV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'M-I Vietnam', N'MIV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-19 14:23:41', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'DNV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'DNV Vietnam Company Limited', N'DNV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-20 09:28:09', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VDK')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Viện Dầu Khí', N'VDK', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-20 11:02:47', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'A')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'APDS', N'A', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-20 14:53:09', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P4')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVFCCo', N'P4', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-24 15:37:42', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'D')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'DNV', N'D', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-25 10:43:05', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Eastsea Star')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Eastsea Star Software Company Limited', N'Eastsea Star', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-25 16:44:53', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Bien Dong')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bien Dong Oil & Gas', N'Bien Dong', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-27 16:43:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Petro Enertech')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petro Enertech Corporation', N'Petro Enertech', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-27 16:49:58', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Saigon Trade')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Saigon Equipment and Trade Services Corporation', N'Saigon Trade', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-01-27 16:51:02', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'UBND TP HCM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Ủy ban nhân dân TP.HCM', N'UBND TP HCM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-08 13:54:02', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BTT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bộ Thông Tin', N'BTT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-09 11:08:52', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'S2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'SEN', N'S2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-11 11:07:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'UT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'UBND TP', N'UT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-18 15:36:50', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'MM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Mermaid Maritime', N'MM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-21 16:46:21', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'KT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Kim Thiet', N'KT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-22 09:32:52', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P5')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'POS', N'P5', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-23 11:08:37', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P6')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petrosetco', N'P6', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-23 11:09:22', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P7')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVCHEM', N'P7', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-23 14:50:21', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PTSC MARINE')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PTSC MARINE', N'PTSC MARINE', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-24 13:09:39', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Tan Cang')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Tan Cang Offshore Services Joint Stock Company', N'Tan Cang', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-24 13:10:38', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'128')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'128 One Member Liability Limited Company', N'128', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-24 13:11:09', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'HADUCO')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Hai Duong Petroleum and Marine Corporation', N'HADUCO', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-24 13:11:34', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ASHICO')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Asia Investment and Asset Management Joint Stock Company', N'ASHICO', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-02-24 13:12:01', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVEP Song Hong')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVEP Song Hong', N'PVEP Song Hong', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-04 13:07:21', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVI South', N'PS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-04 13:08:42', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'MI')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'MI Oil & Gas Services Vietnam L.L.C', N'MI', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-04 13:15:30', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P8')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVI', N'P8', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-04 14:35:03', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'OWS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Offshore Weather Services', N'OWS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-07 08:54:17', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'F')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Furgo', N'F', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-07 08:54:25', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'S3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'SRHMC', N'S3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-07 15:18:07', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'L0909')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Lô 01-97 & 02-97', N'L0909', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-07 15:19:44', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SLB')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Schlumberger', N'SLB', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-09 15:11:26', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ES')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'EAM Solution', N'ES', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-09 15:12:59', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'A2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Aban', N'A2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-09 15:13:41', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PE')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petro Enertech', N'PE', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-09 15:27:41', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VNHSouth')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Southern Vietnam Helicopter Company', N'VNHSouth', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-11 14:20:40', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TCT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Tổng Cục Thuế', N'TCT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-14 10:24:50', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VAM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vinh Nam Joint', N'VAM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-17 14:09:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TAS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Thai an safety', N'TAS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-18 11:18:36', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Scomi')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Scomi Oiltools Thailand Ltd.', N'Scomi', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-20 16:25:10', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'NOV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'National Oilwell Varco (Thailand) Limited', N'NOV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-20 16:25:25', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVD Invest')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petrovietnam Drilling Investment Services Company Ltd.', N'PVD Invest', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-20 16:25:51', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'T')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Tonkin', N'T', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-24 10:32:57', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P9')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVMR', N'P9', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-24 11:04:52', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'EA')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'ES Automation', N'EA', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-24 11:07:35', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BLĐ')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bộ Lao Động', N'BLĐ', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-28 13:31:36', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P10')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phateco', N'P10', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-29 10:50:55', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P11')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PTSC', N'P11', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-29 14:16:13', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Vallourec')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vallourec Asia Pacific Corp', N'Vallourec', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-30 15:42:00', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VAPCP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vallourec Asia Pacific Corp Pte. Ltd.', N'VAPCP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-30 15:45:50', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'J')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'JVPC', N'J', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-03-31 09:37:23', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'C')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Clariant', N'C', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-05 10:54:05', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PSB')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PTSC Supply Base', N'PSB', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-06 08:37:35', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SLTTH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Sở LĐ-TBXH TP.HCM', N'SLTTH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-06 11:26:44', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'E')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'EOSS', N'E', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-07 09:36:02', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'MICCO')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'MICCO', N'MICCO', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-12 14:04:29', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PCSPVCCA')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phòng Cảnh sát PCCC và CNCH – Công an tỉnh BR-VT', N'PCSPVCCA', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-13 15:08:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P12')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PSL', N'P12', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-13 15:56:25', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PW')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVD Well', N'PW', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-13 16:02:00', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'V2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'VPCP', N'V2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-14 16:43:41', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VPCP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Văn Phòng Chính Phủ', N'VPCP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-15 08:43:44', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'HWL')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Honeywell Pte Ltd', N'HWL', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-18 12:47:13', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CCSQLHCV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cục Cảnh sát - Quản lý Hành chính về Trật tự Xã hội - Bộ Công An', N'CCSQLHCV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-19 16:10:22', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'A3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Aberdeen', N'A3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-22 13:38:29', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'AE')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'ADC Energy', N'AE', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-25 10:29:37', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BCT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bộ Công Thương', N'BCT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-25 15:58:25', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TN')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Thien Nam', N'TN', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-26 11:52:00', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TCĐDK')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Trường Cao Đẳng Dầu Khí', N'TCĐDK', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-29 13:10:20', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TH Hill')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bureau Veritas North America, Inc. dba T H Hill Associates', N'TH Hill', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-29 14:21:39', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVE')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PetroVietnam Engineering Consultancy J.S.C', N'PVE', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-04-29 15:10:35', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVD Offshore')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVD Offshore Services Co Ltd', N'PVD Offshore', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-09 11:27:07', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'O.S Offshore')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'O.S Offshore Services Equipment Co Ltd', N'O.S Offshore', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-09 12:13:07', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CHQ')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cục Hải Quan', N'CHQ', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-10 10:56:11', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TTCP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Thủ Tướng Chính Phủ', N'TTCP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-10 10:58:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'D2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Datawise', N'D2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-10 14:36:28', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'GP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Gia Phat', N'GP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-10 15:56:21', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'HM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'HD Marine', N'HM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-12 09:38:48', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PR')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petrofac RNZ', N'PR', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-12 09:44:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'OS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'O.S', N'OS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-12 14:22:07', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TTT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Thuong Thien Tech', N'TTT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-13 10:16:12', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'V3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vietgazprom', N'V3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-13 10:28:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PCSQVTCA')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phòng Cảnh sát QLHC về TTXH -  Công an Tỉnh BRVT', N'PCSQVTCA', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-16 09:16:29', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CHCBCT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cục Hóa Chất - Bộ Công Thương', N'CHCBCT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-17 11:22:37', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PEC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petrovietnam Engineering Consultancy Joint Stock company', N'PEC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-17 15:06:09', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'RANHILL')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Ranhill Worley Sdn Bhd', N'RANHILL', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-17 15:09:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'RNZ')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'RNZ Integrated (M) Sdn Bhd', N'RNZ', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-17 15:09:52', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'EDG')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'EDG Overseas Inc.', N'EDG', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-17 15:10:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TECHNIP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Technip Geoproduction Sdn Bhd', N'TECHNIP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-17 15:10:57', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVD Logging')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petroleum Well Logging Company Limited', N'PVD Logging', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-17 16:03:22', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Amsito')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Amsito Oilwell Services Company Limited', N'Amsito', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-17 16:06:26', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVMTC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petrovietnam Manpower Training College', N'PVMTC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-19 13:21:42', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'JST Vietnam')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'JST Vietnam', N'JST Vietnam', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-19 13:27:19', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Seamap')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Center for Sea Survey and Mapping', N'Seamap', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-19 16:01:10', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PTSC Geos and Subsea')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PTSC G&S', N'PTSC Geos and Subsea', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-19 16:14:22', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'UBND TP. Vũng Tàu')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Ủy ban nhân dân Thành phố Vũng Tàu', N'UBND TP. Vũng Tàu', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-20 09:08:51', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PQLĐTTPV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phòng Quản lý Đô thị Thành phố Vũng Tàu', N'PQLĐTTPV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-20 09:09:07', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CTCCVCXĐ')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Công ty CPPT Công viên Cây xanh & Đô thị Vũng Tàu', N'CTCCVCXĐ', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-20 09:09:22', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'O')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'OTS', N'O', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-20 15:01:36', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'UM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'United Mekong', N'UM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-20 15:02:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ĐHDK')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Đại Học Dầu Khí', N'ĐHDK', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-24 13:23:08', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PG')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PTSC Geos', N'PG', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-24 13:31:13', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVEP POC', N'PP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-25 13:36:21', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'O2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'ONGC', N'O2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-26 16:57:39', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TCTHCVDV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Tổng Công ty Hóa Chất và Dịch Vụ Dầu Khí', N'TCTHCVDV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-27 15:55:02', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'DE')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'DK Engineering Co Ltd', N'DE', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-05-31 14:14:53', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'True Tech', N'TT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-02 16:00:10', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'A4')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Ada', N'A4', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-08 11:34:38', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'C2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cosasco', N'C2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-09 14:32:39', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CCHQQLHĐ')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Chi cục hải quan quản lý hàng đầu tư TP.HCM', N'CCHQQLHĐ', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-14 15:59:47', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BVV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bereau Veritas VN', N'BVV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-16 09:30:53', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TCT2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Tổng cục Thuế', N'TCT2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-16 14:15:05', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BVV2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bereau Veritas VN', N'BVV2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-16 14:16:35', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VI')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vector InfoTech', N'VI', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-17 16:17:28', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Vietubes')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vietubes Corporation', N'Vietubes', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-20 15:26:35', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PT2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phap Tri', N'PT2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-21 09:49:04', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'O3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'OFS', N'O3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-21 16:46:49', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SLĐTBXHT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Sở Lao Động Thương Binh Xã Hội TP.HCM', N'SLĐTBXHT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-24 09:42:01', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TTYTQ1TH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Trung Tâm Y Tế Quận 1, TP. HCM', N'TTYTQ1TH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-24 09:44:13', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BTTMQĐND')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bộ Tổng tham mưu Quân đội nhân dân Việt Nam', N'BTTMQĐND', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-27 10:00:42', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CLJ')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cuu Long JOC', N'CLJ', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-29 14:41:44', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CHH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cục Hàng Hải', N'CHH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-06-29 14:42:45', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VE')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vietnam Energy', N'VE', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-07-11 10:08:17', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CĐDK')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cao Đẳng Dầu Khí', N'CĐDK', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-07-11 10:09:39', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'MD')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Mathews Daniel', N'MD', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-07-12 13:47:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'A5')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'ABL', N'A5', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-07-12 13:48:00', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'S4')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Sterling', N'S4', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-07-12 14:36:30', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PO')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Premier Oil', N'PO', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-07-15 15:51:41', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VCB')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vietcombank', N'VCB', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-07-18 14:02:50', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P13')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PTT', N'P13', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-07-21 16:26:56', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BTTM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bộ Tổng Tham Mưu', N'BTTM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-07-25 14:21:23', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BCHBP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Ban Chỉ Huy Biên Phòng', N'BCHBP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-07-27 09:01:12', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'AECC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Southern Petroleum Construction Joint Stock company', N'AECC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-07-28 11:32:29', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BCA')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bộ Công An', N'BCA', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-08-02 11:10:31', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ĐHMĐC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Đại Học Mỏ Địa Chất', N'ĐHMĐC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-08-02 11:12:28', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'B')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'BAB', N'B', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-08-05 09:47:06', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'G')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Glocom', N'G', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-08-05 11:02:59', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P14')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVEP', N'P14', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-08-12 10:34:14', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'MM2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'MM Marine', N'MM2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-08-19 11:29:02', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'D3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Deloitte', N'D3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-08-22 08:54:32', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CTBĐATHH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Công ty Bảo Đảm An Toàn Hàng Hải Miền Nam', N'CTBĐATHH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-08-30 08:16:52', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'AES')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'ATA Engineer Solutions', N'AES', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-09-05 13:18:41', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bureau Veritas', N'BV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-09-08 09:37:36', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ABS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'American Bureau of Shipping Vietnam Company Limited', N'ABS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-09-12 14:37:29', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'LLOYD')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Lloyd''s Register Area (VN) Company', N'LLOYD', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-09-12 14:38:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PP2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PTSC Production', N'PP2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-09-12 15:31:07', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CKTATVMT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cục Kỹ Thuật An Toàn và Môi Trường Công Nghiệp', N'CKTATVMT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-09-13 08:20:50', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'LS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Lam Son JSC', N'LS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-09-26 17:07:33', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'B&H', N'BH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-09-29 10:25:05', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'V4')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'VEM', N'V4', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-10-04 14:29:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CVHV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cảng vụ HH VT', N'CVHV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-10-13 16:20:32', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'LSRA')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Lloyd''s Register Asia', N'LSRA', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-10-18 15:37:01', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PO2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PTSC Offshore Services', N'PO2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-10-19 15:29:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TĐHDKVN')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Trường Đại học Dầu khí Việt Nam', N'TĐHDKVN', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-10-19 16:28:32', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'A6')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Achison', N'A6', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-10-21 15:58:49', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'STNVMTTB')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Sở tài nguyên và môi trường tỉnh BR-VT', N'STNVMTTB', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-10-21 16:00:46', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'E2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'ES', N'E2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-10-27 09:49:56', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BH2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Blue Hat', N'BH2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-10-27 14:29:12', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CTDNL')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cục thuế doanh nghiệp lớn', N'CTDNL', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-11-03 16:22:05', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BTNMT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bộ Tài Nguyên Môi Trường', N'BTNMT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-11-10 16:40:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'STNMT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Sở Tài Nguyên Môi Trường', N'STNMT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-11-10 16:41:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TCNLM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Tạp chí năng lượng mới', N'TCNLM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-11-14 13:20:40', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'M')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Murphy', N'M', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-11-22 13:33:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'OMS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'OMS Oilfield Services', N'OMS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-11-24 16:30:41', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'O4')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'OSB', N'O4', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-11-28 15:52:29', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BTC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bộ Tài Chính', N'BTC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-11-29 09:56:13', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'I')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Idemitsu', N'I', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-12-09 10:47:26', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Quatest3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Quatest 3', N'Quatest3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-12-12 15:34:47', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BCHBPCKB')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Ban Chỉ Huy Biên Phòng Cửa Khẩu BR-VT', N'BCHBPCKB', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2022-12-29 22:00:46', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TCTCPKVD')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Tổng công ty cổ phần Khoan và Dịch vụ Khoan', N'TCTCPKVD', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-01-05 08:51:30', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PV College', N'PC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-01-06 16:44:33', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P15')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petrohotel', N'P15', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-01-12 09:40:54', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'V5')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Velesto', N'V5', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-01-17 13:45:31', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'J2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Jadestone', N'J2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-01-31 16:03:50', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PW2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVD WS', N'PW2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-02-07 09:13:08', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'S5')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Shintraco', N'S5', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-02-20 09:51:08', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'M2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Mega', N'M2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-02-20 09:54:50', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'E3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'EDPN', N'E3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-02-28 10:22:50', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'DEC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'DK Engineering Consultancy JSC', N'DEC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-03-13 16:58:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'C3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'CPSE', N'C3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-03-20 09:36:03', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VEM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vietnam Equipment & Metrology Services Company', N'VEM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-03-24 16:40:42', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SGT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Sở Giao Thông', N'SGT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-04-18 11:48:00', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'I2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Iwok', N'I2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-04-19 15:05:50', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'M3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'MT', N'M3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-04-28 15:33:33', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CAT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cục an toàn', N'CAT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-04-28 15:35:45', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'M4')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Malibu', N'M4', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-04-28 15:40:59', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SCP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Saigon Connecting Project', N'SCP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-05-05 09:25:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'HV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Ha Viet', N'HV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-05-05 09:35:58', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'HF')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'HP fax', N'HF', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-05-11 08:53:01', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'E4')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'ESOG', N'E4', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-05-11 11:17:19', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'B2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'BOCK', N'B2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-05-12 15:11:03', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'LH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Lam Hong', N'LH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-05-12 15:12:11', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SE')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'SK Earthon', N'SE', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-05-18 16:33:00', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'2O')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'2H Offshore', N'2O', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-05-19 15:30:39', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'DT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Dong Tay', N'DT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-05-25 11:20:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PĐDK')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phát điện dầu khí', N'PĐDK', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-06-01 11:28:23', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ES2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Eastern Sea company LTD (Hai Dong)', N'ES2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-06-06 16:39:12', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CQLXNCBC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cục Quản lý xuất nhập cảnh - Bộ Công an TP.HCM', N'CQLXNCBC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-06-20 13:57:54', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'EO')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Eastern Ocean', N'EO', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-07-06 08:39:06', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TAKA')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Taka company limited', N'TAKA', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-07-18 16:28:10', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'MATCO')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Matco Asia Vietnam Co., LTD', N'MATCO', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-07-18 16:30:29', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Thien Nang')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Thien NangTechnology Co., LTD', N'Thien Nang', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-07-18 16:31:35', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'UBT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'UBND Bình Thuận', N'UBT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-07-25 10:18:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TCTK')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Tổng cục thống kê', N'TCTK', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-07-25 10:22:53', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'M5')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Mogene', N'M5', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-08-04 16:06:25', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CATTT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cục An Toàn Thông Tin', N'CATTT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-08-11 15:49:47', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P16')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'POTs', N'P16', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-08-15 14:13:49', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P17')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVE', N'P17', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-08-16 13:24:26', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BG')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bộ GTVT', N'BG', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-08-24 14:16:49', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'KH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Kim Hung', N'KH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-09-12 14:46:47', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SD')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Shelf Drilling', N'SD', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-09-14 14:55:12', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'M6')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Motec', N'M6', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-09-29 10:31:03', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'DM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'DMC-MT', N'DM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-09-29 14:53:18', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'DT2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Duc Tung', N'DT2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-10-04 14:50:26', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'C4')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'CISR', N'C4', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-10-11 11:08:23', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Z')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Zanest', N'Z', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-10-19 10:33:02', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PLG')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PLG Trading & Technical Services Co., Ltd', N'PLG', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-10-20 10:51:40', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'LE VU')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Le Vu Technical Technology Company Limited', N'LE VU', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-10-20 10:52:27', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'KVA')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'KVA Engineering Services Joint Stock Company', N'KVA', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-10-20 10:52:54', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TTATVBVM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Trung Tâm An Toàn và Bảo Vệ Môi Trường Vietsovpetro', N'TTATVBVM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-10-23 14:52:35', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Weatherford')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Weatherford Asia Pacific Pte Ltd', N'Weatherford', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-10-31 13:24:48', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Z2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Zarubezhneft', N'Z2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-11-16 10:04:21', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PHLHV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PTTEP Hoang Long Hoan Vu', N'PHLHV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-11-21 09:31:42', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'H')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Heatex', N'H', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-11-24 15:53:44', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'DP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'DDS Petro', N'DP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-12-01 14:18:59', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'DQ')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Drill-Quip', N'DQ', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-12-06 16:45:46', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CAPBNQ1T')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Công an phường Bến Nghé, Quận 1, TP. HCM', N'CAPBNQ1T', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-12-07 11:31:13', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ĐKTTVKVN')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Đài Khí Tượng Thủy Văn Khu Vực Nam Bộ', N'ĐKTTVKVN', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-12-26 09:55:43', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'HCTTETTT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Hội Cứu Trợ Trẻ Em Tàn Tật TP.HCM', N'HCTTETTT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2023-12-26 09:57:52', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PB00')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVEP Blocks 01&02', N'PB00', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-01-10 10:12:26', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'E5')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Eni', N'E5', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-01-10 10:41:34', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'B3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'BSR', N'B3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-01-12 08:42:56', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Bitexco')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bitexco Energy', N'Bitexco', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-01-17 10:02:47', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CATBXVHN')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cục An Toàn Bức Xạ và Hạt Nhân', N'CATBXVHN', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-01-18 08:52:57', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SKHVCNTP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Sở Khoa học và Công nghệ Thành phố Hồ Chí Minh', N'SKHVCNTP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-01-18 08:53:28', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ĐUP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Đảng Uỷ PVEP', N'ĐUP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-01-18 15:19:14', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ST')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Solar turbine', N'ST', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-01-23 16:48:57', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P18')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PE Limited', N'P18', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-02-14 12:52:34', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'W')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Weatherford', N'W', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-02-16 16:22:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P19')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PTTEP', N'P19', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-02-21 09:55:18', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'S6')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Seokyoung', N'S6', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-02-21 15:03:40', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BTP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bộ Tư Pháp', N'BTP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-02-22 15:52:42', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BK')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bộ KHDT', N'BK', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-02-22 15:52:51', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VTKNLVPT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vụ Tiết Kiệm Năng Lượng và Phát Triển Bền Vững - Bộ Công Thương', N'VTKNLVPT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-02-26 15:12:18', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'KKTHHTĐH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Khoa Kỹ Thuật Hóa Học - Trường Đại Học Bách Khoa - ĐHQG TP.HCM', N'KKTHHTĐH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-02-26 15:13:04', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'KHTĐHCNT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Khoa Hóa - Trường Đại Học Công Nghiệp TP.HCM', N'KHTĐHCNT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-02-26 15:13:41', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'H2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Hapco', N'H2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-02-28 10:16:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'OGO')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Oil & Gas Operators', N'OGO', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-02-29 11:07:46', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SOG')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'SPM Oil & Gas', N'SOG', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-04 08:56:06', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'LV-Tech')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'LV-Tech Company Limited', N'LV-Tech', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-07 14:31:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TDVTQDV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Tap doan vien thong quan doi (Viettel)', N'TDVTQDV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-07 17:04:50', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'NPX')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Nam Phuong Xanh', N'NPX', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-12 08:36:02', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ST2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Saigontourist Travel Services Company Limited', N'ST2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-12 08:40:08', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'V6')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vietravel Company Limited', N'V6', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-12 08:40:32', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vietluxtour Travel Company Limited', N'VT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-12 08:40:50', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Emerson')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Emerson Process Management (Malaysia) Sdn Bhd.', N'Emerson', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-12 15:04:21', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Bao Nghi')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bao Nghi Techniques Services Trading Company Limited', N'Bao Nghi', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-12 18:39:38', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'EO2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Executive Offshore', N'EO2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-15 14:09:25', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'NTSSI')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Nam Truong Son System Integration Company', N'NTSSI', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-26 11:44:29', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'V7')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'VEMS', N'V7', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-26 13:58:30', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P20')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petrotimes', N'P20', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-03-27 15:56:01', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PCSQLHCV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phòng Cảnh sát Quản lý Hành chính về Trật tự Xã hội - Công an Tp. HCM', N'PCSQLHCV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-04-02 10:13:29', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'NO')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'National Oilwell', N'NO', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-04-04 14:28:00', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vietnam Mice', N'VM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-04-04 15:48:21', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'LVL')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'LA VIE LIMITED LIABILITY COMPANY', N'LVL', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-04-09 09:43:14', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'F2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'FPT', N'F2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-04-23 13:09:39', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'OV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'ONGC VIDESH LTD', N'OV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-04-23 14:47:18', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TD')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'THIEN DUC', N'TD', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-04-23 15:27:41', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PKM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVEP Khánh Mỹ', N'PKM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-05-08 10:38:58', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CGG')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'CGG Services (Singapore) Pte Ltd', N'CGG', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-05-13 14:10:22', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Armada TGT LTD')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Armada TGT LTD', N'Armada TGT LTD', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-05-15 09:00:44', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BAB')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bumi Armada Berhad', N'BAB', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-05-15 09:03:34', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PDW')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PV Drilling & Well Service', N'PDW', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-05-16 11:32:21', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P21')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petronas', N'P21', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-05-22 11:35:38', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TAS2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Thai An Safety Co., Ltd', N'TAS2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-05-23 10:10:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TK')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Tuong Khoa Trading & Services Ltd', N'TK', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-05-23 10:10:38', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'EOS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Eastern Ocean Shipping Service Co., Ltd', N'EOS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-05-23 10:11:08', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'LSMSO')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Lam Son Marine Supplies & Offshore Services JSC', N'LSMSO', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-05-23 10:11:35', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'HST')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Hoang Son Technology Trading Co., Ltd', N'HST', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-05-23 10:12:04', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'V8')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'VNPT', N'V8', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-05-24 15:45:53', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'UBQLV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Uy Ban Quan Ly Von', N'UBQLV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-06-04 14:46:06', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'AIS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Avenue IT Solutions', N'AIS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-06-12 14:20:31', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P22')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'P2P', N'P22', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-06-12 16:00:00', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PGS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PV Gas SE', N'PGS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-06-20 08:52:10', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PE2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PNK engineering Co., Ltd', N'PE2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-06-21 14:32:22', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SCTTBV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Sở Công Thương Tỉnh BR-VT', N'SCTTBV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-06-27 14:24:49', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ĐVLGK')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Địa Vật Lý Giếng Khoan', N'ĐVLGK', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-07-22 15:41:11', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'EL')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Evergreen Line', N'EL', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-07-31 14:12:57', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CTCPDVHH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Công ty Cổ phần Dịch vụ hàng hóa Tân Sơn Nhất', N'CTCPDVHH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-07-31 14:26:21', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CTCPDVHH2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Công ty Cổ phần Dịch vụ hàng hóa Sài Gòn', N'CTCPDVHH2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-07-31 14:27:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'M7')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'MMS', N'M7', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-08-08 11:03:09', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PT3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVD Training', N'PT3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-08-09 11:06:46', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TTIVN')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Trung Tâm Internet Viet Nam', N'TTIVN', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-08-12 15:15:51', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CVĐTNĐ')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cảng Vụ Đường Thủy Nội Địa', N'CVĐTNĐ', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-08-12 15:18:34', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VT2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vietnam Technology', N'VT2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-08-28 17:14:36', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TTTS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Trung Tâm Tần Số', N'TTTS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-09-16 14:19:42', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PCSPCCAT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phòng Cảnh sát PCCC & CNCH - Công an tỉnh BR-VT', N'PCSPCCAT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-09-23 09:06:53', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CTCPPBDK')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Công ty Cổ phần Phân bón Dầu khí Cà Mau', N'CTCPPBDK', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-09-26 15:56:39', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'NMĐCM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Nhà máy Đạm Cà Mau', N'NMĐCM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-09-26 15:56:55', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Tien Cang')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Tien Cang', N'Tien Cang', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-09-28 18:33:43', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ĐHM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Đại Học Mỏ', N'ĐHM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-10-03 11:23:49', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CVT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cục Viễn Thông', N'CVT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-10-08 11:48:13', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SAP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'SPE Asia Pacific', N'SAP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-10-24 09:05:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PHK')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Pham Hong Ky', N'PHK', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-10-24 09:23:43', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VTK')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vo Thanh Khoa', N'VTK', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-10-24 10:17:39', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PI')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVI Insurance', N'PI', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-10-24 14:54:33', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'HE')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Harbour Energy', N'HE', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-11-12 15:41:49', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VMED')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'VIETSOVPETRO MECHANICAL & ENERGY DIVISION', N'VMED', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-11-26 16:55:46', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PI2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVEP - ITC', N'PI2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-11-28 16:25:19', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Formula')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'FORMULA ASIA VIET NAM CO., LTD', N'Formula', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-12-12 09:18:37', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'"HATASI"')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Hatasi Technical Trading & Industry Service Company Limited', N'"HATASI"', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-12-12 09:48:07', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'E6')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Eneos', N'E6', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-12-12 09:49:05', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'MLP')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Me Linh Point', N'MLP', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-12-13 14:16:48', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'S7')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Seahorse', N'S7', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-12-18 15:05:45', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CTCPATMT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Công ty Cổ phần An Toàn Môi Trường Dầu Khí', N'CTCPATMT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-12-19 15:36:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TĐHCNTH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Trường Đại học Công nghiệp TP. HCM', N'TĐHCNTH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-12-19 15:37:38', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VHDHNT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Viện hải dương học Nha Trang', N'VHDHNT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-12-19 15:38:56', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PSCKSTC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'People''s Committee of King Song Truong Commune', N'PSCKSTC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-12-27 14:28:14', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'UBNDXKST')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Uỷ ban nhân dân xã Kim Song Trường', N'UBNDXKST', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-12-27 14:28:38', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CCHQCKCV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Chi Cục Hải Quan Cửa Khẩu Cảng VT', N'CCHQCKCV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2024-12-27 14:49:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Truong Manh', N'TM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-01-07 13:50:03', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SYTTH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Sở Y tế TP. HCM', N'SYTTH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-01-15 10:49:05', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'IAC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'IAC - System Co., Ltd', N'IAC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-01-21 08:09:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VNERGY')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'VNERGY', N'VNERGY', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-01-21 08:09:46', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VEMS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vietnam Equipment Metrology Services', N'VEMS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-01-21 08:11:52', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PV&N - TECH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PV&N TECHNICAL SERVICE COMPANY LIMITED', N'PV&N - TECH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-02-06 14:54:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'A7')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Alia', N'A7', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-02-25 10:45:02', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'V9')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Vietba', N'V9', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-02-25 10:45:54', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PN')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PV&N', N'PN', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-03-04 09:34:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'F3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Frasers', N'F3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-03-11 13:34:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CCĐKS9')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Chi cục đăng kiểm số 9', N'CCĐKS9', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-03-11 13:43:05', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Thang Nhat')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Thang Nhat Subsea', N'Thang Nhat', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-03-18 10:38:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'VPD')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'VAN PHAT DAT JSC', N'VPD', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-03-24 12:02:43', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'MDTHL')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Ms. Do Thi Hau (Lessor)', N'MDTHL', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-03-24 12:04:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'LVCC')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Lac Viet Computing Corp.', N'LVCC', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-03-25 09:22:01', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PB009')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVN - Blocks 01- & 02-97', N'PB009', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-04-01 10:08:02', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'HHLV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Hàng hải Long Vũ', N'HHLV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-04-26 14:03:37', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BNNVMT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Bộ Nông Nghiệp và Môi Trường', N'BNNVMT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-04-28 10:20:09', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CNNT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Công nghệ niềm tin', N'CNNT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-05-06 13:45:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'V10')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'VGCE', N'V10', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-05-14 09:36:58', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P23')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petrosouth', N'P23', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-05-20 11:05:27', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CA')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'CNTT Avenue', N'CA', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-05-30 16:05:11', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PCD')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PV Chem DMC', N'PCD', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-06-09 15:26:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Z3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Znepvn', N'Z3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-06-11 13:47:30', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SNVTPHCM')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Sở Nội vụ - Thành phố Hồ Chí Minh', N'SNVTPHCM', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-07-08 16:16:37', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'MLG')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Mai Linh Group Corporation', N'MLG', 'Both', NULL, NULL, NULL, N'0315161593', 1, 0, '2025-07-09 11:23:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PI3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVEP-ITC', N'PI3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-07-15 09:41:32', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'DownUnder GeoSolutions')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'DUG', N'DownUnder GeoSolutions', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-07-15 09:53:03', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CCTDNL')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Chi Cục Thuế Doanh Nghiệp Lớn', N'CCTDNL', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-07-18 16:47:52', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVNIEG')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petrovietnam (Vietnam National Industry - Energy Group)', N'PVNIEG', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-07-22 09:54:28', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'SNNVMTTH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Sở Nông nghiệp và Môi trường TP. HCM', N'SNNVMTTH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-07-22 15:51:23', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'D4')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'DMC', N'D4', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-07-31 14:01:00', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PCSQVTCA2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phòng Cảnh sát QLHC về TTXH - Công an Thành phố Hồ Chí Minh', N'PCSQVTCA2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-09-05 09:10:54', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PCSPCCAT2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phòng Cảnh sát PCCC & CNCH - Công an TP.HCM', N'PCSPCCAT2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-09-05 09:13:53', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PVOIL')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVOIL', N'PVOIL', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-09-16 14:52:28', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PCSPCCAT3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Phòng Cảnh sát PCCC & CNCH - Công an TP. HCM', N'PCSPCCAT3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-09-18 16:01:41', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P24')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVN', N'P24', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-09-25 11:08:28', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'O5')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'OPECO', N'O5', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-10-10 11:22:36', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'CVHHTH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Cảng vụ Hàng hải TP. HCM', N'CVHHTH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-10-23 14:58:33', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'UBNDPRDT')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Uỷ ban nhân dân Phường Rạch Dừa, TP. HCM', N'UBNDPRDT', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-10-24 08:28:23', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PCL')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVEP Cuu Long', N'PCL', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-11-03 11:53:49', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Thai Hoa')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Thai Hoa Diving Services JSC', N'Thai Hoa', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-12-01 09:57:15', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ĐCCVCNCH')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Đội Chữa cháy và Cứu nạn cứu hộ khu vực 24', N'ĐCCVCNCH', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-12-02 10:28:45', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P25')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PLG', N'P25', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-12-09 16:20:11', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TTYTKVSG')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Trung Tâm Y Tế Khu Vực Sài Gòn', N'TTYTKVSG', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-12-12 16:11:41', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PPL')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PPL Investment Trading Joint Stock Company', N'PPL', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-12-24 09:38:58', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'PB')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'PVD BH', N'PB', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-12-30 14:53:29', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'E7')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'ESS', N'E7', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2025-12-30 14:53:50', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'T2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'TDH', N'T2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2026-01-30 11:18:24', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'GS')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Global system', N'GS', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2026-02-13 11:20:08', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'Expro')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Expro Overseas Inc.', N'Expro', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2026-03-17 14:55:35', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'HHBD')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Hoang hai bien dong', N'HHBD', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2026-03-20 13:47:01', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'P26')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Petrovietnam', N'P26', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2026-03-30 15:23:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'LV')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'LUA VIET CO., LTD', N'LV', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2026-06-12 17:01:55', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'BTT2')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'BEN THANH TOURIST', N'BTT2', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2026-06-12 17:10:57', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'ST3')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'SAO THIEN', N'ST3', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2026-06-12 17:21:51', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'RPB')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'RONG PHUONG BAC', N'RPB', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2026-06-12 17:29:33', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'V11')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'VRJ', N'V11', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2026-06-18 15:33:01', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'C5')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Chiron', N'C5', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2026-07-29 14:04:16', @SeedUserId);
IF NOT EXISTS (SELECT 1 FROM partner.Partners WHERE ShortName = N'TCTTDKTD')
    INSERT INTO partner.Partners (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES (NEWID(), N'Tổng Công ty Thăm dò Khai thác Dầu khí', N'TCTTDKTD', 'Both', NULL, NULL, NULL, NULL, 1, 0, '2026-07-29 16:44:02', @SeedUserId);

-- Kiem tra ket qua
SELECT COUNT(*) AS TotalDepartments FROM auth.Departments;
SELECT COUNT(*) AS TotalPartners FROM partner.Partners;-- =====================================================================
-- Seed document.Documents tu DAS.sql (bang goc doc_incomings/doc_outgoings/doc_internals)
-- Lay mau 150 CV Den + 150 CV Di + 100 CV Noi bo gan nhat (du du test, khong seed het
-- ~16,700 dong goc de tranh file qua nang).
-- Mapping status: 'In Progress'->Draft, 'Distributed'->Distributed, 'Cancelled'->Draft
-- (backend chi co 3 status, khong co Cancelled nen fallback ve Draft).
-- =====================================================================
DECLARE @DocSeedUserId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Users WHERE Username = 'vinhgiachau');
IF @DocSeedUserId IS NULL SET @DocSeedUserId = NEWID();

IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0683/HL')
BEGIN
  DECLARE @PID_6518 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6518 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0683/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of Well completion for TGT-20X well in 2026 Drilling campaignrn', NULL, @PID_6518, NULL, @DocSeedUserId, '2026-07-10', '2026-07-15 16:17:05', '2026-07-15 16:17:05', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0684/HL')
BEGIN
  DECLARE @PID_6519 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6519 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0684/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of E-line perforating and pipe recovery for TGT-20X well in 2026 Drilling campaignrn', NULL, @PID_6519, NULL, @DocSeedUserId, '2026-07-10', '2026-07-15 16:17:55', '2026-07-15 16:17:55', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0685/HL')
BEGIN
  DECLARE @PID_6520 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PGS');
  IF @PID_6520 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0685/HL', 'INCOMING', 'Distributed', N'PV Gas SE - TGT & HSDT Provisional Monthly Allocation Statement for Jun 2026rn', NULL, @PID_6520, NULL, @DocSeedUserId, '2026-07-13', '2026-07-15 16:18:44', '2026-07-15 16:18:44', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0686/HV')
BEGIN
  DECLARE @PID_6521 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6521 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0686/HV', 'INCOMING', 'Distributed', N'PVEP - Postpone CNV 2026 Pressure Survey Campaignrn', NULL, @PID_6521, NULL, @DocSeedUserId, '2026-07-13', '2026-07-15 16:19:17', '2026-07-15 16:19:17', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0687/HLHV')
BEGIN
  DECLARE @PID_6522 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6522 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0687/HLHV', 'INCOMING', 'Distributed', N'PVEP - 2026 MCMs schedule proposal for HLHVJOCrn', NULL, @PID_6522, NULL, @DocSeedUserId, '2026-07-13', '2026-07-15 16:20:00', '2026-07-15 16:20:00', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0688/HL')
BEGIN
  DECLARE @PID_6523 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PB');
  IF @PID_6523 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0688/HL', 'INCOMING', 'Distributed', N'PVD BH - BC2 - Cementing for TGT-20X well in 2026 drillingrn', NULL, @PID_6523, NULL, @DocSeedUserId, '2026-07-13', '2026-07-15 16:21:03', '2026-07-15 16:21:03', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0689/HL')
BEGIN
  DECLARE @PID_6524 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6524 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0689/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Request a meeting for 16-1-TGT-20X Geological prognosis & Formation evaluation programrn', NULL, @PID_6524, NULL, @DocSeedUserId, '2026-07-13', '2026-07-15 16:21:39', '2026-07-15 16:21:39', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0690/HV')
BEGIN
  DECLARE @PID_6525 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6525 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0690/HV', 'INCOMING', 'Distributed', N'PVEP - Approval for 2026 Budget conversion from Contingent to Firm for CNV-6PST1 Perforation above Production Packerrn', NULL, @PID_6525, NULL, @DocSeedUserId, '2026-07-13', '2026-07-15 16:22:33', '2026-07-15 16:22:33', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0691/HL')
BEGIN
  DECLARE @PID_6526 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6526 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0691/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of Gauge hanger rental and associated services for TGT-20X well in 2026rn', NULL, @PID_6526, NULL, @DocSeedUserId, '2026-07-13', '2026-07-15 16:23:53', '2026-07-15 16:23:53', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0692/HL')
BEGIN
  DECLARE @PID_6527 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6527 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0692/HL', 'INCOMING', 'Distributed', N'PTTEP ARR for Downhole Tool Rental Servs for TGT-20Xrn', NULL, @PID_6527, NULL, @DocSeedUserId, '2026-07-13', '2026-07-15 16:26:44', '2026-07-15 16:26:44', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0693/HL')
BEGIN
  DECLARE @PID_6528 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6528 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0693/HL', 'INCOMING', 'Distributed', N'PTTEP - Additional  Cost under Provision of Helicopter Servicesrn', NULL, @PID_6528, NULL, @DocSeedUserId, '2026-07-13', '2026-07-15 16:27:50', '2026-07-15 16:27:50', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0694/HL')
BEGIN
  DECLARE @PID_6529 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6529 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0694/HL', 'INCOMING', 'Distributed', N'PTTEP - ARR for QAQC Specialist Servs for TGT-20Xrn', NULL, @PID_6529, NULL, @DocSeedUserId, '2026-07-13', '2026-07-15 16:28:37', '2026-07-15 16:28:37', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0695/HL')
BEGIN
  DECLARE @PID_6530 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PTSC MARINE');
  IF @PID_6530 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0695/HL', 'INCOMING', 'Distributed', N'PTSC Marine - BC3 - Vessel service for TGT-20Xrn', NULL, @PID_6530, NULL, @DocSeedUserId, '2026-07-13', '2026-07-15 16:29:14', '2026-07-15 16:29:14', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0696/HLHV')
BEGIN
  DECLARE @PID_6531 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6531 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0696/HLHV', 'INCOMING', 'Distributed', N'PVEP - Nomination of PVEP''s Secondees at HLHV - Mr. Nguyen Nam Sonrn', NULL, @PID_6531, NULL, @DocSeedUserId, '2026-07-14', '2026-07-16 10:16:31', '2026-07-16 10:16:31', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0697/HV')
BEGIN
  DECLARE @PID_6532 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6532 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0697/HV', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for Completion procedure for Well 09-2-CNV-5Xrn', NULL, @PID_6532, NULL, @DocSeedUserId, '2026-07-14', '2026-07-16 10:58:28', '2026-07-16 10:58:28', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0698/HV')
BEGIN
  DECLARE @PID_6533 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S');
  IF @PID_6533 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0698/HV', 'INCOMING', 'Distributed', N'SOCO Approval to Postpone the CNV 2026 Pressure Survey Campaignrn', NULL, @PID_6533, NULL, @DocSeedUserId, '2026-07-14', '2026-07-16 10:59:29', '2026-07-16 10:59:29', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0699/HL')
BEGIN
  DECLARE @PID_6534 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6534 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0699/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for Casing accessories for TGT-20X in 2026 drillingrn', NULL, @PID_6534, NULL, @DocSeedUserId, '2026-07-14', '2026-07-16 11:00:24', '2026-07-16 11:00:24', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0700/HL')
BEGIN
  DECLARE @PID_6535 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6535 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0700/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of Whipstock and Fishing for TGT-20X in 2026 drillingrn', NULL, @PID_6535, NULL, @DocSeedUserId, '2026-07-14', '2026-07-16 11:04:11', '2026-07-16 11:04:11', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0701/HL')
BEGIN
  DECLARE @PID_6536 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6536 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0701/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of Drill bits for TGT-20X well in 2026 drillingrn', NULL, @PID_6536, NULL, @DocSeedUserId, '2026-07-14', '2026-07-16 11:04:54', '2026-07-16 11:04:54', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0702/HL')
BEGIN
  DECLARE @PID_6537 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PB');
  IF @PID_6537 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0702/HL', 'INCOMING', 'Distributed', N'PVD BH - BC4 - E-line perforating and pipe recovery for TGT-20X well in 2026 Drillling campaignrn', NULL, @PID_6537, NULL, @DocSeedUserId, '2026-07-14', '2026-07-16 11:05:51', '2026-07-16 11:05:51', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0703/HL')
BEGIN
  DECLARE @PID_6538 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P2');
  IF @PID_6538 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0703/HL', 'INCOMING', 'Distributed', N'PV Drilling - LOI for Contract award of  Jack-up drilling rig rental for TGT-20Xrn', NULL, @PID_6538, NULL, @DocSeedUserId, '2026-07-14', '2026-07-16 11:06:30', '2026-07-16 11:06:30', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0704/HLHV')
BEGIN
  DECLARE @PID_6539 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'VSP');
  IF @PID_6539 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0704/HLHV', 'INCOMING', 'Distributed', N'VSP - Thư mời tham dự Hội thảo về an toàn TTAT & BVMTrn', NULL, @PID_6539, NULL, @DocSeedUserId, '2026-07-15', '2026-07-16 11:07:05', '2026-07-16 11:07:05', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0705/HL')
BEGIN
  DECLARE @PID_6540 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PGS');
  IF @PID_6540 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0705/HL', 'INCOMING', 'Distributed', N'PV Gas SE - TGT & HSDT Final Monthly Allocation Statement for February 2026rn', NULL, @PID_6540, NULL, @DocSeedUserId, '2026-07-14', '2026-07-16 11:10:48', '2026-07-16 11:10:48', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0706/HLHV')
BEGIN
  DECLARE @PID_6541 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'VSP');
  IF @PID_6541 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0706/HLHV', 'INCOMING', 'Distributed', N'VSP - Thư mời tham dự Hội thảo về an toàn TTAT & BVMTrn', NULL, @PID_6541, NULL, @DocSeedUserId, '2026-07-15', '2026-07-16 11:13:07', '2026-07-16 11:13:07', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0707/HL')
BEGIN
  DECLARE @PID_6542 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6542 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0707/HL', 'INCOMING', 'Distributed', N'PVEP - Partner audit report for Block 16-1 from 1 Jan to 31 Dec 2025rn', NULL, @PID_6542, NULL, @DocSeedUserId, '2026-07-15', '2026-07-16 11:13:34', '2026-07-16 11:13:34', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0708/HV')
BEGIN
  DECLARE @PID_6543 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6543 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0708/HV', 'INCOMING', 'Distributed', N'PVEP - Partner audit report for Block 09-2 from 1 Jan to 31 Dec 2025rn', NULL, @PID_6543, NULL, @DocSeedUserId, '2026-07-15', '2026-07-16 11:14:39', '2026-07-16 11:14:39', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0709/HL')
BEGIN
  DECLARE @PID_6544 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PB');
  IF @PID_6544 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0709/HL', 'INCOMING', 'Distributed', N'PVD BH - BC2 - Well completion for TGT-20X well in 2026 drilling.pdfrn', NULL, @PID_6544, NULL, @DocSeedUserId, '2026-07-15', '2026-07-16 11:15:26', '2026-07-16 11:15:26', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0710/HL')
BEGIN
  DECLARE @PID_6545 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6545 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0710/HL', 'INCOMING', 'Distributed', N'PVEP - Approval  for ARR for Supply of Wellhead/ Xmas Tree Equip & Servs for TGT-20X Wellrn', NULL, @PID_6545, NULL, @DocSeedUserId, '2026-07-16', '2026-07-16 11:17:19', '2026-07-16 11:17:19', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0711/HLHV')
BEGIN
  DECLARE @PID_6546 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6546 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0711/HLHV', 'INCOMING', 'Distributed', N'PVEP - Xét tặng Kỷ niệm chương Vì sự nghiệp Dầu khírn', NULL, @PID_6546, NULL, @DocSeedUserId, '2026-07-16', '2026-07-16 11:18:08', '2026-07-16 11:18:08', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0712/HL')
BEGIN
  DECLARE @PID_6547 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6547 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0712/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of E-line perforating and pipe recovery for TGT-20Xrn', NULL, @PID_6547, NULL, @DocSeedUserId, '2026-07-16', '2026-07-16 11:18:56', '2026-07-16 11:18:56', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0713/HL')
BEGIN
  DECLARE @PID_6548 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6548 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0713/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of Downhole gauge for TGT-20X wellrn', NULL, @PID_6548, NULL, @DocSeedUserId, '2026-07-16', '2026-07-20 16:31:31', '2026-07-20 16:31:31', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0714/HL')
BEGIN
  DECLARE @PID_6549 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6549 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0714/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of DD, MWD & LWD for TGT-20X wellrn', NULL, @PID_6549, NULL, @DocSeedUserId, '2026-07-16', '2026-07-20 16:32:31', '2026-07-20 16:32:31', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0715/HL')
BEGIN
  DECLARE @PID_6550 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PTSC MARINE');
  IF @PID_6550 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0715/HL', 'INCOMING', 'Distributed', N'PTSC Marine - BC3 - Vessel service for TGT-20X - Fu4rn', NULL, @PID_6550, NULL, @DocSeedUserId, '2026-07-17', '2026-07-20 16:33:51', '2026-07-20 16:33:51', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0716/HLHV')
BEGIN
  DECLARE @PID_6551 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6551 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0716/HLHV', 'INCOMING', 'Distributed', N'PVEP - Invitation to Technical workshop on Field development & O&Mrn', NULL, @PID_6551, NULL, @DocSeedUserId, '2026-07-17', '2026-07-20 16:34:28', '2026-07-20 16:34:28', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0717/HL')
BEGIN
  DECLARE @PID_6552 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'VNHSouth');
  IF @PID_6552 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0717/HL', 'INCOMING', 'Distributed', N'VNH South - Helicopter services No. 989rn', NULL, @PID_6552, NULL, @DocSeedUserId, '2026-07-17', '2026-07-20 16:38:56', '2026-07-20 16:38:56', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0718/HL')
BEGIN
  DECLARE @PID_6553 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'VSP');
  IF @PID_6553 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0718/HL', 'INCOMING', 'Distributed', N'VSP - Installation of V1C Metering system No. 2841rn', NULL, @PID_6553, NULL, @DocSeedUserId, '2026-07-20', '2026-07-20 16:49:56', '2026-07-20 16:49:56', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0719/HV')
BEGIN
  DECLARE @PID_6554 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6554 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0719/HV', 'INCOMING', 'Distributed', N'PTTEP - Approval to Postpone the CNV 2026 Pressure Survey Campaignrn', NULL, @PID_6554, NULL, @DocSeedUserId, '2026-07-20', '2026-07-20 16:50:39', '2026-07-20 16:50:39', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0720/HL')
BEGIN
  DECLARE @PID_6555 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'E7');
  IF @PID_6555 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0720/HL', 'INCOMING', 'Distributed', N'ESS - BC1 - Computing service for running TGT simulation models - Fu 2rn', NULL, @PID_6555, NULL, @DocSeedUserId, '2026-07-20', '2026-07-20 16:51:03', '2026-07-20 16:51:03', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0721/HL')
BEGIN
  DECLARE @PID_6556 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6556 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0721/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of Helicopter services for TGT-20X wellrn', NULL, @PID_6556, NULL, @DocSeedUserId, '2026-07-20', '2026-07-20 16:51:35', '2026-07-20 16:51:35', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0722/HL')
BEGIN
  DECLARE @PID_6557 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6557 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0722/HL', 'INCOMING', 'Distributed', N'PVEP - Technical bid evaluation & issues of Tender for Jacket member & H1-HST gaslift riser repair on TGT-H1 jacketrn', NULL, @PID_6557, NULL, @DocSeedUserId, '2026-07-20', '2026-07-20 16:52:24', '2026-07-20 16:52:24', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0723/HV')
BEGIN
  DECLARE @PID_6558 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'T2');
  IF @PID_6558 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0723/HV', 'INCOMING', 'Distributed', N'TDH - BC1 - Auto sampling system of CPP3 - Fu1rn', NULL, @PID_6558, NULL, @DocSeedUserId, '2026-07-20', '2026-07-20 16:53:59', '2026-07-20 16:53:59', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0724/HLHV')
BEGIN
  DECLARE @PID_6559 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6559 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0724/HLHV', 'INCOMING', 'Distributed', N'PVEP - Báo cáo tình hình nộp phí bảo vệ môi trường nước khai thác thảirn', NULL, @PID_6559, NULL, @DocSeedUserId, '2026-07-20', '2026-07-20 16:54:27', '2026-07-20 16:54:27', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0725/HL')
BEGIN
  DECLARE @PID_6560 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6560 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0725/HL', 'INCOMING', 'Distributed', N'PTTEP - CS of Measurement  Allocation Support Activitiesrn', NULL, @PID_6560, NULL, @DocSeedUserId, '2026-07-20', '2026-07-22 13:13:36', '2026-07-22 13:13:36', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0726/HL')
BEGIN
  DECLARE @PID_6561 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6561 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0726/HL', 'INCOMING', 'Draft', N'PTTEP - ARR for Supply Base Rental  Waste Disposal Treatment for TGT-20Xrn', NULL, @PID_6561, NULL, @DocSeedUserId, '2026-07-20', NULL, '2026-07-22 13:14:05', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0727/HL')
BEGIN
  DECLARE @PID_6562 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6562 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0727/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Jackup drilling rig rental for TGT-20X wellrn', NULL, @PID_6562, NULL, @DocSeedUserId, '2026-07-21', '2026-07-23 10:56:13', '2026-07-23 10:56:13', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0728/HL')
BEGIN
  DECLARE @PID_6563 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PT3');
  IF @PID_6563 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0728/HL', 'INCOMING', 'Distributed', N'PVD Training - BC2 - Manpower services for TGT-20X wellrn', NULL, @PID_6563, NULL, @DocSeedUserId, '2026-07-21', '2026-07-23 10:57:10', '2026-07-23 10:57:10', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0729/HLHV')
BEGIN
  DECLARE @PID_6564 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6564 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0729/HLHV', 'INCOMING', 'Distributed', N'PVEP - Thư mời tham dự họp trao đổi chuyên môn ATSKMTrn', NULL, @PID_6564, NULL, @DocSeedUserId, '2026-07-21', '2026-07-23 10:57:39', '2026-07-23 10:57:39', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0730/HL')
BEGIN
  DECLARE @PID_6565 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6565 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0730/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Nhận xét đánh giá của Tổ chuyên viên về Báo cáo tài nguyên, trữ lượng dầu khí mỏ Tê Giác Trắng cập nhật khu vực giếng khoan TGT-18Xrn', NULL, @PID_6565, NULL, @DocSeedUserId, '2026-07-21', '2026-07-23 10:58:34', '2026-07-23 10:58:34', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0731/HL')
BEGIN
  DECLARE @PID_6566 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PB');
  IF @PID_6566 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0731/HL', 'INCOMING', 'Distributed', N'PVD BH - Coil tubing services in 2026 rn', NULL, @PID_6566, NULL, @DocSeedUserId, '2026-07-21', '2026-07-23 11:02:38', '2026-07-23 11:02:38', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0732/HL')
BEGIN
  DECLARE @PID_6567 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6567 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0732/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Whipstock and Fishing for TGT-20Xrn', NULL, @PID_6567, NULL, @DocSeedUserId, '2026-07-22', '2026-07-23 11:04:33', '2026-07-23 11:04:33', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0733/HL')
BEGIN
  DECLARE @PID_6568 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6568 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0733/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for 2026 Budget conversion from Contingent to Firm No. 1798rn', NULL, @PID_6568, NULL, @DocSeedUserId, '2026-07-22', '2026-07-23 11:05:23', '2026-07-23 11:05:23', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0734/HL')
BEGIN
  DECLARE @PID_6569 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6569 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0734/HL', 'INCOMING', 'Distributed', N'PVEP - Approval request for ARR of Downhole tool rental for TGT-20X wellrn', NULL, @PID_6569, NULL, @DocSeedUserId, '2026-07-22', '2026-07-23 11:06:09', '2026-07-23 11:06:09', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0735/HL')
BEGIN
  DECLARE @PID_6570 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6570 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0735/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of Supply base rental & associated services and waster disposal treatmentrn', NULL, @PID_6570, NULL, @DocSeedUserId, '2026-07-22', '2026-07-23 13:48:06', '2026-07-23 13:48:06', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0736/HL')
BEGIN
  DECLARE @PID_6571 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6571 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0736/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Petroleum Reserves Committee meeting on TGT RAR Updatedrn', NULL, @PID_6571, NULL, @DocSeedUserId, '2026-07-22', '2026-07-23 13:48:49', '2026-07-23 13:48:49', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0737/HL')
BEGIN
  DECLARE @PID_6572 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6572 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0737/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Casing accessories for TGT-20Xrn', NULL, @PID_6572, NULL, @DocSeedUserId, '2026-07-22', '2026-07-23 13:49:41', '2026-07-23 13:49:41', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0738/HL')
BEGIN
  DECLARE @PID_6573 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6573 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0738/HL', 'INCOMING', 'Distributed', N'PVEP - Hydrocarbon initially in place & reserves assessment report of TGT Field updated with TGT-18X in H5W fault blockrn', NULL, @PID_6573, NULL, @DocSeedUserId, '2026-07-22', '2026-07-23 13:51:40', '2026-07-23 13:51:40', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0739/HL')
BEGIN
  DECLARE @PID_6574 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'SLB');
  IF @PID_6574 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0739/HL', 'INCOMING', 'Distributed', N'SLB - BC1 - Coil tubing Fu 2rn', NULL, @PID_6574, NULL, @DocSeedUserId, '2026-07-22', '2026-07-23 13:55:26', '2026-07-23 13:55:26', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0740/HL')
BEGIN
  DECLARE @PID_6575 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6575 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0740/HL', 'INCOMING', 'Distributed', N'PVEP - Approval request for ARR of Quality Assurance and Quality Control specialist for TGT-20X wellrn', NULL, @PID_6575, NULL, @DocSeedUserId, '2026-07-22', '2026-07-23 13:56:02', '2026-07-23 13:56:02', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0741/HL')
BEGIN
  DECLARE @PID_6576 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PTSC MARINE');
  IF @PID_6576 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0741/HL', 'INCOMING', 'Distributed', N'PTSC Marine - Replacement of MV Vung Tau 03 rn', NULL, @PID_6576, NULL, @DocSeedUserId, '2026-07-22', '2026-07-23 13:57:26', '2026-07-23 13:57:26', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0742/HL')
BEGIN
  DECLARE @PID_6577 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PTSC MARINE');
  IF @PID_6577 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0742/HL', 'INCOMING', 'Distributed', N'PTSC Marine - BC3 - Vessel service for TGT-20X - Fu 6rn', NULL, @PID_6577, NULL, @DocSeedUserId, '2026-07-22', '2026-07-23 13:57:56', '2026-07-23 13:57:56', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0743/HL')
BEGIN
  DECLARE @PID_6578 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S');
  IF @PID_6578 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0743/HL', 'INCOMING', 'Distributed', N'SOCO	Approval for ARR for Supply Base Rental & Waste Disposal Treatment Servs for TGT-20X Wellrn', NULL, @PID_6578, NULL, @DocSeedUserId, '2026-07-23', '2026-07-23 13:59:07', '2026-07-23 13:59:07', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0744/HL')
BEGIN
  DECLARE @PID_6579 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'O5');
  IF @PID_6579 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0744/HL', 'INCOMING', 'Distributed', N'OPECO	Approval for ARR for Supply Base Rental & Waste Disposal Treatment Servs for TGT-20X Wellrn', NULL, @PID_6579, NULL, @DocSeedUserId, '2026-07-23', '2026-07-23 13:59:29', '2026-07-23 13:59:29', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0745/HL')
BEGIN
  DECLARE @PID_6580 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S');
  IF @PID_6580 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0745/HL', 'INCOMING', 'Distributed', N'SOCO	Approval for CS of Measurement & Allocation Support Activitiesrn', NULL, @PID_6580, NULL, @DocSeedUserId, '2026-07-23', '2026-07-23 13:59:49', '2026-07-23 13:59:49', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0746/HL')
BEGIN
  DECLARE @PID_6581 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'O5');
  IF @PID_6581 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0746/HL', 'INCOMING', 'Distributed', N'OPECO	Approval for CS of Measurement & Allocation Support Activitiesrn', NULL, @PID_6581, NULL, @DocSeedUserId, '2026-07-23', '2026-07-23 14:00:15', '2026-07-23 14:00:15', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0747/HV')
BEGIN
  DECLARE @PID_6582 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6582 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0747/HV', 'INCOMING', 'Distributed', N'PTTEP	Approval for CNV-5X Well Completion & Well Testing Proposalrn', NULL, @PID_6582, NULL, @DocSeedUserId, '2026-07-23', '2026-07-23 14:01:20', '2026-07-23 14:01:20', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0748/HV')
BEGIN
  DECLARE @PID_6583 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6583 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0748/HV', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for CNV-5X well completion & well testing proposalrn', NULL, @PID_6583, NULL, @DocSeedUserId, '2026-07-23', '2026-07-23 14:01:46', '2026-07-23 14:01:46', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0749/HLHV')
BEGIN
  DECLARE @PID_6584 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6584 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0749/HLHV', 'INCOMING', 'Distributed', N'PVEP - Tổ chức ngày làm thêm để ủng hộ Quỹ tương trợ Dầu khírn', NULL, @PID_6584, NULL, @DocSeedUserId, '2026-07-23', '2026-07-29 09:32:57', '2026-07-29 09:32:57', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0750/HL')
BEGIN
  DECLARE @PID_6585 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'Vietubes');
  IF @PID_6585 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0750/HL', 'INCOMING', 'Distributed', N'Viettubes - Fabrication and repair of casing, pup joints, crossoversrn', NULL, @PID_6585, NULL, @DocSeedUserId, '2026-07-23', '2026-07-29 09:33:48', '2026-07-29 09:33:48', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0751/HL')
BEGIN
  DECLARE @PID_6586 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6586 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0751/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for Block 16-1 PC 2026 Budget conversion from Contingent to Firmrn', NULL, @PID_6586, NULL, @DocSeedUserId, '2026-07-24', '2026-07-29 13:33:02', '2026-07-29 13:33:02', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0752/HLHV')
BEGIN
  DECLARE @PID_6587 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6587 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0752/HLHV', 'INCOMING', 'Distributed', N'PVEP - Cho ý kiến Dự thảo hồ sơ Luật tài nguyên, môi trường biển và hải đảorn', NULL, @PID_6587, NULL, @DocSeedUserId, '2026-07-24', '2026-07-29 13:34:19', '2026-07-29 13:34:19', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0753/HV')
BEGIN
  DECLARE @PID_6588 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6588 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0753/HV', 'INCOMING', 'Distributed', N'PVEP - Approval for CNV-5X well completion & well testing proposalrn', NULL, @PID_6588, NULL, @DocSeedUserId, '2026-07-24', '2026-07-29 13:37:06', '2026-07-29 13:37:06', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0754/HL')
BEGIN
  DECLARE @PID_6589 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6589 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0754/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Mud logging for TGT-20X wellrn', NULL, @PID_6589, NULL, @DocSeedUserId, '2026-07-24', '2026-07-29 13:38:29', '2026-07-29 13:38:29', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0755/HL')
BEGIN
  DECLARE @PID_6590 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6590 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0755/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Downhole gauge for TGT-20X wellrn', NULL, @PID_6590, NULL, @DocSeedUserId, '2026-07-24', '2026-07-29 13:39:13', '2026-07-29 13:39:13', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0756/HL')
BEGIN
  DECLARE @PID_6591 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6591 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0756/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Wellhead, Xmas tree for TGT-20X wellrn', NULL, @PID_6591, NULL, @DocSeedUserId, '2026-07-24', '2026-07-29 13:40:04', '2026-07-29 13:40:04', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0757/HLHV')
BEGIN
  DECLARE @PID_6592 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6592 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0757/HLHV', 'INCOMING', 'Distributed', N'PVEP - Lấy ý kiến dự thảo Thông tư xây dựng bản đồ nhạy cảm môi trường tràn dầu rn', NULL, @PID_6592, NULL, @DocSeedUserId, '2026-07-24', '2026-07-29 13:41:29', '2026-07-29 13:41:29', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0758/HLHV')
BEGIN
  DECLARE @PID_6593 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P');
  IF @PID_6593 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0758/HLHV', 'INCOMING', 'Distributed', N'PV Gas - Giới thiệu chức danh và chữ ký Ông Huỳnh Quang Hảirn', NULL, @PID_6593, NULL, @DocSeedUserId, '2026-07-24', '2026-07-29 13:42:41', '2026-07-29 13:42:41', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0759/HL')
BEGIN
  DECLARE @PID_6594 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6594 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0759/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for Geological prognosis and formation evaluation for TGT-20X wellrn', NULL, @PID_6594, NULL, @DocSeedUserId, '2026-07-24', '2026-07-29 13:44:54', '2026-07-29 13:44:54', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0760/HL')
BEGIN
  DECLARE @PID_6595 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6595 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0760/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Liner hanger for TGT-20X wellrn', NULL, @PID_6595, NULL, @DocSeedUserId, '2026-07-24', '2026-07-29 13:49:25', '2026-07-29 13:49:25', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0761/HV')
BEGIN
  DECLARE @PID_6596 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6596 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0761/HV', 'INCOMING', 'Distributed', N'Petrovietnam - Approval to postpone the CNV 2026 Pressure survey campaignrn', NULL, @PID_6596, NULL, @DocSeedUserId, '2026-07-25', '2026-07-29 13:50:28', '2026-07-29 13:50:28', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0762/HL')
BEGIN
  DECLARE @PID_6597 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6597 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0762/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of DD, MWD, LWD for TGT-20X wellrn', NULL, @PID_6597, NULL, @DocSeedUserId, '2026-07-25', '2026-07-29 13:52:04', '2026-07-29 13:52:04', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0763/HL')
BEGIN
  DECLARE @PID_6598 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6598 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0763/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Drill bits for TGT-20X wellrn', NULL, @PID_6598, NULL, @DocSeedUserId, '2026-07-25', '2026-07-29 13:52:46', '2026-07-29 13:52:46', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0764/HL')
BEGIN
  DECLARE @PID_6599 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'SLB');
  IF @PID_6599 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0764/HL', 'INCOMING', 'Distributed', N'SLB - Geophysical interpretation petrel softwarern', NULL, @PID_6599, NULL, @DocSeedUserId, '2026-07-27', '2026-07-29 13:53:34', '2026-07-29 13:53:34', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0765/HL')
BEGIN
  DECLARE @PID_6600 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6600 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0765/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Gauge hanger for TGT-20X wellrn', NULL, @PID_6600, NULL, @DocSeedUserId, '2026-07-27', '2026-07-29 13:54:06', '2026-07-29 13:54:06', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0766/HLHV')
BEGIN
  DECLARE @PID_6601 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6601 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0766/HLHV', 'INCOMING', 'Distributed', N'Petrovietnam - TGT and CNV Abandonment plan updates in 2026rn', NULL, @PID_6601, NULL, @DocSeedUserId, '2026-07-27', '2026-07-29 13:54:48', '2026-07-29 13:54:48', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0767/HL')
BEGIN
  DECLARE @PID_6602 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PT3');
  IF @PID_6602 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0767/HL', 'INCOMING', 'Distributed', N'PVD Training - Manpower services for TGT-20X wellrn', NULL, @PID_6602, NULL, @DocSeedUserId, '2026-07-27', '2026-07-29 14:00:25', '2026-07-29 14:00:25', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0768/HLHV')
BEGIN
  DECLARE @PID_6603 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6603 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0768/HLHV', 'INCOMING', 'Distributed', N'PVEP - Đóng góp ý kiến Dự thảo thông tư sửa đổi bổ sung Thông tư số 40-2018-TT-BCTrn', NULL, @PID_6603, NULL, @DocSeedUserId, '2026-07-28', '2026-07-29 14:00:58', '2026-07-29 14:00:58', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0769/HL')
BEGIN
  DECLARE @PID_6604 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S2');
  IF @PID_6604 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0769/HL', 'INCOMING', 'Distributed', N'SEN - BC1 - Environmental registration for H3 wellrn', NULL, @PID_6604, NULL, @DocSeedUserId, '2026-07-28', '2026-07-29 14:01:41', '2026-07-29 14:01:41', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0770/HL')
BEGIN
  DECLARE @PID_6605 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6605 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0770/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Helicopter services for TGT-20X wellrn', NULL, @PID_6605, NULL, @DocSeedUserId, '2026-07-28', '2026-07-29 14:02:11', '2026-07-29 14:02:11', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0771/HL')
BEGIN
  DECLARE @PID_6606 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'VSP');
  IF @PID_6606 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0771/HL', 'INCOMING', 'Distributed', N'VSP - The celebration of the 15th Anniversary of TGT OMrn', NULL, @PID_6606, NULL, @DocSeedUserId, '2026-07-28', '2026-07-29 14:02:58', '2026-07-29 14:02:58', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0772/HV')
BEGIN
  DECLARE @PID_6607 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'VSP');
  IF @PID_6607 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0772/HV', 'INCOMING', 'Distributed', N'VSP - The Celebration of the milestone of 18th Anniversary of CNV OMrn', NULL, @PID_6607, NULL, @DocSeedUserId, '2026-07-28', '2026-07-29 14:03:25', '2026-07-29 14:03:25', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0773/HV')
BEGIN
  DECLARE @PID_6608 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PB');
  IF @PID_6608 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0773/HV', 'INCOMING', 'Distributed', N'PVD BH - Pending invoice for Coiled tubing & cement above Packer for CNV-6PST1rn', NULL, @PID_6608, NULL, @DocSeedUserId, '2026-07-28', '2026-07-29 14:03:57', '2026-07-29 14:03:57', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0774/HL')
BEGIN
  DECLARE @PID_6609 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'C5');
  IF @PID_6609 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0774/HL', 'INCOMING', 'Distributed', N'Chiron - BC2 - Spare part for HPU, Production & Electrical systemrn', NULL, @PID_6609, NULL, @DocSeedUserId, '2026-07-29', '2026-07-29 14:04:33', '2026-07-29 14:04:33', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0775/HL')
BEGIN
  DECLARE @PID_6610 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S');
  IF @PID_6610 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0775/HL', 'INCOMING', 'Distributed', N'SOCO	Approval for ARR of Tubular Handling Equip and Servs for TGT-20X Wellrn', NULL, @PID_6610, NULL, @DocSeedUserId, '2026-07-29', '2026-07-29 14:05:00', '2026-07-29 14:05:00', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0776/HL')
BEGIN
  DECLARE @PID_6611 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'O5');
  IF @PID_6611 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0776/HL', 'INCOMING', 'Distributed', N'OPECO	Approval for ARR of Tubular Handling Equip and Servs for TGT-20X Wellrn', NULL, @PID_6611, NULL, @DocSeedUserId, '2026-07-29', '2026-07-29 14:05:20', '2026-07-29 14:05:20', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0777/HL')
BEGIN
  DECLARE @PID_6612 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S');
  IF @PID_6612 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0777/HL', 'INCOMING', 'Distributed', N'SOCO	Plug & Abandoment Programme for Well 16-1-TGT-H4-12Prn', NULL, @PID_6612, NULL, @DocSeedUserId, '2026-07-29', '2026-07-29 14:05:40', '2026-07-29 14:05:40', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0778/HL')
BEGIN
  DECLARE @PID_6613 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'O5');
  IF @PID_6613 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0778/HL', 'INCOMING', 'Distributed', N'OPECO	Plug & Abandoment Programme for Well 16-1-TGT-H4-12Prn', NULL, @PID_6613, NULL, @DocSeedUserId, '2026-07-29', '2026-07-29 14:06:01', '2026-07-29 14:06:01', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0779/HL')
BEGIN
  DECLARE @PID_6614 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6614 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0779/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for CS of Measurement & Allocation support for TGT, HSD, HSTrn', NULL, @PID_6614, NULL, @DocSeedUserId, '2026-07-29', '2026-07-31 14:47:46', '2026-07-31 14:47:46', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0780/HL')
BEGIN
  DECLARE @PID_6615 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S2');
  IF @PID_6615 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0780/HL', 'INCOMING', 'Distributed', N'SEN - BC1 - Environmental registration for H3 well - Fu1rn', NULL, @PID_6615, NULL, @DocSeedUserId, '2026-07-29', '2026-07-31 14:48:33', '2026-07-31 14:48:33', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0781/HL')
BEGIN
  DECLARE @PID_6616 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6616 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0781/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of Downhole tool rental for TGT-20X wellrn', NULL, @PID_6616, NULL, @DocSeedUserId, '2026-07-30', '2026-07-31 14:49:19', '2026-07-31 14:49:19', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0782/HLHV')
BEGIN
  DECLARE @PID_6617 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6617 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0782/HLHV', 'INCOMING', 'Distributed', N'PVEP - Rà soát cập nhật triển khai đầu tư, giải ngân 2026 và kế hoạch năm 2027rn', NULL, @PID_6617, NULL, @DocSeedUserId, '2026-07-30', '2026-07-31 14:50:16', '2026-07-31 14:50:16', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0783/HL')
BEGIN
  DECLARE @PID_6618 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'A5');
  IF @PID_6618 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0783/HL', 'INCOMING', 'Distributed', N'ABL - Rig inspection for TGT-20X wellrn', NULL, @PID_6618, NULL, @DocSeedUserId, '2026-07-30', '2026-07-31 14:52:11', '2026-07-31 14:52:11', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0784/HL')
BEGIN
  DECLARE @PID_6619 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6619 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0784/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for CS for Extension of TGT facilities O&M Agreementrn', NULL, @PID_6619, NULL, @DocSeedUserId, '2026-07-30', '2026-07-31 14:52:48', '2026-07-31 14:52:48', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0785/HL')
BEGIN
  DECLARE @PID_6620 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6620 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0785/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for CS of Bare boat charter for supply of a leased FPSOrn', NULL, @PID_6620, NULL, @DocSeedUserId, '2026-07-30', '2026-07-31 14:53:38', '2026-07-31 14:53:38', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0786/HL')
BEGIN
  DECLARE @PID_6621 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'M');
  IF @PID_6621 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0786/HL', 'INCOMING', 'Distributed', N'Murphy - Temporary loan of 11-3-4 inch water bushing and casing jointrn', NULL, @PID_6621, NULL, @DocSeedUserId, '2026-07-30', '2026-07-31 14:54:28', '2026-07-31 14:54:28', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0787/HL')
BEGIN
  DECLARE @PID_6622 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'TLJOC');
  IF @PID_6622 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0787/HL', 'INCOMING', 'Distributed', N'TLJOC - Notice of Office relocation rn', NULL, @PID_6622, NULL, @DocSeedUserId, '2026-07-31', '2026-07-31 14:54:58', '2026-07-31 14:54:58', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0788/HLHV')
BEGIN
  DECLARE @PID_6623 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'GP');
  IF @PID_6623 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0788/HLHV', 'INCOMING', 'Distributed', N'Gia phat - Drill bits for 2025 - 2026 TGT & CNV Drilling campaignsrn', NULL, @PID_6623, NULL, @DocSeedUserId, '2026-07-31', '2026-08-04 15:12:26', '2026-08-04 15:12:26', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0789/HL')
BEGIN
  DECLARE @PID_6624 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6624 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0789/HL', 'INCOMING', 'Distributed', N'PVEP - Invitation to TGT RFDP 2026 workshoprn', NULL, @PID_6624, NULL, @DocSeedUserId, '2026-07-31', '2026-08-04 15:14:07', '2026-08-04 15:14:07', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0790/HL')
BEGIN
  DECLARE @PID_6625 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6625 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0790/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of Well completion services for TGT-20X well in 2026 Drilling campaignrn', NULL, @PID_6625, NULL, @DocSeedUserId, '2026-07-31', '2026-08-04 15:15:12', '2026-08-04 15:15:12', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0791/HL')
BEGIN
  DECLARE @PID_6626 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'J');
  IF @PID_6626 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0791/HL', 'INCOMING', 'Distributed', N'JVPC - Proposal to purchase one 8-1-2 inch drill bit out of three drill bits borrowedrn', NULL, @PID_6626, NULL, @DocSeedUserId, '2026-07-31', '2026-08-04 15:15:53', '2026-08-04 15:15:53', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0792/HL')
BEGIN
  DECLARE @PID_6627 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6627 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0792/HL', 'INCOMING', 'Distributed', N'PTTEP - Invitation to TGT RFDP 2026 Workshoprn', NULL, @PID_6627, NULL, @DocSeedUserId, '2026-08-03', '2026-08-04 15:21:31', '2026-08-04 15:21:31', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0793/HL')
BEGIN
  DECLARE @PID_6628 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6628 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0793/HL', 'INCOMING', 'Distributed', N'PTTEP - ARR of Tubular Handling Equip and Servs for TGT-20Xrn', NULL, @PID_6628, NULL, @DocSeedUserId, '2026-08-03', '2026-08-04 15:22:54', '2026-08-04 15:22:54', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0794/HL')
BEGIN
  DECLARE @PID_6629 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6629 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0794/HL', 'INCOMING', 'Distributed', N'PTTEP - Plug  Abandonment Programme for well 16-1 TGT-H4-12Prn', NULL, @PID_6629, NULL, @DocSeedUserId, '2026-08-03', '2026-08-04 15:32:55', '2026-08-04 15:32:55', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0795/HL')
BEGIN
  DECLARE @PID_6630 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6630 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0795/HL', 'INCOMING', 'Distributed', N'PTTEP - Approval of Rebidding the Provision of Jacket Member H1-HST Gaslift Riser Repair Servsrn', NULL, @PID_6630, NULL, @DocSeedUserId, '2026-08-03', '2026-08-04 15:33:28', '2026-08-04 15:33:28', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0796/HL')
BEGIN
  DECLARE @PID_6631 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'A5');
  IF @PID_6631 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0796/HL', 'INCOMING', 'Distributed', N'ABL - BC2 - Rig inspection for TGT-20X wellrn', NULL, @PID_6631, NULL, @DocSeedUserId, '2026-08-03', '2026-08-04 15:33:52', '2026-08-04 15:33:52', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0797/HL')
BEGIN
  DECLARE @PID_6632 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'POH');
  IF @PID_6632 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0797/HL', 'INCOMING', 'Distributed', N'PV Oil - Breakdown calculation for TGT-506 dated 30 Jun 2026rn', NULL, @PID_6632, NULL, @DocSeedUserId, '2026-08-03', '2026-08-04 15:34:24', '2026-08-04 15:34:24', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0798/HV')
BEGIN
  DECLARE @PID_6633 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S');
  IF @PID_6633 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0798/HV', 'INCOMING', 'Distributed', N'SOCO 	Approval CS for Provision of Chemical and Acidizing Services for CNV-8P Wellrn', NULL, @PID_6633, NULL, @DocSeedUserId, '2026-08-04', '2026-08-04 15:35:47', '2026-08-04 15:35:47', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0799/HV')
BEGIN
  DECLARE @PID_6634 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S');
  IF @PID_6634 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0799/HV', 'INCOMING', 'Distributed', N'SOCO 	Comments on CNV-6PST1 Perforation above Production Packer Proposalrn', NULL, @PID_6634, NULL, @DocSeedUserId, '2026-08-04', '2026-08-05 11:25:31', '2026-08-05 11:25:31', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0800/HL')
BEGIN
  DECLARE @PID_6635 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6635 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0800/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Supply base rental and waste disposal treatment for TGT-20Xrn', NULL, @PID_6635, NULL, @DocSeedUserId, '2026-08-04', '2026-08-05 11:28:08', '2026-08-05 11:28:08', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0801/HL')
BEGIN
  DECLARE @PID_6636 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S');
  IF @PID_6636 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0801/HL', 'INCOMING', 'Distributed', N'SOCO	Approval of ARR for the Supply of MGO for TGT-20X Wellrn', NULL, @PID_6636, NULL, @DocSeedUserId, '2026-08-04', '2026-08-05 11:29:04', '2026-08-05 11:29:04', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0802/HL')
BEGIN
  DECLARE @PID_6637 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'O5');
  IF @PID_6637 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0802/HL', 'INCOMING', 'Distributed', N'OPECO	Approval of ARR for the Supply of MGO for TGT-20X Wellrn', NULL, @PID_6637, NULL, @DocSeedUserId, '2026-08-04', '2026-08-05 11:29:28', '2026-08-05 11:29:28', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0803/HL')
BEGIN
  DECLARE @PID_6638 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S');
  IF @PID_6638 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0803/HL', 'INCOMING', 'Distributed', N'SOCO	Approval of ARR for Vessel Services for TGT-20X Wellrn', NULL, @PID_6638, NULL, @DocSeedUserId, '2026-08-04', '2026-08-05 11:29:51', '2026-08-05 11:29:51', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0804/HL')
BEGIN
  DECLARE @PID_6639 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'O5');
  IF @PID_6639 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0804/HL', 'INCOMING', 'Distributed', N'OPECO	Approval of ARR for Vessel Services for TGT-20X Wellrn', NULL, @PID_6639, NULL, @DocSeedUserId, '2026-08-04', '2026-08-05 11:30:13', '2026-08-05 11:30:13', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0805/HV')
BEGIN
  DECLARE @PID_6640 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6640 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0805/HV', 'INCOMING', 'Distributed', N'PVEP - CNV crude oil term sales for September 2026rn', NULL, @PID_6640, NULL, @DocSeedUserId, '2026-08-05', '2026-08-05 11:30:40', '2026-08-05 11:30:40', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0806/HL')
BEGIN
  DECLARE @PID_6641 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PGS');
  IF @PID_6641 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0806/HL', 'INCOMING', 'Distributed', N'PV Gas SE - TGT & HSDT Final Monthly Allocation Statement from March 2026 - 5 Augrn', NULL, @PID_6641, NULL, @DocSeedUserId, '2026-08-05', '2026-08-05 11:31:22', '2026-08-05 11:31:22', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0807/HL')
BEGIN
  DECLARE @PID_6642 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6642 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0807/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of QA QC for TGT-20X well rn', NULL, @PID_6642, NULL, @DocSeedUserId, '2026-08-05', '2026-08-11 14:57:27', '2026-08-11 14:57:27', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0808/HL')
BEGIN
  DECLARE @PID_6643 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6643 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0808/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Downhole tool rental for TGT-20X well rn', NULL, @PID_6643, NULL, @DocSeedUserId, '2026-08-05', '2026-08-11 14:58:28', '2026-08-11 14:58:28', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0809/HV')
BEGIN
  DECLARE @PID_6644 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'VSP');
  IF @PID_6644 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0809/HV', 'INCOMING', 'Distributed', N'VSP - Review and confirmation of design basis for CNV auto sampling systemrn', NULL, @PID_6644, NULL, @DocSeedUserId, '2026-08-06', '2026-08-11 14:59:21', '2026-08-11 14:59:21', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0810/HL')
BEGIN
  DECLARE @PID_6645 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'POH');
  IF @PID_6645 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0810/HL', 'INCOMING', 'Distributed', N'PV Oil - Breakdown calculation for TGT-507 dated 25 Jul 2026rn', NULL, @PID_6645, NULL, @DocSeedUserId, '2026-08-06', '2026-08-11 14:59:48', '2026-08-11 14:59:48', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0811/HLHV')
BEGIN
  DECLARE @PID_6646 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6646 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0811/HLHV', 'INCOMING', 'Distributed', N'PVEP - Khai báo nộp phí bảo vệ môi trường nước khai thác thải hàng quýrn', NULL, @PID_6646, NULL, @DocSeedUserId, '2026-08-06', '2026-08-11 15:00:21', '2026-08-11 15:00:21', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0812/HL')
BEGIN
  DECLARE @PID_6647 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'SLB');
  IF @PID_6647 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0812/HL', 'INCOMING', 'Distributed', N'SLB - BC2 - Coil tubing Fu 2 rn', NULL, @PID_6647, NULL, @DocSeedUserId, '2026-08-06', '2026-08-11 15:01:08', '2026-08-11 15:01:08', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0813/HL')
BEGIN
  DECLARE @PID_6648 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6648 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0813/HL', 'INCOMING', 'Distributed', N'PVEP - Approval request for ARR of Vessel services for TGT-20X wellrn', NULL, @PID_6648, NULL, @DocSeedUserId, '2026-08-07', '2026-08-11 15:01:40', '2026-08-11 15:01:40', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0814/HL')
BEGIN
  DECLARE @PID_6649 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6649 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0814/HL', 'INCOMING', 'Distributed', N'PVEP - Approval for ARR of MGO supply for TGT-20X wellrn', NULL, @PID_6649, NULL, @DocSeedUserId, '2026-08-07', '2026-08-11 15:02:35', '2026-08-11 15:02:35', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0815/HL')
BEGIN
  DECLARE @PID_6650 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6650 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0815/HL', 'INCOMING', 'Distributed', N'PTTEP - Approval for CS for Chemical and Acidizing Servicesrn', NULL, @PID_6650, NULL, @DocSeedUserId, '2026-08-07', '2026-08-11 15:03:27', '2026-08-11 15:03:27', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0816/HL')
BEGIN
  DECLARE @PID_6651 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S');
  IF @PID_6651 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0816/HL', 'INCOMING', 'Distributed', N'SOCO	Approval of Upgrade VSAT IP under Contract VSAT IP & VSAT Leased Line for TGT-H1-WHPrn', NULL, @PID_6651, NULL, @DocSeedUserId, '2026-08-07', '2026-08-11 15:42:38', '2026-08-11 15:42:38', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0817/HL')
BEGIN
  DECLARE @PID_6652 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'O5');
  IF @PID_6652 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0817/HL', 'INCOMING', 'Distributed', N'OPECO	Approval of Upgrade VSAT IP under Contract VSAT IP & VSAT Leased Line for TGT-H1-WHPrn', NULL, @PID_6652, NULL, @DocSeedUserId, '2026-08-07', '2026-08-11 15:43:12', '2026-08-11 15:43:12', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0818/HL')
BEGIN
  DECLARE @PID_6653 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S');
  IF @PID_6653 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0818/HL', 'INCOMING', 'Distributed', N'SOCO	Approval of ARR for Professional Manpower Services for TGT-20X Wellrn', NULL, @PID_6653, NULL, @DocSeedUserId, '2026-08-11', '2026-08-11 15:43:40', '2026-08-11 15:43:40', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0819/HL')
BEGIN
  DECLARE @PID_6654 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'O5');
  IF @PID_6654 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0819/HL', 'INCOMING', 'Distributed', N'OPECO	Approval of ARR for Professional Manpower Services for TGT-20X Wellrn', NULL, @PID_6654, NULL, @DocSeedUserId, '2026-08-07', '2026-08-11 15:44:06', '2026-08-11 15:44:06', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0820/HL')
BEGIN
  DECLARE @PID_6655 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S');
  IF @PID_6655 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0820/HL', 'INCOMING', 'Distributed', N'SOCO	Approval of ARR for Oil, Gas, Water, Debris Sampling and Analysis Servicesrn', NULL, @PID_6655, NULL, @DocSeedUserId, '2026-08-07', '2026-08-11 15:49:01', '2026-08-11 15:49:01', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0821/HL')
BEGIN
  DECLARE @PID_6656 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'O5');
  IF @PID_6656 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0821/HL', 'INCOMING', 'Distributed', N'OPECO	Approval of ARR for Oil, Gas, Water, Debris Sampling and Analysis Servicesrn', NULL, @PID_6656, NULL, @DocSeedUserId, '2026-08-07', '2026-08-11 15:49:36', '2026-08-11 15:49:36', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0822/HL')
BEGIN
  DECLARE @PID_6657 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6657 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0822/HL', 'INCOMING', 'Distributed', N'PVEP - Plug & abandonment program for Well 16-1-TGT-H4-12Prn', NULL, @PID_6657, NULL, @DocSeedUserId, '2026-08-10', '2026-08-11 15:50:30', '2026-08-11 15:50:30', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0823/HL')
BEGIN
  DECLARE @PID_6658 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'VSP');
  IF @PID_6658 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0823/HL', 'INCOMING', 'Distributed', N'VSP - Thư mời Hội thảo công tác tối ưu hệ thống vận hành khai thác và kết nối mỏ dầu khírn', NULL, @PID_6658, NULL, @DocSeedUserId, '2026-08-10', '2026-08-11 15:53:40', '2026-08-11 15:53:40', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0824/HL')
BEGIN
  DECLARE @PID_6659 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6659 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0824/HL', 'INCOMING', 'Distributed', N'PTTEP - ARR for Oil Gas Water Debris Sampling and Analysis Servicesrn', NULL, @PID_6659, NULL, @DocSeedUserId, '2026-08-10', '2026-08-11 15:54:22', '2026-08-11 15:54:22', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0825/HL')
BEGIN
  DECLARE @PID_6660 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6660 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0825/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for Revised CS of TGT Facilities Operation and Maintenance Agreementrn', NULL, @PID_6660, NULL, @DocSeedUserId, '2026-08-10', '2026-08-11 15:54:59', '2026-08-11 15:54:59', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0826/HL')
BEGIN
  DECLARE @PID_6661 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6661 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0826/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for Revised CS of Measurement & Allocation support activities for TGT, HSD, HSTrn', NULL, @PID_6661, NULL, @DocSeedUserId, '2026-08-10', '2026-08-11 15:55:44', '2026-08-11 15:55:44', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0827/HL')
BEGIN
  DECLARE @PID_6662 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6662 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0827/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Tubular handling for TGT-20X wellrn', NULL, @PID_6662, NULL, @DocSeedUserId, '2026-08-11', '2026-08-11 15:58:18', '2026-08-11 15:58:18', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0828/HL')
BEGIN
  DECLARE @PID_6663 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6663 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0828/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of Well completion for TGT-20X wellrn', NULL, @PID_6663, NULL, @DocSeedUserId, '2026-08-11', '2026-08-11 15:59:32', '2026-08-11 15:59:32', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0829/HL')
BEGIN
  DECLARE @PID_6664 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6664 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0829/HL', 'INCOMING', 'Distributed', N'Petrovietnam - Approval for ARR of QA QC for TGT-20X wellrn', NULL, @PID_6664, NULL, @DocSeedUserId, '2026-08-11', '2026-08-11 16:00:25', '2026-08-11 16:00:25', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0830/HL')
BEGIN
  DECLARE @PID_6665 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P19');
  IF @PID_6665 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0830/HL', 'INCOMING', 'Distributed', N'PTTEP - Approval for Upgrade VSAT IP for TGT-H1-WHPrn', NULL, @PID_6665, NULL, @DocSeedUserId, '2026-08-11', '2026-08-11 16:02:32', '2026-08-11 16:02:32', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0831/HLHV')
BEGIN
  DECLARE @PID_6666 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  IF @PID_6666 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0831/HLHV', 'INCOMING', 'Distributed', N'PVEP - Hỗ trợ cung cấp số liệu, tài liệu phục vụ Nhiệm vụ khoa học công nghệrn', NULL, @PID_6666, NULL, @DocSeedUserId, '2026-08-11', '2026-08-12 10:38:01', '2026-08-12 10:38:01', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0832/HLHV')
BEGIN
  DECLARE @PID_6667 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P26');
  IF @PID_6667 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0832/HLHV', 'INCOMING', 'Distributed', N'Petrovietnam - Support the well data and documents for VPI''s science and technology tasksrn', NULL, @PID_6667, NULL, @DocSeedUserId, '2026-08-12', '2026-08-12 10:38:45', '2026-08-12 10:38:45', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1319/HV/C&P')
BEGIN
  DECLARE @PID_O10353 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'Bao Nghi');
  DECLARE @DID_O10353 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10353 IS NOT NULL AND @DID_O10353 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1319/HV/C&P', 'OUTGOING', 'Draft', N'Subject:	Bid Clarification No. 01-Follow-up #1rnRFP Title:	Provision of Auto Sampling System of CPP3 V-1-C Separator rnfor CNV-BH Commingle in 2026rnRFP No.:	HV-PRD-26-033rn', NULL, @PID_O10353, @DID_O10353, @DocSeedUserId, NULL, NULL, '2026-07-14 05:59:49', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1320/HV/C&P')
BEGIN
  DECLARE @PID_O10354 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'DE');
  DECLARE @DID_O10354 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10354 IS NOT NULL AND @DID_O10354 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1320/HV/C&P', 'OUTGOING', 'Draft', N'Subject:	Bid Clarification No. 01 – Follow-up#1rnRFP Title:	Provision of Auto Sampling System of CPP3 V-1-C Separator rnfor CNV-BH Commingle in 2026rnRFP No.:	HV-PRD-26-033rn', NULL, @PID_O10354, @DID_O10354, @DocSeedUserId, NULL, NULL, '2026-07-14 06:52:43', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1321/HV/C&P')
BEGIN
  DECLARE @PID_O10355 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'T2');
  DECLARE @DID_O10355 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10355 IS NOT NULL AND @DID_O10355 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1321/HV/C&P', 'OUTGOING', 'Draft', N'Subject:	Bid Clarification No. 01 – Follow-up#1rnRFP Title:	Provision of Auto Sampling System of CPP3 V-1-C Separator rnfor CNV-BH Commingle in 2026rnRFP No.:	HV-PRD-26-033rn', NULL, @PID_O10355, @DID_O10355, @DocSeedUserId, NULL, NULL, '2026-07-14 08:43:01', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1322/HL/C&P')
BEGIN
  DECLARE @PID_O10356 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'O4');
  DECLARE @DID_O10356 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10356 IS NOT NULL AND @DID_O10356 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1322/HL/C&P', 'OUTGOING', 'Draft', N'Clar 3 - FU#1', NULL, @PID_O10356, @DID_O10356, @DocSeedUserId, NULL, NULL, '2026-07-14 08:49:42', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1323/HV/C&P')
BEGIN
  DECLARE @PID_O10357 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'V7');
  DECLARE @DID_O10357 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10357 IS NOT NULL AND @DID_O10357 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1323/HV/C&P', 'OUTGOING', 'Draft', N'Subject:	Bid Clarification No. 01-Follow-up#1rnRFP Title:	Provision of Auto Sampling System of CPP3 V-1-C Separator rnfor CNV-BH Commingle in 2026rnRFP No.:	HV-PRD-26-033rn', NULL, @PID_O10357, @DID_O10357, @DocSeedUserId, NULL, NULL, '2026-07-14 09:15:10', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1324/HLHV/C&P')
BEGIN
  DECLARE @PID_O10358 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S2');
  DECLARE @DID_O10358 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10358 IS NOT NULL AND @DID_O10358 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1324/HLHV/C&P', 'OUTGOING', 'Distributed', N'Request for Proposal for Provision of Additional scopernAssessment of Occupational Burden and Ergonomic Psychophysiological Indicators for Offshore Staff', NULL, @PID_O10358, @DID_O10358, @DocSeedUserId, NULL, '2026-07-16', '2026-07-14 10:20:24', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1325/HL/C&P')
BEGIN
  DECLARE @PID_O10359 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVD Baker Hughes');
  DECLARE @DID_O10359 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10359 IS NOT NULL AND @DID_O10359 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1325/HL/C&P', 'OUTGOING', 'Draft', N'Subject:	Clarification No. 04rnRFP Title:	Provision of E-line Perforating and Pipe Recovery Equipment & Services for TGT-20X Well in 2026 rnRFP No.:	HLHV-DRL-23-049-A03rn', NULL, @PID_O10359, @DID_O10359, @DocSeedUserId, NULL, NULL, '2026-07-14 10:45:12', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1326/HL/C&P')
BEGIN
  DECLARE @PID_O10360 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVD Baker Hughes');
  DECLARE @DID_O10360 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10360 IS NOT NULL AND @DID_O10360 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1326/HL/C&P', 'OUTGOING', 'Draft', N'Clarification No. 03rnProvision of Cementing Equipment and Services for TGT-20X in 2025-2026 TGT and CNV Drilling CampaignsrnHLHV-DRL-23-040-A03rn', NULL, @PID_O10360, @DID_O10360, @DocSeedUserId, NULL, NULL, '2026-07-14 10:56:04', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1328/HL/C&P')
BEGIN
  DECLARE @PID_O10362 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVD Baker Hughes');
  DECLARE @DID_O10362 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10362 IS NOT NULL AND @DID_O10362 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1328/HL/C&P', 'OUTGOING', 'Draft', N'rnSubject:	rnBid Clarification No. 02 – Follow-up#2rnITB Title:	Provision of Coil Tubing Equipment and Services in 2026rnITB No.:	HL-PRD-26-044rn', NULL, @PID_O10362, @DID_O10362, @DocSeedUserId, NULL, NULL, '2026-07-14 15:12:19', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1365/HLHV/C&P')
BEGIN
  DECLARE @PID_O10399 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'VNHSouth');
  DECLARE @DID_O10399 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10399 IS NOT NULL AND @DID_O10399 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1365/HLHV/C&P', 'OUTGOING', 'Draft', N'Helicopter services_Fuel price', NULL, @PID_O10399, @DID_O10399, @DocSeedUserId, NULL, NULL, '2026-07-20 09:34:42', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1369/HV/FIN')
BEGIN
  DECLARE @PID_O10403 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP3');
  DECLARE @DID_O10403 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @PID_O10403 IS NOT NULL AND @DID_O10403 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1369/HV/FIN', 'OUTGOING', 'Distributed', N'Subject: Block 9-2 Ca Ngu Vang– Monthly Statement for June 2026', NULL, @PID_O10403, @DID_O10403, @DocSeedUserId, NULL, '2026-07-22', '2026-07-21 11:07:14', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1370/HV/HSE')
BEGIN
  DECLARE @PID_O10404 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PCSQVTCA2');
  DECLARE @DID_O10404 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @PID_O10404 IS NOT NULL AND @DID_O10404 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1370/HV/HSE', 'OUTGOING', 'Distributed', N'Đơn đề nghị cấp GPVC VLNCN', NULL, @PID_O10404, @DID_O10404, @DocSeedUserId, NULL, '2026-07-21', '2026-07-21 13:42:38', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1371/HV/HSE')
BEGIN
  DECLARE @PID_O10405 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PCSQVTCA2');
  DECLARE @DID_O10405 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @PID_O10405 IS NOT NULL AND @DID_O10405 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1371/HV/HSE', 'OUTGOING', 'Distributed', N'Giấy ĐKTN về VLNCN', NULL, @PID_O10405, @DID_O10405, @DocSeedUserId, NULL, '2026-07-21', '2026-07-21 13:44:20', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1372/HL/C&P')
BEGIN
  DECLARE @PID_O10406 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PT');
  DECLARE @DID_O10406 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10406 IS NOT NULL AND @DID_O10406 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1372/HL/C&P', 'OUTGOING', 'Distributed', N'Bid Clarification No. 01 – Follow-up No. 03rnSupply of Spare Part for Well Intervention in 2026rn', NULL, @PID_O10406, @DID_O10406, @DocSeedUserId, NULL, '2026-07-21', '2026-07-21 13:44:37', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1373/HL/HSE')
BEGIN
  DECLARE @PID_O10407 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP4');
  DECLARE @DID_O10407 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @PID_O10407 IS NOT NULL AND @DID_O10407 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1373/HL/HSE', 'OUTGOING', 'Draft', N'Báo cáo tình hình nộp phí bảo vệ môi trường đối với nước khai thác thải theo Nghị định số 346/2025/NĐ-CP ngày 29/12/2025 của Chính phủ', NULL, @PID_O10407, @DID_O10407, @DocSeedUserId, NULL, NULL, '2026-07-21 13:50:51', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1375/HL/C&P')
BEGIN
  DECLARE @PID_O10409 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10409 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10409 IS NOT NULL AND @DID_O10409 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1375/HL/C&P', 'OUTGOING', 'Draft', N'Request for Approval for Award Recommendation for Provision of Shaker Screens for TGT-20X Well in 2026 Drilling Campaign – Cancellation of Submitted Award Recommendationrn    (Contract No. HLHV-DRL-25-045B-A02)rn', NULL, @PID_O10409, @DID_O10409, @DocSeedUserId, NULL, NULL, '2026-07-21 15:14:46', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1376/HL/C&P')
BEGIN
  DECLARE @PID_O10410 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'Scomi');
  DECLARE @DID_O10410 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10410 IS NOT NULL AND @DID_O10410 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1376/HL/C&P', 'OUTGOING', 'Draft', N'Clarification No. 03rnProvision of Solid Control Equipment and Services for TGT-20X in 2025-2026 TGT and CNV Drilling CampaignsrnHLHV-DRL-25-045A-A08rn', NULL, @PID_O10410, @DID_O10410, @DocSeedUserId, NULL, NULL, '2026-07-21 16:18:16', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1377/HV/C&P')
BEGIN
  DECLARE @PID_O10411 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'AES');
  DECLARE @DID_O10411 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10411 IS NOT NULL AND @DID_O10411 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1377/HV/C&P', 'OUTGOING', 'Distributed', N'Bid Bulletin No. 02rn', NULL, @PID_O10411, @DID_O10411, @DocSeedUserId, NULL, '2026-07-21', '2026-07-21 16:19:58', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1378/HV/SUB')
BEGIN
  DECLARE @PID_O10412 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP');
  DECLARE @DID_O10412 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @PID_O10412 IS NOT NULL AND @DID_O10412 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1378/HV/SUB', 'OUTGOING', 'Distributed', N'Approval Request for CNV-5X Well Completion & Well Testing Proposal', NULL, @PID_O10412, @DID_O10412, @DocSeedUserId, NULL, '2026-07-21', '2026-07-21 17:32:32', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1379/HL/C&P')
BEGIN
  DECLARE @PID_O10413 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10413 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10413 IS NOT NULL AND @DID_O10413 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1379/HL/C&P', 'OUTGOING', 'Draft', N'Request for Approval for Award Recommendation for Provision of Tubular Handling Equipment & Services for TGT-20X Well in 2026 Drilling Campaign rn  	(Contract No. HLHV-DRL-25-064-A02)rn', NULL, @PID_O10413, @DID_O10413, @DocSeedUserId, NULL, NULL, '2026-07-21 19:21:52', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1380/HV/HSE')
BEGIN
  DECLARE @PID_O10414 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PCSQVTCA2');
  DECLARE @DID_O10414 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @PID_O10414 IS NOT NULL AND @DID_O10414 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1380/HV/HSE', 'OUTGOING', 'Distributed', N'LỆNH XUẤT KHO', NULL, @PID_O10414, @DID_O10414, @DocSeedUserId, NULL, '2026-07-22', '2026-07-22 09:46:42', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1385/HL/HSE')
BEGIN
  DECLARE @PID_O10419 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'STNVMTTB');
  DECLARE @DID_O10419 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @PID_O10419 IS NOT NULL AND @DID_O10419 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1385/HL/HSE', 'OUTGOING', 'Draft', N'Kê khai phí bảo vệ môi trường đối với nước thải theo Nghị định số 346/2025/NĐ-CP', NULL, @PID_O10419, @DID_O10419, @DocSeedUserId, NULL, NULL, '2026-07-22 13:52:28', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1387/HL/FIN')
BEGIN
  DECLARE @PID_O10421 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP2');
  DECLARE @DID_O10421 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @PID_O10421 IS NOT NULL AND @DID_O10421 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1387/HL/FIN', 'OUTGOING', 'Distributed', N'Subject:	Block 16-1 Te Giac Trang – Monthly Statement for June 2026', NULL, @PID_O10421, @DID_O10421, @DocSeedUserId, NULL, '2026-07-22', '2026-07-22 16:41:00', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1389/HL/C&P')
BEGIN
  DECLARE @PID_O10423 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P2');
  DECLARE @DID_O10423 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10423 IS NOT NULL AND @DID_O10423 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1389/HL/C&P', 'OUTGOING', 'Draft', N'Letter of Award - Provision of Jack-up Drilling Rig Rental and Services for TGT-20X Well rn', NULL, @PID_O10423, @DID_O10423, @DocSeedUserId, NULL, NULL, '2026-07-23 11:53:28', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1391/HL/MGM')
BEGIN
  DECLARE @PID_O10425 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10425 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'MGM');
  IF @PID_O10425 IS NOT NULL AND @DID_O10425 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1391/HL/MGM', 'OUTGOING', 'Distributed', N'TGT Monthly Production Operations Report for June 2026', NULL, @PID_O10425, @DID_O10425, @DocSeedUserId, NULL, '2026-07-23', '2026-07-23 14:16:04', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1392/HV/MGM')
BEGIN
  DECLARE @PID_O10426 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP2');
  DECLARE @DID_O10426 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'MGM');
  IF @PID_O10426 IS NOT NULL AND @DID_O10426 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1392/HV/MGM', 'OUTGOING', 'Distributed', N'CNV Monthly Production Operations Report for June 2026', NULL, @PID_O10426, @DID_O10426, @DocSeedUserId, NULL, '2026-07-23', '2026-07-23 14:17:38', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1393/HL/MGM')
BEGIN
  DECLARE @PID_O10427 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10427 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'MGM');
  IF @PID_O10427 IS NOT NULL AND @DID_O10427 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1393/HL/MGM', 'OUTGOING', 'Distributed', N'TGT Monthly Activity Status Report for June 2026', NULL, @PID_O10427, @DID_O10427, @DocSeedUserId, NULL, '2026-07-23', '2026-07-23 15:26:33', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1394/HV/MGM')
BEGIN
  DECLARE @PID_O10428 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP2');
  DECLARE @DID_O10428 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'MGM');
  IF @PID_O10428 IS NOT NULL AND @DID_O10428 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1394/HV/MGM', 'OUTGOING', 'Distributed', N'CNV Monthly Activity Status Report for June 2026', NULL, @PID_O10428, @DID_O10428, @DocSeedUserId, NULL, '2026-07-23', '2026-07-23 15:35:27', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1397/HV/PRD')
BEGIN
  DECLARE @PID_O10431 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P');
  DECLARE @DID_O10431 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @PID_O10431 IS NOT NULL AND @DID_O10431 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1397/HV/PRD', 'OUTGOING', 'Distributed', N'CNV Gas Delivery Forecast Week 27 July - 16 Aug 26', NULL, @PID_O10431, @DID_O10431, @DocSeedUserId, NULL, '2026-07-23', '2026-07-23 15:57:09', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1401/HL/PRD')
BEGIN
  DECLARE @PID_O10435 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P');
  DECLARE @DID_O10435 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @PID_O10435 IS NOT NULL AND @DID_O10435 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1401/HL/PRD', 'OUTGOING', 'Distributed', N'TGT Gas Delivery Forecast Week 27 July - 16 Aug 26', NULL, @PID_O10435, @DID_O10435, @DocSeedUserId, NULL, '2026-07-23', '2026-07-23 16:02:47', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1402/HL/PRJ')
BEGIN
  DECLARE @PID_O10436 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P14');
  DECLARE @DID_O10436 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRJ');
  IF @PID_O10436 IS NOT NULL AND @DID_O10436 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1402/HL/PRJ', 'OUTGOING', 'Distributed', N'Response to PVEP - FPSO', NULL, @PID_O10436, @DID_O10436, @DocSeedUserId, NULL, '2026-07-27', '2026-07-23 16:44:00', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1403/HV/C&P')
BEGIN
  DECLARE @PID_O10437 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP2');
  DECLARE @DID_O10437 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10437 IS NOT NULL AND @DID_O10437 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1403/HV/C&P', 'OUTGOING', 'Draft', N'Request for Approval of Contracting Strategy for the Provision of Chemical and Acidizing Services (ITB No. HV-PRD-26-076)', NULL, @PID_O10437, @DID_O10437, @DocSeedUserId, NULL, NULL, '2026-07-23 16:59:25', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1404/HL/C&P')
BEGIN
  DECLARE @PID_O10438 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10438 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10438 IS NOT NULL AND @DID_O10438 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1404/HL/C&P', 'OUTGOING', 'Draft', N'Request for Approval for Award Recommendation for Provision of Downhole Tool Rental Services for TGT-20X Well in 2026 Drilling Campaign – Response to PVEPrn  	(Contract No. HLHV-DRL-25-012A-A06 & HLHV-DRL-25-012B-A02)rn', NULL, @PID_O10438, @DID_O10438, @DocSeedUserId, NULL, NULL, '2026-07-23 17:07:55', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1405/HL/C&P')
BEGIN
  DECLARE @PID_O10439 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVD Baker Hughes');
  DECLARE @DID_O10439 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10439 IS NOT NULL AND @DID_O10439 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1405/HL/C&P', 'OUTGOING', 'Draft', N'rnSubject:	rnBid Clarification No. 02 – Follow-up#2rnITB Title:	Provision of Coil Tubing Equipment and Services in 2026rnITB No.:	HL-PRD-26-044rn', NULL, @PID_O10439, @DID_O10439, @DocSeedUserId, NULL, NULL, '2026-07-23 19:04:21', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1407/HL/C&P')
BEGIN
  DECLARE @PID_O10441 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'Halliburton');
  DECLARE @DID_O10441 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10441 IS NOT NULL AND @DID_O10441 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1407/HL/C&P', 'OUTGOING', 'Draft', N'rnSubject:	rnBid Clarification No. 02 – Follow-up #1rnITB Title:	Provision of Coil Tubing Equipment and Services in 2026rnITB No.:	HL-PRD-26-044rn', NULL, @PID_O10441, @DID_O10441, @DocSeedUserId, NULL, NULL, '2026-07-24 10:55:23', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1409/HL/C&P')
BEGIN
  DECLARE @PID_O10443 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'Halliburton');
  DECLARE @DID_O10443 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10443 IS NOT NULL AND @DID_O10443 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1409/HL/C&P', 'OUTGOING', 'Draft', N'rnSubject:	rnBid Clarification No. 02 – Follow-up #2rnITB Title:	Provision of Coil Tubing Equipment and Services in 2026rnITB No.:	HL-PRD-26-044rn', NULL, @PID_O10443, @DID_O10443, @DocSeedUserId, NULL, NULL, '2026-07-24 11:27:28', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1410/HL/HSE')
BEGIN
  DECLARE @PID_O10444 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PCSQVTCA2');
  DECLARE @DID_O10444 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @PID_O10444 IS NOT NULL AND @DID_O10444 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1410/HL/HSE', 'OUTGOING', 'Distributed', N'Báo cáo vận chuyển VLNCN còn thừa về kho bảo quản', NULL, @PID_O10444, @DID_O10444, @DocSeedUserId, NULL, '2026-07-24', '2026-07-24 13:08:24', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1411/HL/DRL')
BEGIN
  DECLARE @PID_O10445 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP');
  DECLARE @DID_O10445 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'DRL');
  IF @PID_O10445 IS NOT NULL AND @DID_O10445 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1411/HL/DRL', 'OUTGOING', 'Distributed', N'PLUG & ABANDOMENT PROGRAMME FOR WELL 16-1-TGT-H4-12P', NULL, @PID_O10445, @DID_O10445, @DocSeedUserId, NULL, '2026-07-26', '2026-07-24 15:40:23', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1414/HL/C&P')
BEGIN
  DECLARE @PID_O10448 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'ES2');
  DECLARE @DID_O10448 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10448 IS NOT NULL AND @DID_O10448 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1414/HL/C&P', 'OUTGOING', 'Draft', N'Bid Clarification No. 02rnSupply of Spare Parts for HPU, Production & Electrical System on TGT-WHPs', NULL, @PID_O10448, @DID_O10448, @DocSeedUserId, NULL, NULL, '2026-07-24 16:47:43', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1415/HL/C&P')
BEGIN
  DECLARE @PID_O10449 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'G');
  DECLARE @DID_O10449 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10449 IS NOT NULL AND @DID_O10449 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1415/HL/C&P', 'OUTGOING', 'Draft', N'Bid Clarification No. 02rnSupply of Spare Parts for HPU, Production & Electrical System on TGT-WHPs', NULL, @PID_O10449, @DID_O10449, @DocSeedUserId, NULL, NULL, '2026-07-24 16:48:52', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1416/HL/C&P')
BEGIN
  DECLARE @PID_O10450 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'KVA');
  DECLARE @DID_O10450 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10450 IS NOT NULL AND @DID_O10450 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1416/HL/C&P', 'OUTGOING', 'Draft', N'Bid Clarification No. 02rnSupply of Spare Parts for HPU, Production & Electrical System on TGT-WHPs', NULL, @PID_O10450, @DID_O10450, @DocSeedUserId, NULL, NULL, '2026-07-24 16:50:00', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1417/HL/C&P')
BEGIN
  DECLARE @PID_O10451 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'LV-Tech');
  DECLARE @DID_O10451 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10451 IS NOT NULL AND @DID_O10451 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1417/HL/C&P', 'OUTGOING', 'Draft', N'Bid Clarification No. 02rnSupply of Spare Parts for HPU, Production & Electrical System on TGT-WHPs', NULL, @PID_O10451, @DID_O10451, @DocSeedUserId, NULL, NULL, '2026-07-24 16:51:08', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1418/HL/C&P')
BEGIN
  DECLARE @PID_O10452 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'SLB');
  DECLARE @DID_O10452 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10452 IS NOT NULL AND @DID_O10452 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1418/HL/C&P', 'OUTGOING', 'Draft', N'rnSubject:	rnBid Clarification No. 02-FU1rnITB Title:	Provision of Coil Tubing Equipment and Services in 2026rnITB No.:	HL-PRD-26-044rn', NULL, @PID_O10452, @DID_O10452, @DocSeedUserId, NULL, NULL, '2026-07-27 08:18:24', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1419/HL/PRD')
BEGIN
  DECLARE @PID_O10453 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'VSP');
  DECLARE @DID_O10453 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @PID_O10453 IS NOT NULL AND @DID_O10453 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1419/HL/PRD', 'OUTGOING', 'Distributed', N'DAILY TGT FIELD PRODUCTION TARGET IN AUGUST 2026', NULL, @PID_O10453, @DID_O10453, @DocSeedUserId, NULL, '2026-07-27', '2026-07-27 11:05:28', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1421/HLHV/HSE')
BEGIN
  DECLARE @PID_O10455 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP4');
  DECLARE @DID_O10455 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @PID_O10455 IS NOT NULL AND @DID_O10455 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1421/HLHV/HSE', 'OUTGOING', 'Distributed', N'Đóng góp ý kiến đối với Dự thảo Thông tư sửa đổi bổ sung Thông tư số 40/2018/TT-BCT', NULL, @PID_O10455, @DID_O10455, @DocSeedUserId, NULL, '2026-07-28', '2026-07-28 10:00:07', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1422/HLHV/PRJ')
BEGIN
  DECLARE @PID_O10456 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP3');
  DECLARE @DID_O10456 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRJ');
  IF @PID_O10456 IS NOT NULL AND @DID_O10456 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1422/HLHV/PRJ', 'OUTGOING', 'Distributed', N'TGT and CNV Abandonment Plan Updates in 2026', NULL, @PID_O10456, @DID_O10456, @DocSeedUserId, NULL, '2026-07-28', '2026-07-28 13:28:51', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1423/HL/C&P')
BEGIN
  DECLARE @PID_O10457 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP2');
  DECLARE @DID_O10457 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10457 IS NOT NULL AND @DID_O10457 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1423/HL/C&P', 'OUTGOING', 'Draft', N'Request for Approval for Award Recommendation for Provision of Quality Assurance and Quality Control Specialist Services for TGT-20X Well in 2026 Drilling Campaign -  Clarification to PVEP', NULL, @PID_O10457, @DID_O10457, @DocSeedUserId, NULL, NULL, '2026-07-28 13:45:27', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1424/HL/C&P')
BEGIN
  DECLARE @PID_O10458 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVD Baker Hughes');
  DECLARE @DID_O10458 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10458 IS NOT NULL AND @DID_O10458 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1424/HL/C&P', 'OUTGOING', 'Draft', N'Clarification No. 02 – Follow-up 1rnProvision of Cementing Equipment and Services for TGT-20X in 2025-2026 TGT and CNV Drilling CampaignsrnHLHV-DRL-23-040-A03rn', NULL, @PID_O10458, @DID_O10458, @DocSeedUserId, NULL, NULL, '2026-07-28 19:19:11', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1425/HL/C&P')
BEGIN
  DECLARE @PID_O10459 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10459 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10459 IS NOT NULL AND @DID_O10459 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1425/HL/C&P', 'OUTGOING', 'Draft', N'Request for Approval of Rebidding the Provision of Jacket Member & H1-HST Gaslift Riser Repair Services on TGT-H1 Jacket rn	(RFP No. HL-PRD-26-080)rn', NULL, @PID_O10459, @DID_O10459, @DocSeedUserId, NULL, NULL, '2026-07-28 19:59:20', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1426/HV/PRD')
BEGIN
  DECLARE @PID_O10460 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P');
  DECLARE @DID_O10460 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @PID_O10460 IS NOT NULL AND @DID_O10460 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1426/HV/PRD', 'OUTGOING', 'Distributed', N'CNV Gas Delivery Forecast Week 03 - 23 Aug 26', NULL, @PID_O10460, @DID_O10460, @DocSeedUserId, NULL, '2026-07-28', '2026-07-29 09:14:58', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1427/HL/PRD')
BEGIN
  DECLARE @PID_O10461 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P');
  DECLARE @DID_O10461 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @PID_O10461 IS NOT NULL AND @DID_O10461 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1427/HL/PRD', 'OUTGOING', 'Distributed', N'TGT Gas Delivery Forecast Week 03 - 23 Aug 26', NULL, @PID_O10461, @DID_O10461, @DocSeedUserId, NULL, '2026-07-29', '2026-07-29 09:18:25', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1428/HV/PRD')
BEGIN
  DECLARE @PID_O10462 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP2');
  DECLARE @DID_O10462 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @PID_O10462 IS NOT NULL AND @DID_O10462 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1428/HV/PRD', 'OUTGOING', 'Distributed', N'Approval Request for 2026 Budget Conversion from Contingent to Firm for CNV-6PST1 Perforation above Production Packer rnrn', NULL, @PID_O10462, @DID_O10462, @DocSeedUserId, NULL, '2026-07-29', '2026-07-29 11:09:17', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1430/HL/DRL')
BEGIN
  DECLARE @PID_O10464 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP');
  DECLARE @DID_O10464 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'DRL');
  IF @PID_O10464 IS NOT NULL AND @DID_O10464 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1430/HL/DRL', 'OUTGOING', 'Draft', N'DRILLING PROGRAMME FOR WELL 16-1-TGT-H4-20X', NULL, @PID_O10464, @DID_O10464, @DocSeedUserId, NULL, NULL, '2026-07-29 13:36:30', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1433/HL/SUB')
BEGIN
  DECLARE @PID_O10467 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10467 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @PID_O10467 IS NOT NULL AND @DID_O10467 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1433/HL/SUB', 'OUTGOING', 'Distributed', N'Invitation to TGT RFDP 2026 Workshop', NULL, @PID_O10467, @DID_O10467, @DocSeedUserId, NULL, '2026-07-29', '2026-07-29 15:41:16', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1434/HL/C&P')
BEGIN
  DECLARE @PID_O10468 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10468 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10468 IS NOT NULL AND @DID_O10468 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1434/HL/C&P', 'OUTGOING', 'Draft', N'Request for Approval for Award Recommendation for Provision of Vessel Services Services for TGT-20X Well rn  	(Contract No. HLHV-DRL-25-030A-A08 & HLHV-DRL-25-030B-A05)rn', NULL, @PID_O10468, @DID_O10468, @DocSeedUserId, NULL, NULL, '2026-07-29 15:53:06', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1435/HL/C&P')
BEGIN
  DECLARE @PID_O10469 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'Halliburton');
  DECLARE @DID_O10469 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10469 IS NOT NULL AND @DID_O10469 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1435/HL/C&P', 'OUTGOING', 'Draft', N'rnSubject:	Bid Clarification No. 02 – Follow-up #3rnITB Title:	Provision of Coil Tubing Equipment and Services in 2026rnITB No.:	HL-PRD-26-044rn', NULL, @PID_O10469, @DID_O10469, @DocSeedUserId, NULL, NULL, '2026-07-29 16:30:15', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1436/HL/C&P')
BEGIN
  DECLARE @PID_O10470 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10470 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10470 IS NOT NULL AND @DID_O10470 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1436/HL/C&P', 'OUTGOING', 'Draft', N'Request for Approval for Award Recommendation for Provision of Professional Manpower Services for TGT-20X Well', NULL, @PID_O10470, @DID_O10470, @DocSeedUserId, NULL, NULL, '2026-07-29 16:30:34', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1441/HL/C&P')
BEGIN
  DECLARE @PID_O10475 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P3');
  DECLARE @DID_O10475 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10475 IS NOT NULL AND @DID_O10475 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1441/HL/C&P', 'OUTGOING', 'Distributed', N'BC#1rnProvision of Recoating and Reinforcement Services for TGT H1 Production Flowline & H5 Risersrnrn', NULL, @PID_O10475, @DID_O10475, @DocSeedUserId, NULL, '2026-07-31', '2026-07-30 16:24:06', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1442/HL/SUB')
BEGIN
  DECLARE @PID_O10476 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PetroVietnam');
  DECLARE @DID_O10476 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @PID_O10476 IS NOT NULL AND @DID_O10476 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1442/HL/SUB', 'OUTGOING', 'Distributed', N'HLJOC Submission of TGT RAR 2026', NULL, @PID_O10476, @DID_O10476, @DocSeedUserId, NULL, '2026-07-31', '2026-07-30 17:07:52', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1443/HLHV/HSE')
BEGIN
  DECLARE @PID_O10477 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'VSP');
  DECLARE @DID_O10477 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @PID_O10477 IS NOT NULL AND @DID_O10477 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1443/HLHV/HSE', 'OUTGOING', 'Draft', N'The celebration of 18-year CNV, 15-year TGT O&M Anniversary', NULL, @PID_O10477, @DID_O10477, @DocSeedUserId, NULL, NULL, '2026-07-31 09:44:55', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-1444/HL/C&P')
BEGIN
  DECLARE @PID_O10478 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10478 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10478 IS NOT NULL AND @DID_O10478 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-1444/HL/C&P', 'OUTGOING', 'Draft', N'Request for Approval of Award Recommendation for Provision of Solid Control Equipment and Services for TGT-20X in 2026 Drilling Campaign – Reminder to Petrovietnamrn	(Contract No. HLHV-DRL-25-045A-A08)rn', NULL, @PID_O10478, @DID_O10478, @DocSeedUserId, NULL, NULL, '2026-07-31 16:16:26', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1447/HL/HSE')
BEGIN
  DECLARE @PID_O10481 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PCSQVTCA2');
  DECLARE @DID_O10481 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @PID_O10481 IS NOT NULL AND @DID_O10481 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1447/HL/HSE', 'OUTGOING', 'Distributed', N'Báo cáo vận chuyển VLNCN còn thừa về kho bảo quản', NULL, @PID_O10481, @DID_O10481, @DocSeedUserId, NULL, '2026-08-03', '2026-08-03 10:06:36', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1448/HL/FIN')
BEGIN
  DECLARE @PID_O10482 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP2');
  DECLARE @DID_O10482 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @PID_O10482 IS NOT NULL AND @DID_O10482 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1448/HL/FIN', 'OUTGOING', 'Distributed', N'Subject:	Entitlements Applicable to TGT- 508', NULL, @PID_O10482, @DID_O10482, @DocSeedUserId, NULL, '2026-08-03', '2026-08-03 10:28:55', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1450/HLHV/C&P')
BEGIN
  DECLARE @PID_O10484 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'S2');
  DECLARE @DID_O10484 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10484 IS NOT NULL AND @DID_O10484 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1450/HLHV/C&P', 'OUTGOING', 'Draft', N'Bid Bulletin No. 02rnProvision of ISO 14001:2026 Consultancy and Internal Audit Services for Environment Management Systemrn', NULL, @PID_O10484, @DID_O10484, @DocSeedUserId, NULL, NULL, '2026-08-03 11:06:52', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1451/HL/PRD')
BEGIN
  DECLARE @PID_O10485 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PGS');
  DECLARE @DID_O10485 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @PID_O10485 IS NOT NULL AND @DID_O10485 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1451/HL/PRD', 'OUTGOING', 'Distributed', N'TGT HSDT Final allocation statement for Mar 2026', NULL, @PID_O10485, @DID_O10485, @DocSeedUserId, NULL, '2026-08-03', '2026-08-03 11:34:28', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1454/HL/C&P')
BEGIN
  DECLARE @PID_O10488 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10488 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10488 IS NOT NULL AND @DID_O10488 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1454/HL/C&P', 'OUTGOING', 'Draft', N'Request for Approval for Award Recommendation for Provision of Professional Manpower Services for TGT-20X Well rn  (Contract No. HLHV-DRL-25-068)rn', NULL, @PID_O10488, @DID_O10488, @DocSeedUserId, NULL, NULL, '2026-08-03 16:02:18', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1455/HV/PRD')
BEGIN
  DECLARE @PID_O10489 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P');
  DECLARE @DID_O10489 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @PID_O10489 IS NOT NULL AND @DID_O10489 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1455/HV/PRD', 'OUTGOING', 'Distributed', N'CNV Provisional Gas Sales for July 2026', NULL, @PID_O10489, @DID_O10489, @DocSeedUserId, NULL, '2026-08-04', '2026-08-04 08:45:17', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1457/HL/FIN')
BEGIN
  DECLARE @PID_O10491 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP2');
  DECLARE @DID_O10491 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @PID_O10491 IS NOT NULL AND @DID_O10491 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1457/HL/FIN', 'OUTGOING', 'Distributed', N'2026 Mid-Year Budget review and approval request', NULL, @PID_O10491, @DID_O10491, @DocSeedUserId, NULL, '2026-08-06', '2026-08-04 10:34:07', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1458/HV/FIN')
BEGIN
  DECLARE @PID_O10492 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP3');
  DECLARE @DID_O10492 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @PID_O10492 IS NOT NULL AND @DID_O10492 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1458/HV/FIN', 'OUTGOING', 'Distributed', N'2026 Mid-Year Budget review and approval request', NULL, @PID_O10492, @DID_O10492, @DocSeedUserId, NULL, '2026-08-06', '2026-08-04 10:34:34', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1459/HL/HSE')
BEGIN
  DECLARE @PID_O10493 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PetroVietnam');
  DECLARE @DID_O10493 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @PID_O10493 IS NOT NULL AND @DID_O10493 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1459/HL/HSE', 'OUTGOING', 'Distributed', N'Trình nộp tài liệu Quản lý An toàn cho hoạt động khoan thăm dò giếng TGT-20X, mỏ Tê Giác Trắng', NULL, @PID_O10493, @DID_O10493, @DocSeedUserId, NULL, '2026-08-04', '2026-08-04 10:34:37', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1460/HL/HSE')
BEGIN
  DECLARE @PID_O10494 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PetroVietnam');
  DECLARE @DID_O10494 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @PID_O10494 IS NOT NULL AND @DID_O10494 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1460/HL/HSE', 'OUTGOING', 'Distributed', N'V/v trình nộp hoạch ứng phó sự cố tràn dầu cho chiến dịch khoan thăm dò giếng TGT-20X, mỏ Tê Giác Trắng, năm 2026', NULL, @PID_O10494, @DID_O10494, @DocSeedUserId, NULL, '2026-08-05', '2026-08-04 10:43:13', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1463/HL/C&P')
BEGIN
  DECLARE @PID_O10497 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'SLB');
  DECLARE @DID_O10497 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10497 IS NOT NULL AND @DID_O10497 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1463/HL/C&P', 'OUTGOING', 'Draft', N'rnSubject:	rnBid Clarification No. 02-FU2rnITB Title:	Provision of Coil Tubing Equipment and Services in 2026rnITB No.:	HL-PRD-26-044rn', NULL, @PID_O10497, @DID_O10497, @DocSeedUserId, NULL, NULL, '2026-08-05 13:19:02', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1464/HL/C&P')
BEGIN
  DECLARE @PID_O10498 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'Halliburton');
  DECLARE @DID_O10498 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10498 IS NOT NULL AND @DID_O10498 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1464/HL/C&P', 'OUTGOING', 'Draft', N'rnSubject:	rnBid Clarification No. 02 – Follow-up #4rnITB Title:	Provision of Coil Tubing Equipment and Services in 2026rnITB No.:	HL-PRD-26-044rn', NULL, @PID_O10498, @DID_O10498, @DocSeedUserId, NULL, NULL, '2026-08-05 14:10:47', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1465/HL/C&P')
BEGIN
  DECLARE @PID_O10499 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'Halliburton');
  DECLARE @DID_O10499 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10499 IS NOT NULL AND @DID_O10499 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1465/HL/C&P', 'OUTGOING', 'Draft', N'rnSubject:	rnBid Clarification No. 03rnITB Title:	Provision of Coil Tubing Equipment and Services in 2026rnITB No.:	HL-PRD-26-044rn', NULL, @PID_O10499, @DID_O10499, @DocSeedUserId, NULL, NULL, '2026-08-06 09:47:02', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1467/HL/HSE')
BEGIN
  DECLARE @PID_O10501 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PetroVietnam');
  DECLARE @DID_O10501 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @PID_O10501 IS NOT NULL AND @DID_O10501 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1467/HL/HSE', 'OUTGOING', 'Draft', N'Giấy ủy quyền', NULL, @PID_O10501, @DID_O10501, @DocSeedUserId, NULL, NULL, '2026-08-06 11:58:05', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1468/HL/C&P')
BEGIN
  DECLARE @PID_O10502 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P7');
  DECLARE @DID_O10502 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10502 IS NOT NULL AND @DID_O10502 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1468/HL/C&P', 'OUTGOING', 'Draft', N'Subject: 	Clarification No. 02 for Contract ExtensionrnRFP Title:	Supply of De-oiler and Associated ServicesrnRFP No.: HL-PRD-22-037-A01rn', NULL, @PID_O10502, @DID_O10502, @DocSeedUserId, NULL, NULL, '2026-08-06 13:45:21', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1476/HL/C&P')
BEGIN
  DECLARE @PID_O10510 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVD Baker Hughes');
  DECLARE @DID_O10510 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10510 IS NOT NULL AND @DID_O10510 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1476/HL/C&P', 'OUTGOING', 'Draft', N'rnSubject:	Bid Clarification No. 02 – Follow-up#3rnITB Title:	Provision of Coil Tubing Equipment and Services in 2026rnITB No.:	HL-PRD-26-044rn', NULL, @PID_O10510, @DID_O10510, @DocSeedUserId, NULL, NULL, '2026-08-06 15:02:01', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1477/HL/PRD')
BEGIN
  DECLARE @PID_O10511 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P');
  DECLARE @DID_O10511 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @PID_O10511 IS NOT NULL AND @DID_O10511 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1477/HL/PRD', 'OUTGOING', 'Distributed', N'TGT Gas Delivery Forecast Week 10 - 30 Aug 26', NULL, @PID_O10511, @DID_O10511, @DocSeedUserId, NULL, '2026-08-07', '2026-08-07 08:20:33', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1478/HV/PRD')
BEGIN
  DECLARE @PID_O10512 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'P');
  DECLARE @DID_O10512 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @PID_O10512 IS NOT NULL AND @DID_O10512 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1478/HV/PRD', 'OUTGOING', 'Distributed', N'CNV Gas Delivery Forecast Week 10 - 30 Aug 26', NULL, @PID_O10512, @DID_O10512, @DocSeedUserId, NULL, '2026-08-07', '2026-08-07 08:21:34', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1481/HV/FIN')
BEGIN
  DECLARE @PID_O10515 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP3');
  DECLARE @DID_O10515 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @PID_O10515 IS NOT NULL AND @DID_O10515 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1481/HV/FIN', 'OUTGOING', 'Draft', N'Subject: Block 9-2 CNV Associated Gas – Provisional Statements for July 2026.', NULL, @PID_O10515, @DID_O10515, @DocSeedUserId, NULL, NULL, '2026-08-07 11:49:34', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1483/HV/SUB')
BEGIN
  DECLARE @PID_O10517 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP2');
  DECLARE @DID_O10517 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @PID_O10517 IS NOT NULL AND @DID_O10517 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1483/HV/SUB', 'OUTGOING', 'Distributed', N'HVJOC CNV-5X Well Testing Update and Forward Plan', NULL, @PID_O10517, @DID_O10517, @DocSeedUserId, NULL, '2026-08-07', '2026-08-07 15:32:47', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1484/HL/C&P')
BEGIN
  DECLARE @PID_O10518 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'SLB');
  DECLARE @DID_O10518 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10518 IS NOT NULL AND @DID_O10518 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1484/HL/C&P', 'OUTGOING', 'Draft', N'Subject:	Bid Clarification No. 02-FU3rnITB Title:	Provision of Coil Tubing Equipment and Services in 2026rnITB No.:	HL-PRD-26-044rn', NULL, @PID_O10518, @DID_O10518, @DocSeedUserId, NULL, NULL, '2026-08-07 16:24:09', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1485/HL/C&P')
BEGIN
  DECLARE @PID_O10519 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10519 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10519 IS NOT NULL AND @DID_O10519 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1485/HL/C&P', 'OUTGOING', 'Draft', N'Report on Substitute Vessel under the Contract for Provision of Marine Growth Removal Services for TGT-H4 and TGT-H5 Jackets in 2026rn	(Contract No. HL-PRD-25-162)rn', NULL, @PID_O10519, @DID_O10519, @DocSeedUserId, NULL, NULL, '2026-08-09 09:40:04', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1486/HL/C&P')
BEGIN
  DECLARE @PID_O10520 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'HADUCO');
  DECLARE @DID_O10520 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10520 IS NOT NULL AND @DID_O10520 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1486/HL/C&P', 'OUTGOING', 'Draft', N'Clarification No. 01 – Follow-up 4rnProvision of Vessel Services for TGT-20X in 2025-2026 TGT and CNV Drilling CampaignsrnHLHV-DRL-25-030A-A08rn', NULL, @PID_O10520, @DID_O10520, @DocSeedUserId, NULL, NULL, '2026-08-09 15:19:53', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1487/HL/C&P')
BEGIN
  DECLARE @PID_O10521 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'Saigon Trade');
  DECLARE @DID_O10521 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10521 IS NOT NULL AND @DID_O10521 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1487/HL/C&P', 'OUTGOING', 'Draft', N'BC#1rnProvision of Recoating and Reinforcement Services for TGT H1 Production Flowline & H5 Risersrn', NULL, @PID_O10521, @DID_O10521, @DocSeedUserId, NULL, NULL, '2026-08-10 14:49:15', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1488/HL/FIN')
BEGIN
  DECLARE @PID_O10522 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP2');
  DECLARE @DID_O10522 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @PID_O10522 IS NOT NULL AND @DID_O10522 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1488/HL/FIN', 'OUTGOING', 'Draft', N'Subject: Block 16.1 TGT Associated Gas – Provisional Statements for July 2026.', NULL, @PID_O10522, @DID_O10522, @DocSeedUserId, NULL, NULL, '2026-08-10 16:20:54', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1489/HV/C&P')
BEGIN
  DECLARE @PID_O10523 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVD Baker Hughes');
  DECLARE @DID_O10523 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10523 IS NOT NULL AND @DID_O10523 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1489/HV/C&P', 'OUTGOING', 'Draft', N'rnSubject:	rnBid Clarification No. 01rnITB Title:	Provision of Nitrogen Pumping Equipment and Services for CNV-6PST1rnITB No.:	HV-PRD-25-112rn', NULL, @PID_O10523, @DID_O10523, @DocSeedUserId, NULL, NULL, '2026-08-11 08:12:20', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1491/HV/DRL')
BEGIN
  DECLARE @PID_O10525 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP');
  DECLARE @DID_O10525 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'DRL');
  IF @PID_O10525 IS NOT NULL AND @DID_O10525 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1491/HV/DRL', 'OUTGOING', 'Draft', N'Approval Request for Additional Scope of Work of Nitrogen for Well CNV-5X. rnrn', NULL, @PID_O10525, @DID_O10525, @DocSeedUserId, NULL, NULL, '2026-08-11 11:05:06', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1492/HL/PRD')
BEGIN
  DECLARE @PID_O10526 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PGS');
  DECLARE @DID_O10526 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @PID_O10526 IS NOT NULL AND @DID_O10526 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1492/HL/PRD', 'OUTGOING', 'Draft', N'TGT HSDT Provisional allocation statement for Jul 2026', NULL, @PID_O10526, @DID_O10526, @DocSeedUserId, NULL, NULL, '2026-08-11 13:54:27', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1493/HL/PRD')
BEGIN
  DECLARE @PID_O10527 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PGS');
  DECLARE @DID_O10527 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @PID_O10527 IS NOT NULL AND @DID_O10527 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1493/HL/PRD', 'OUTGOING', 'Draft', N'TGT HSDT Final allocation statement for Apr 2026', NULL, @PID_O10527, @DID_O10527, @DocSeedUserId, NULL, NULL, '2026-08-11 13:56:24', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1494/HV/FIN')
BEGIN
  DECLARE @PID_O10528 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP3');
  DECLARE @DID_O10528 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @PID_O10528 IS NOT NULL AND @DID_O10528 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1494/HV/FIN', 'OUTGOING', 'Distributed', N'Approval Request for Utilizing remaining CNV-5X well budget for additional Nitrogen kick-off Operation (N2)', NULL, @PID_O10528, @DID_O10528, @DocSeedUserId, NULL, '2026-08-11', '2026-08-11 14:19:22', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1495/HL/C&P')
BEGIN
  DECLARE @PID_O10529 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'TLJOC');
  DECLARE @DID_O10529 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10529 IS NOT NULL AND @DID_O10529 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1495/HL/C&P', 'OUTGOING', 'Draft', N'Purchasing Explosives for TGT-20X Well in 2026', NULL, @PID_O10529, @DID_O10529, @DocSeedUserId, NULL, NULL, '2026-08-11 15:14:46', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1496/HLHV/C&P')
BEGIN
  DECLARE @PID_O10530 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'O.S Offshore');
  DECLARE @DID_O10530 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10530 IS NOT NULL AND @DID_O10530 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1496/HLHV/C&P', 'OUTGOING', 'Draft', N'Subject: 	Side Letter for Updated Bank Account DetailsrnAgreement Title:  	Provision of Tubular Maintenance and Inspection Services rnAgreement No.:	HLHV-DRL-24-014 rn', NULL, @PID_O10530, @DID_O10530, @DocSeedUserId, NULL, NULL, '2026-08-11 17:13:25', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1498/HL/FIN')
BEGIN
  DECLARE @PID_O10532 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP4');
  DECLARE @DID_O10532 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @PID_O10532 IS NOT NULL AND @DID_O10532 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1498/HL/FIN', 'OUTGOING', 'Draft', N'HLJOC - Rà soát cập nhật triển khai đầu tư, giải ngân năm 2026 và kế hoạch năm 2027', NULL, @PID_O10532, @DID_O10532, @DocSeedUserId, NULL, NULL, '2026-08-12 14:13:36', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1499/HV/FIN')
BEGIN
  DECLARE @PID_O10533 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVEP3');
  DECLARE @DID_O10533 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @PID_O10533 IS NOT NULL AND @DID_O10533 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1499/HV/FIN', 'OUTGOING', 'Draft', N'HVJOC - Rà soát cập nhật triển khai đầu tư, giải ngân năm 2026 và kế hoạch năm 2027', NULL, @PID_O10533, @DID_O10533, @DocSeedUserId, NULL, NULL, '2026-08-12 14:14:35', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1500/HL/C&P')
BEGIN
  DECLARE @PID_O10534 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10534 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10534 IS NOT NULL AND @DID_O10534 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1500/HL/C&P', 'OUTGOING', 'Draft', N'Request for Approval for Award Recommendation for Provision of Vessel  Services for TGT-20X Well - Update on Negotiation Result rn	(Contract No. HLHV-DRL-25-030A-A08 & HLHV-DRL-25-030B-A05)rn', NULL, @PID_O10534, @DID_O10534, @DocSeedUserId, NULL, NULL, '2026-08-12 14:27:16', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-1503/HL/C&P')
BEGIN
  DECLARE @PID_O10537 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM partner.Partners WHERE ShortName = N'PVN & PVEP');
  DECLARE @DID_O10537 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @PID_O10537 IS NOT NULL AND @DID_O10537 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-1503/HL/C&P', 'OUTGOING', 'Draft', N'Request for Approval for Additional Cost under Provision of Helicopter Services for TGT Well in 2025-2026 Drilling Campaign – Reminder to PVEP', NULL, @PID_O10537, @DID_O10537, @DocSeedUserId, NULL, NULL, '2026-08-12 16:28:35', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-08-0079/INT/HLHV/C&P')
BEGIN
  DECLARE @DID_I354 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'C&P');
  IF @DID_I354 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-08-0079/INT/HLHV/C&P', 'INTERNAL', 'Draft', N'Subject: 	Bid Clarification No. 04 rnRFP Title:  	Supply of Wellsite Geologist Services for 2025-2026 TGT & CNV Drilling CampaignsrnRFP No.: HLHV-DRL-25-080rn', N'Ocean Invest', NULL, @DID_I354, @DocSeedUserId, NULL, NULL, '2025-08-06 10:19:45', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-08-0080/INT/HLHV/FIN')
BEGIN
  DECLARE @DID_I355 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @DID_I355 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-08-0080/INT/HLHV/FIN', 'INTERNAL', 'Distributed', N'Subject: Monthly Average Dated Brent Price in July 2025', NULL, NULL, @DID_I355, @DocSeedUserId, NULL, '2025-08-07', '2025-08-07 14:33:45', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-08-0081/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I356 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I356 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-08-0081/INT/HLHV/ADM', 'INTERNAL', 'Draft', N'Mobile phone device financial support guideline.', N'With the above justification, it is proposed to revise the mobile phone device support issue with the following principles:rn•	Senior staff (including GM, DGM, Managers, Dty Managers and Senior Staff and above) shall remain eligible for the mobile phone device financial support (upon Management’s approval).rn•	The cost limit of USD 400 for a mobile phone device shall be treated as a one-time financial support, not as a purchase of company property.rn•	The mobile phone shall remain the property of the staff member, not the Company.rn•	A valid invoice must be submitted to Finance for reimbursement under a staff claim process.rnrnThis guidline shall be applicable to those who have already received financial support prior to and after the issuance date of this new regulation.rn', NULL, @DID_I356, @DocSeedUserId, NULL, NULL, '2025-08-08 09:49:36', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-08-0082/INT/HL/ADM')
BEGIN
  DECLARE @DID_I357 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I357 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-08-0082/INT/HL/ADM', 'INTERNAL', 'Distributed', N'Guideline and Regulation on Taxi Card Usage and Administration', N'The purpose of the guidelines is:rn-	To have common understanding amongst all staff for the right purpose of the taxi service.rn-	To assist staff and departments in using and managing the taxi service properly and effectively.rn-	To define roles and responsibilities of each staff and department when using the taxi service.rn-	To assist in managing budget and cost of the taxi servicernrnThe guidelines is outlined in the attached document.rnrnFurthermore, under 2025 WP&B, practical use of the service across departments, and available services from taxi service providers, it is recommended to allocate the cost to departments as follows:rnManagement	2	2	 2,000,000 rnSubsurface	1	1	 2,000,000 rnProduction	2		 2,000,000 rnDrilling	2		 2,000,000 rnProject	2		 2,000,000 rnCon.& Pro.	2		 2,000,000 rnFinance	3		 2,000,000 rnAdministration (*)	3	2	 2,000,000 rnHSE team	1		 2,000,000 rn', NULL, @DID_I357, @DocSeedUserId, NULL, '2025-08-08', '2025-08-08 09:52:42', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-08-0083/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I358 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I358 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-08-0083/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'2025 Annual Health Check Program', NULL, NULL, @DID_I358, @DocSeedUserId, NULL, '2025-08-18', '2025-08-13 09:33:14', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-08-0084/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I359 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I359 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-08-0084/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Biên bản Hội nghị Người lao động 2025', NULL, NULL, @DID_I359, @DocSeedUserId, NULL, '2025-08-14', '2025-08-14 08:18:51', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-08-0085/INT/HLHV/SUB')
BEGIN
  DECLARE @DID_I360 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @DID_I360 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-08-0085/INT/HLHV/SUB', 'INTERNAL', 'Distributed', N'Relating cost for the well path revision for TGT-18X and CNV-8P Workshop', NULL, NULL, @DID_I360, @DocSeedUserId, NULL, '2025-08-19', '2025-08-19 13:22:36', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-08-0086/INT/HV/ADM')
BEGIN
  DECLARE @DID_I361 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I361 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-08-0086/INT/HV/ADM', 'INTERNAL', 'Distributed', N'Ceremony for 25th Anniversary of Hoan Vu Company', NULL, NULL, @DID_I361, @DocSeedUserId, NULL, '2025-08-07', '2025-08-19 14:07:44', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-08-0087/INT/HV/ADM')
BEGIN
  DECLARE @DID_I362 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I362 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-08-0087/INT/HV/ADM', 'INTERNAL', 'Distributed', N'Press and Communication on 25 years of Hoan Vu JOC', NULL, NULL, @DID_I362, @DocSeedUserId, NULL, '2025-08-13', '2025-08-19 16:10:02', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-08-0088/INT/HL/PRD')
BEGIN
  DECLARE @DID_I363 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @DID_I363 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-08-0088/INT/HL/PRD', 'INTERNAL', 'Distributed', N'Compassionate Allowance  & Support', NULL, NULL, @DID_I363, @DocSeedUserId, NULL, '2025-08-20', '2025-08-20 15:48:47', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-08-0089/INT/HLHV/SUB')
BEGIN
  DECLARE @DID_I364 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @DID_I364 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-08-0089/INT/HLHV/SUB', 'INTERNAL', 'Distributed', N'Relating cost for the HLHV annual TCMs', NULL, NULL, @DID_I364, @DocSeedUserId, NULL, '2025-09-09', '2025-08-21 09:22:41', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-08-0090/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I365 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I365 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-08-0090/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Donation Activities – 3rd Quarter 2025 – No.3', NULL, NULL, @DID_I365, @DocSeedUserId, NULL, '2025-08-26', '2025-08-21 17:12:21', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0091/INT/HLHV/FIN')
BEGIN
  DECLARE @DID_I366 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @DID_I366 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0091/INT/HLHV/FIN', 'INTERNAL', 'Distributed', N'Subject: Monthly Average Dated Brent Price in August 2025', NULL, NULL, @DID_I366, @DocSeedUserId, NULL, '2025-09-04', '2025-09-04 13:54:54', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0092/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I367 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I367 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0092/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Proposal for staff training course on "Tendering and Legal Aspects of Tendering"', NULL, NULL, @DID_I367, @DocSeedUserId, NULL, '2025-09-05', '2025-09-08 11:37:31', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0093/INT/HV/ADM')
BEGIN
  DECLARE @DID_I368 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I368 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0093/INT/HV/ADM', 'INTERNAL', 'Distributed', N'Company Present & 25th Anniversary of Hoan Vu JOC', NULL, NULL, @DID_I368, @DocSeedUserId, NULL, '2025-09-09', '2025-09-09 13:15:12', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0094/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I369 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I369 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0094/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'2025 Pre - Management Committee Meetings (Pre-MCMs) for Block 16-1 & 09-2', N'1.	Date of meetings: 30th & 31st Oct. 2025rn2.	Venue: HLHVJOCs’ Office, Ho Chi Minh Cityrn3.	Participant: Estimated 50 pax (representative of PVN/Partners and HLHVJOCs staff)rn4.	Cost Estimation for 2025 Pre MCM (details in the attached table)   : 15,065.08', NULL, @DID_I369, @DocSeedUserId, NULL, '2025-09-09', '2025-09-09 15:11:18', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0095/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I370 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I370 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0095/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'2025 Management Committee Meetings for Block 16-1 & 09-2', N'SOW required for the MCM, as follows: rn1.	Date of meetings & venue: 20th & 21st November 2025rn2.	Participant: Estimated 50 pax (representatives of PVN/Partners and staff)rn3.	Estimated total, included cost return air tickets & Logistics arrangement for MCM: USD 49,839.46 rn•	Air tickets shall be arranged by Admin dept. via current air ticket contracts/ agents. The number of air tickets is based on approval TR and confimation.rn•	Logistics arrangement (land transportation, meals, logistics, ect) shall be obtained by Supplier via procurement process with SOW attached.rn', NULL, @DID_I370, @DocSeedUserId, NULL, '2025-09-09', '2025-09-09 15:13:03', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0096/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I371 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I371 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0096/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Decision on the Establishment of the Donation Committeern', NULL, NULL, @DID_I371, @DocSeedUserId, NULL, '2025-09-18', '2025-09-12 12:54:18', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0097/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I372 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I372 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0097/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Office cleansing: Re-location old or Damaged Equipment, and utilize warehouse.', N'•	Office Cleansing: Adm Dept will hire a transportation service provider to move old and damaged equipment to the VTB warehouse. rn•	ADM will coordinate with the Drilling Department to make use of approximately 20 – 30 sqm of space at the warehouse currently rented for the Drilling Projects 2025 & 2026. This space will be used for a period of 6 to 9 months to store old and damaged equipment pending disposal.', NULL, @DID_I372, @DocSeedUserId, NULL, '2025-12-09', '2025-09-12 15:57:43', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0098/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I373 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I373 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0098/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Recreational Sport Activity 2025 ( additional)', N'Following a period of practice, the number of participating employees has increased. However, the signed contract with the service provider does not include the cost of hiring trainers/instructors. Additionally, the Labour Union has proposed supplementing the number of playing court and providing practice balls, with costs aligned to the budget allocated for this sports activity.rnLabor Union propose:rn•	Rent 1 new court. 						31,500,000 VNDrn•	Practice balls 							  3,840,000 VNDrn•	Coaching services						12,800,000 VNDrnTotal in VND							46,220,000 VNDrnTotal In USD							         1,840 USDrn', NULL, @DID_I373, @DocSeedUserId, NULL, '2025-09-13', '2025-09-12 17:21:53', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0099/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I374 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I374 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0099/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Compliance with VN Labor Code on payment for working overtime', NULL, NULL, @DID_I374, @DocSeedUserId, NULL, '2025-09-19', '2025-09-18 08:59:10', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0100/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I375 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I375 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0100/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Manpower support to HLHVJOC''s 2025-2026 drilling campaigns - Mr. Do Van Hien', NULL, NULL, @DID_I375, @DocSeedUserId, NULL, '2025-09-22', '2025-09-22 13:37:42', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0101/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I376 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I376 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0101/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Provision of Office Equipment and Electrical Appliances', NULL, NULL, @DID_I376, @DocSeedUserId, NULL, '2025-09-17', '2025-09-22 15:27:59', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0102/INT/HL/ADM')
BEGIN
  DECLARE @DID_I377 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I377 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0102/INT/HL/ADM', 'INTERNAL', 'Distributed', N'Extension the iBHXH I-VAN C Software with TS24 for Hoang Long JOC', NULL, NULL, @DID_I377, @DocSeedUserId, NULL, '2025-09-23', '2025-09-23 09:55:05', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0103/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I378 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I378 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0103/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Plant rental service for offices from Nov 2025 to Oct 2027', N'The JOCs typically maintain around 70 small plants placed on employees’ desks and approximately 50 larger plants positioned in corners and open areas.rnIn addition to the regular office plants, and in line with the festive tradition of welcoming the Lunar New Year, HLHVJOCs annually rent hoa mai trees to be placed at the entrances of the offices on the 16th and 22nd floors. This practice is considered necessary to maintain every year.rnVoi Con Company has been the long-term supplier of plant rental services for HLHVJOCs, with stable costs over the years. They also provide services to other companies such as PVEP POC, PVEP, and Cuu Long. Their team of professional and dedicated staff ensures proper plant care and is always ready to replace plants when required. The plants are consistently kept fresh and well-maintained.rnThe cost for this activity is based on the current contract number: HLHV-ADM-18-091-A03 (a detailed breakdown is attached in the SOW).rn', NULL, @DID_I378, @DocSeedUserId, NULL, '2025-09-12', '2025-09-23 14:25:37', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0104/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I379 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I379 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0104/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'AM :authorized Document to the payment of expenses claims', N'Administration Manager to sign, for and on behalf of the General Manager, the document to the payment of expenses claims for the monthly mobile phone charge of those paid by the Companies with effect from 01st Sept 2025 until further notice.', NULL, @DID_I379, @DocSeedUserId, NULL, '2025-09-01', '2025-09-23 14:37:10', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0105/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I380 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I380 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0105/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Petroleum Contract Extension Celebration Event', NULL, NULL, @DID_I380, @DocSeedUserId, NULL, '2025-09-23', '2025-09-23 14:55:41', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0106/INT/HL/ADM')
BEGIN
  DECLARE @DID_I381 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I381 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0106/INT/HL/ADM', 'INTERNAL', 'Distributed', N'Manpower  supply to HLHVJOCs'' 2025-2026 drilling Caimpaign (PVEP)', NULL, NULL, @DID_I381, @DocSeedUserId, NULL, '2025-09-24', '2025-09-24 15:07:18', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0107/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I382 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I382 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0107/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Mid-Autumn Festival Voucher (2025) for Staffs', NULL, NULL, @DID_I382, @DocSeedUserId, NULL, '2025-09-25', '2025-09-25 16:56:26', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-09-0108/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I383 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I383 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-09-0108/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Pet control services', N'Pet control , 1 time for 2 months, rodent trap call out', NULL, @DID_I383, @DocSeedUserId, NULL, '2025-09-23', '2025-09-29 10:54:10', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-10-0109/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I384 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I384 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-10-0109/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Support Mobile Phone', NULL, NULL, @DID_I384, @DocSeedUserId, NULL, '2025-10-01', '2025-10-01 09:15:34', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-10-0110/INT/HLHV/SUB')
BEGIN
  DECLARE @DID_I385 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @DID_I385 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-10-0110/INT/HLHV/SUB', 'INTERNAL', 'Draft', N'Awarding scholarships to poor and unlucky Petroleum Geology students', NULL, NULL, @DID_I385, @DocSeedUserId, NULL, NULL, '2025-10-02 09:31:29', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-10-0111/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I386 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I386 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-10-0111/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Staff Birthday Gifts in Q3,  Women''s Day and Men''s Day 2025', NULL, NULL, @DID_I386, @DocSeedUserId, NULL, '2025-10-02', '2025-10-02 13:48:45', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-10-0112/INT/HLHV/FIN')
BEGIN
  DECLARE @DID_I387 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @DID_I387 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-10-0112/INT/HLHV/FIN', 'INTERNAL', 'Distributed', N'Subject: Monthly Average Dated Brent Price in Sep 2025', NULL, NULL, @DID_I387, @DocSeedUserId, NULL, '2025-10-07', '2025-10-07 09:54:35', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-10-0113/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I388 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I388 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-10-0113/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Relocation services  Mr Atchariyawit', N'PTTEP Hoang Long Company Limited has notified that the assignment of Mr. Atchariyawit Prayoonhong at HLHVJOC will end on 31 December 2025.rnrnMr. Atchariyawit has informed the Administration Department and requested HLHVJOC’s support for the transportation of his family’s personal belongings to Thailand.rnrnThe shipment includes personal effects, used electronic equipment, musical instruments, etc..rnrnThe packing date is scheduled for 22nd December 2025.rn', NULL, @DID_I388, @DocSeedUserId, NULL, '2025-10-21', '2025-10-21 14:22:34', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-10-0114/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I389 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I389 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-10-0114/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Photocopier Rental Service', NULL, NULL, @DID_I389, @DocSeedUserId, NULL, '2025-10-21', '2025-10-22 15:09:11', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-10-0115/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I390 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I390 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-10-0115/INT/HLHV/ADM', 'INTERNAL', 'Draft', N'Purchase printer ink cartridges - urgent!', NULL, NULL, @DID_I390, @DocSeedUserId, NULL, NULL, '2025-10-27 10:53:25', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-10-0116/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I391 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I391 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-10-0116/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Donation Activities – 4th Quarter 2025 – No.1', NULL, NULL, @DID_I391, @DocSeedUserId, NULL, '2025-11-03', '2025-10-27 15:52:04', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'25-10-0117/INT/HL/ADM')
BEGIN
  DECLARE @DID_I392 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I392 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'25-10-0117/INT/HL/ADM', 'INTERNAL', 'Distributed', N'Announcement of Labor Contract Termination', NULL, NULL, @DID_I392, @DocSeedUserId, NULL, '2025-10-28', '2025-10-28 13:13:06', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-01-0012/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I442 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I442 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-01-0012/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Logistics Arrangement for Tet occasion 2026', N'In order to meet the working schedule and the work requirement, the followings are prepared and proposed:rn1.	Company Public Relations: Based on the approved list of organizations, companies, and agencies that have maintained strong relationships and provided significant support to the JOCs during the year, it is recommended to present vouchers, flowers, and small gifts as a token of appreciation from the JOCs and the Contracting Parties. rn2.	Office flower Services: To add two large flower arrangements for Tet decoration to welcome the Lunar New Year.rnrnrnrn3.	Lucky Money for outsourced personnel, tea ladies, drivers, and long-term servicing contractorsrnIn accordance with Vietnamese tradition during the Tet holiday and the approved 2026 WP&B, it is recommended that Management extend New Year wishes to employees on the first working day of the Lunar New Year by providing lucky money in red envelopes. The attached list of recipients includes all employees, office service contractors’ staff (outsourced personnel, tea ladies, drivers, and long-term servicing contractors). The estimated value is detailed in the attached table.rn4.	Other Consumables for Tet Occasionrn-	Confectionery, fresh fruits, cakes, meat for Tet holiday.rn-	On the last working day of the current year and first working day of the Lunar New Year, it is recommended that a small office gathering be organized. (Detail of these activities is on SOW.rn', NULL, @DID_I442, @DocSeedUserId, NULL, '2026-01-20', '2026-01-20 09:37:54', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-01-0013/INT/HLHV/HSE')
BEGIN
  DECLARE @DID_I443 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I443 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-01-0013/INT/HLHV/HSE', 'INTERNAL', 'Distributed', N'Organisation of HSE Day in 2026', NULL, NULL, @DID_I443, @DocSeedUserId, NULL, '2026-02-03', '2026-01-22 13:57:08', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-01-0014/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I444 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I444 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-01-0014/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Donation Activities – 1st Quarter 2026 – No.2 – Dien Ha Commune', NULL, NULL, @DID_I444, @DocSeedUserId, NULL, '2026-01-29', '2026-01-23 16:37:12', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-01-0015/INT/HL/ADM')
BEGIN
  DECLARE @DID_I445 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I445 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-01-0015/INT/HL/ADM', 'INTERNAL', 'Distributed', N'Thông báo chấm dứt HĐLĐ  - Lê Anh Hào', NULL, NULL, @DID_I445, @DocSeedUserId, NULL, '2026-01-29', '2026-01-29 08:58:37', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-01-0016/INT/HL/SUB')
BEGIN
  DECLARE @DID_I446 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @DID_I446 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-01-0016/INT/HL/SUB', 'INTERNAL', 'Distributed', N'Relating cost TGT H3 Hydrocarbons Potential Review Workshop on 5th Feb 2026', NULL, NULL, @DID_I446, @DocSeedUserId, NULL, '2026-01-29', '2026-01-29 14:00:20', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-02-0017/INT/HLHV/FIN')
BEGIN
  DECLARE @DID_I447 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @DID_I447 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-02-0017/INT/HLHV/FIN', 'INTERNAL', 'Distributed', N'Subject: Monthly Average Dated Brent Price in January 2026', NULL, NULL, @DID_I447, @DocSeedUserId, NULL, '2026-02-02', '2026-02-02 15:11:21', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-02-0018/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I448 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I448 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-02-0018/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Donation Activities – 1st Quarter 2026 – No.3 – Study Promotion Association of Thanh Hoa Province', NULL, NULL, @DID_I448, @DocSeedUserId, NULL, '2026-02-04', '2026-02-03 11:15:19', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-02-0019/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I449 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I449 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-02-0019/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'2025 Performance Appraisal and Bonus Program', NULL, NULL, @DID_I449, @DocSeedUserId, NULL, '2026-02-04', '2026-02-03 19:50:49', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-02-0020/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I450 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I450 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-02-0020/INT/HLHV/ADM', 'INTERNAL', 'Draft', N'QĐ vv Mức độ HTNV đối với tập thể/cá nhân tại HLHVJOC, phục vụ Công tác TDKT 2025', NULL, NULL, @DID_I450, @DocSeedUserId, NULL, NULL, '2026-02-05 11:35:45', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0021/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I451 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I451 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0021/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'TET gift voucher for PVEP Secondee.', NULL, NULL, @DID_I451, @DocSeedUserId, NULL, '2026-02-26', '2026-03-02 10:25:33', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0022/INT/HL/ADM')
BEGIN
  DECLARE @DID_I452 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I452 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0022/INT/HL/ADM', 'INTERNAL', 'Distributed', N'International Women’s Day 2026', NULL, NULL, @DID_I452, @DocSeedUserId, NULL, '2026-03-02', '2026-03-02 16:38:47', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0023/INT/HV/DRL')
BEGIN
  DECLARE @DID_I453 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'DRL');
  IF @DID_I453 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0023/INT/HV/DRL', 'INTERNAL', 'Distributed', N'Technical Meeting with PVEP Team to present the CNV-5X Drilling Program', NULL, NULL, @DID_I453, @DocSeedUserId, NULL, '2026-03-03', '2026-03-03 11:26:44', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0024/INT/HV/DRL')
BEGIN
  DECLARE @DID_I454 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'DRL');
  IF @DID_I454 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0024/INT/HV/DRL', 'INTERNAL', 'Distributed', N'2026 Pre-spud Meeting for Well CNV-5X', NULL, NULL, @DID_I454, @DocSeedUserId, NULL, '2026-03-05', '2026-03-05 10:19:02', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0025/INT/HV/DRL')
BEGIN
  DECLARE @DID_I455 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'DRL');
  IF @DID_I455 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0025/INT/HV/DRL', 'INTERNAL', 'Draft', N'Pre-spud Meeting for Well CNV_5X', NULL, NULL, @DID_I455, @DocSeedUserId, NULL, NULL, '2026-03-05 15:43:30', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0026/INT/HLHV/FIN')
BEGIN
  DECLARE @DID_I456 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @DID_I456 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0026/INT/HLHV/FIN', 'INTERNAL', 'Distributed', N'Subject: Monthly Average Dated Brent Price in February 2026', NULL, NULL, @DID_I456, @DocSeedUserId, NULL, '2026-03-06', '2026-03-06 15:57:14', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0027/INT/HL/ADM')
BEGIN
  DECLARE @DID_I457 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I457 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0027/INT/HL/ADM', 'INTERNAL', 'Distributed', N'Extend 12 months for SAFE -CA Digital Signature for Hoang Long JOC', NULL, NULL, @DID_I457, @DocSeedUserId, NULL, '2026-03-10', '2026-03-10 12:39:53', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0028/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I458 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I458 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0028/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Periodic Minor office Repairs & Office Repair and Rearrangement for Additional Workstations', N'1. Periodic Minor office Repairs.rnrnIn Jan 2026, The ADM team has conducted a review and identified several items requiring minor repairs on the 16th and 20th floors, as listed in the attached document. The detailed scope of work and the estimated costs, based on unit rates from previous similar works, are listed in Attachment #1.rnrn⸻rnrn2. Office Repair and Rearrangement for Additional Workstations.rnrnFollowing the request from the SFF Department to accommodate four additional personnel expected to join during March and early April 2026, the Administration Department has arranged workspace for two personnel using currently available vacant positions.rnrnHowever, workspace for the remaining two personnel is still required. Based on an assessment of the office space and its suitability for additional workstations, the Administration Department proposes to design and install two additional workstations, as illustrated in the attached layout.rnrnThis work has been preliminarily designed and cost-estimated by Quoc Hoang Contractor, as detailed in Attachment #2.', NULL, @DID_I458, @DocSeedUserId, NULL, '2026-03-10', '2026-03-10 13:54:18', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0029/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I459 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I459 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0029/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Recommendation for Recognition of Initiative Titled “Integrated Reservoir–Surface Network Modeling for Back Pressure Evaluation in CNV Field”', NULL, NULL, @DID_I459, @DocSeedUserId, NULL, '2026-03-15', '2026-03-10 19:52:33', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0030/INT/HLHV/SUB')
BEGIN
  DECLARE @DID_I460 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @DID_I460 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0030/INT/HLHV/SUB', 'INTERNAL', 'Distributed', N'Relating cost for Blocks 16-1 & 09-2 seismic interpretation and TGT-18X & CNV-8P well result review Workshop', NULL, NULL, @DID_I460, @DocSeedUserId, NULL, '2026-03-11', '2026-03-11 16:06:18', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0031/INT/HLHV/HSE')
BEGIN
  DECLARE @DID_I461 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I461 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0031/INT/HLHV/HSE', 'INTERNAL', 'Distributed', N'QUYẾT ĐỊNH THÀNH LẬP ĐỘI NỔ MÌN', NULL, NULL, @DID_I461, @DocSeedUserId, NULL, '2026-03-23', '2026-03-23 13:08:08', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0032/INT/HLHV/HSE')
BEGIN
  DECLARE @DID_I462 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I462 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0032/INT/HLHV/HSE', 'INTERNAL', 'Distributed', N'quyết định giao nhiệm vụ quản lý  chuyên môn, kỹ thuật, an toàn trong hoạt động VLNCN', NULL, NULL, @DID_I462, @DocSeedUserId, NULL, '2026-03-23', '2026-03-23 13:10:23', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0033/INT/HL/HSE')
BEGIN
  DECLARE @DID_I463 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I463 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0033/INT/HL/HSE', 'INTERNAL', 'Distributed', N'Quyết định giao nhiệm vụ chỉ huy nổ mìn', NULL, NULL, @DID_I463, @DocSeedUserId, NULL, '2026-03-23', '2026-03-23 13:12:56', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0034/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I464 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I464 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0034/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Donation Activities – 1st Quarter 2026 – No.4 – Than Khe Commune Study Promotion Associationrn', NULL, NULL, @DID_I464, @DocSeedUserId, NULL, '2026-03-30', '2026-03-25 16:43:51', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0035/INT/HV/HSE')
BEGIN
  DECLARE @DID_I465 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I465 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0035/INT/HV/HSE', 'INTERNAL', 'Distributed', N'Quyết định về việc ban hành Biện pháp phòng ngừa ứng phó sự cố hóa chất cho hoạt động dầu khí tại mỏ Cá Ngừ Vàngrn', NULL, NULL, @DID_I465, @DocSeedUserId, NULL, '2026-03-26', '2026-03-26 08:53:15', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0036/INT/HL/HSE')
BEGIN
  DECLARE @DID_I466 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I466 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0036/INT/HL/HSE', 'INTERNAL', 'Distributed', N'Quyết định về việc ban hành Biện pháp phòng ngừa ứng phó sự cố hóa chất cho hoạt động dầu khí tại mỏ Tê Giác Trắngrn', NULL, NULL, @DID_I466, @DocSeedUserId, NULL, '2026-03-26', '2026-03-26 08:54:19', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-03-0037/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I467 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I467 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-03-0037/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'2026 R&R Allowance', NULL, NULL, @DID_I467, @DocSeedUserId, NULL, '2026-04-01', '2026-03-30 18:14:21', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-04-0038/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I468 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I468 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-04-0038/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Promotion and JD Revision for DH of SSF', NULL, NULL, @DID_I468, @DocSeedUserId, NULL, '2026-04-01', '2026-04-01 09:28:24', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-04-0039/INT/HLHV/FIN')
BEGIN
  DECLARE @DID_I469 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @DID_I469 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-04-0039/INT/HLHV/FIN', 'INTERNAL', 'Distributed', N'Subject: Monthly Average Dated Brent Price in March 2026', NULL, NULL, @DID_I469, @DocSeedUserId, NULL, '2026-04-03', '2026-04-03 09:03:20', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-04-0040/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I470 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I470 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-04-0040/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Happy Hour & Happy Friday Event - April 2026', NULL, NULL, @DID_I470, @DocSeedUserId, NULL, '2026-04-01', '2026-04-07 08:21:34', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-04-0041/INT/HL/ADM')
BEGIN
  DECLARE @DID_I471 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I471 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-04-0041/INT/HL/ADM', 'INTERNAL', 'Distributed', N'Notice of  Mr. Nguyen Viet Dung''s LC Termination', NULL, NULL, @DID_I471, @DocSeedUserId, NULL, '2026-04-07', '2026-04-07 15:02:14', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-04-0042/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I472 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I472 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-04-0042/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Encashment of 2025 Unused Annual Leave Days for Production Offshore Staff', NULL, NULL, @DID_I472, @DocSeedUserId, NULL, '2026-04-17', '2026-04-10 10:23:58', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-04-0043/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I473 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I473 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-04-0043/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Labour Conference & Tree Planting 2026', NULL, NULL, @DID_I473, @DocSeedUserId, NULL, '2026-04-06', '2026-04-10 11:20:49', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-04-0044/INT/HL/HSE')
BEGIN
  DECLARE @DID_I474 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I474 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-04-0044/INT/HL/HSE', 'INTERNAL', 'Distributed', N'Giao nhiệm vụ Người quản lý trong lĩnh vực vật liệu nổ công nghiệp', NULL, NULL, @DID_I474, @DocSeedUserId, NULL, '2026-04-16', '2026-04-16 09:08:10', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-04-0045/INT/HL/SUB')
BEGIN
  DECLARE @DID_I475 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @DID_I475 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-04-0045/INT/HL/SUB', 'INTERNAL', 'Distributed', N'Relating cost for TGT-H3 Additional Exploration Program Workshop', NULL, NULL, @DID_I475, @DocSeedUserId, NULL, '2026-04-16', '2026-04-16 11:04:16', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-04-0046/INT/HLHV/HSE')
BEGIN
  DECLARE @DID_I476 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I476 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-04-0046/INT/HLHV/HSE', 'INTERNAL', 'Distributed', N'Sponsor for the Propaganda Campaign 2026 on Enhancing Offshore Oil & Gas Safety & Security', NULL, NULL, @DID_I476, @DocSeedUserId, NULL, '2026-04-17', '2026-04-17 08:51:56', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-04-0047/INT/HLHV/PRD')
BEGIN
  DECLARE @DID_I477 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'PRD');
  IF @DID_I477 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-04-0047/INT/HLHV/PRD', 'INTERNAL', 'Draft', N'2026 Offshore Technology Conference', NULL, NULL, @DID_I477, @DocSeedUserId, NULL, NULL, '2026-04-20 10:14:11', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-04-0048/INT/HL/HSE')
BEGIN
  DECLARE @DID_I478 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I478 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-04-0048/INT/HL/HSE', 'INTERNAL', 'Distributed', N'Quyết định kiện toàn Ban Hệ thống Quản lý Môi trường rn', NULL, NULL, @DID_I478, @DocSeedUserId, NULL, '2026-05-04', '2026-04-29 15:20:26', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-04-0049/INT/HV/HSE')
BEGIN
  DECLARE @DID_I479 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I479 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-04-0049/INT/HV/HSE', 'INTERNAL', 'Distributed', N'Quyết định kiện toàn Ban Hệ thống Quản lý Môi trường', NULL, NULL, @DID_I479, @DocSeedUserId, NULL, '2026-05-04', '2026-04-29 15:21:32', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-05-0050/INT/HLHV/FIN')
BEGIN
  DECLARE @DID_I480 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @DID_I480 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-05-0050/INT/HLHV/FIN', 'INTERNAL', 'Distributed', N'Subject: Monthly Average Dated Brent Price in April 2026', NULL, NULL, @DID_I480, @DocSeedUserId, NULL, '2026-05-04', '2026-05-04 14:16:47', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-05-0051/INT/HL/ADM')
BEGIN
  DECLARE @DID_I481 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I481 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-05-0051/INT/HL/ADM', 'INTERNAL', 'Distributed', N'Notice the termination of  Labor Contract with Ms. TNP.Trang', NULL, NULL, @DID_I481, @DocSeedUserId, NULL, '2026-05-18', '2026-05-18 15:44:41', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-05-0052/INT/HLHV/HSE')
BEGIN
  DECLARE @DID_I482 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I482 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-05-0052/INT/HLHV/HSE', 'INTERNAL', 'Draft', N'Quyết định kiện toàn Tổ An toàn Vệ sinh viên & Đội phòng cháy, chữa cháy và cứu nạn, cứu hộ cơ sởrn', NULL, NULL, @DID_I482, @DocSeedUserId, NULL, NULL, '2026-05-19 14:43:31', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-05-0053/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I483 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I483 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-05-0053/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Children Day 01/06 & Staff Birthday Gifts in Q2 & Q3/ 2026', NULL, NULL, @DID_I483, @DocSeedUserId, NULL, '2026-05-18', '2026-05-19 15:30:36', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-06-0054/INT/HL/SUB')
BEGIN
  DECLARE @DID_I484 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @DID_I484 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-06-0054/INT/HL/SUB', 'INTERNAL', 'Distributed', N'Relating cost for the Meeting of Supplemental Hydrocarbon Initially In Place & Reserve Assessment Report for the H5W Fault Block of TGT Field on 11 June 2026', NULL, NULL, @DID_I484, @DocSeedUserId, NULL, '2026-06-02', '2026-06-01 09:03:48', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-06-0055/INT/HLHV/FIN')
BEGIN
  DECLARE @DID_I485 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @DID_I485 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-06-0055/INT/HLHV/FIN', 'INTERNAL', 'Distributed', N'Subject: Monthly Average Dated Brent Price in May 2026', NULL, NULL, @DID_I485, @DocSeedUserId, NULL, '2026-06-04', '2026-06-03 14:59:40', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-06-0056/INT/HL/ADM')
BEGIN
  DECLARE @DID_I486 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I486 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-06-0056/INT/HL/ADM', 'INTERNAL', 'Distributed', N'Notice of LC Termination - Mr. Hoang Le Nguyen - Accountant', NULL, NULL, @DID_I486, @DocSeedUserId, NULL, '2026-06-05', '2026-06-04 13:24:01', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-06-0057/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I487 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I487 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-06-0057/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Donation Activities - 2nd Q2 2026', NULL, NULL, @DID_I487, @DocSeedUserId, NULL, '2026-06-10', '2026-06-05 09:19:55', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-06-0058/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I488 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I488 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-06-0058/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Payday Revision Effective from June 2026 Payroll Period', NULL, NULL, @DID_I488, @DocSeedUserId, NULL, '2026-06-10', '2026-06-10 13:37:03', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-06-0059/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I489 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I489 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-06-0059/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'2026 Salary Review and Adjustment for HLHVJOCs Direct Hire Staff', NULL, NULL, @DID_I489, @DocSeedUserId, NULL, '2026-06-14', '2026-06-10 15:54:44', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-06-0060/INT/HL/HSE')
BEGIN
  DECLARE @DID_I490 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I490 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-06-0060/INT/HL/HSE', 'INTERNAL', 'Draft', N'Quyết định giao nhiệm vụ Người quản lý trong lĩnh vực vật liệu nổ công nghiệp rn', NULL, NULL, @DID_I490, @DocSeedUserId, NULL, NULL, '2026-06-17 14:34:10', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-06-0061/INT/HV/HSE')
BEGIN
  DECLARE @DID_I491 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I491 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-06-0061/INT/HV/HSE', 'INTERNAL', 'Draft', N'Quyết định giao nhiệm vụ Người quản lý trong lĩnh vực vật liệu nổ công nghiệp rn', NULL, NULL, @DID_I491, @DocSeedUserId, NULL, NULL, '2026-06-17 14:37:40', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-06-0062/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I492 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I492 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-06-0062/INT/HLHV/ADM', 'INTERNAL', 'Draft', N'Repair audio system in meeting room.', NULL, NULL, @DID_I492, @DocSeedUserId, NULL, NULL, '2026-06-18 09:05:23', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-06-0063/INT/HLHV/HSE')
BEGIN
  DECLARE @DID_I493 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'HSE');
  IF @DID_I493 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-06-0063/INT/HLHV/HSE', 'INTERNAL', 'Draft', N'Quyết định thành lập Tổ An toàn Vệ sinh viên & Đội phòng cháy, chữa cháy và cứu nạn, cứu hộ cơ sở tại công trình biểnrn', NULL, NULL, @DID_I493, @DocSeedUserId, NULL, NULL, '2026-06-19 10:07:48', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0064/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I494 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I494 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0064/INT/HLHV/ADM', 'INTERNAL', 'Draft', N'Enhancing HLHVJOCs'' Corporate Image and Reputation through an Offshore Media Visit in 2026.', NULL, NULL, @DID_I494, @DocSeedUserId, NULL, NULL, '2026-07-01 14:42:06', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0065/INT/HLHV/FIN')
BEGIN
  DECLARE @DID_I495 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @DID_I495 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0065/INT/HLHV/FIN', 'INTERNAL', 'Distributed', N'Subject: Monthly Average Dated Brent Price in June 2026', NULL, NULL, @DID_I495, @DocSeedUserId, NULL, '2026-07-10', '2026-07-10 14:47:44', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0066/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I496 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I496 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0066/INT/HLHV/ADM', 'INTERNAL', 'Draft', N'General Manager''s Office Improvement Works', NULL, NULL, @DID_I496, @DocSeedUserId, NULL, NULL, '2026-07-13 09:44:36', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0067/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I497 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I497 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0067/INT/HLHV/ADM', 'INTERNAL', 'Distributed', N'Donation Activities – 3rd Quarter 2026', NULL, NULL, @DID_I497, @DocSeedUserId, NULL, '2026-07-08', '2026-07-13 15:13:30', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0068/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I498 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I498 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0068/INT/HLHV/ADM', 'INTERNAL', 'Draft', N'Completion and Acceptance of Offshore Media Visit and Professional Media Services in Jyly 2026 (Journalist Nguyen Nhu Phong)', N'The offshore media visit was completed from 02 to 06 July 2026. Two articles were submitted and accepted in accordance with the approved IOM. The professional service fee of VND 25,000,000 is the gross amount and is subject to applicable PIT withholding. Publication of the articles remains subject to HLHVJOCs’ final technical, confidentiality and Management review.', NULL, @DID_I498, @DocSeedUserId, NULL, NULL, '2026-07-14 08:54:14', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0069/INT/HLHV/SUB')
BEGIN
  DECLARE @DID_I499 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @DID_I499 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0069/INT/HLHV/SUB', 'INTERNAL', 'Draft', N'Awarding scholarships to poor and unlucky students', NULL, NULL, @DID_I499, @DocSeedUserId, NULL, NULL, '2026-07-21 16:23:34', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-07-0070/INT/HL/SUB')
BEGIN
  DECLARE @DID_I500 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'SUB');
  IF @DID_I500 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-07-0070/INT/HL/SUB', 'INTERNAL', 'Distributed', N'Memo for Defence Meeting on HIIP & RAR TGT Supplemental H5W on 28 Jul 2026', NULL, NULL, @DID_I500, @DocSeedUserId, NULL, '2026-07-22', '2026-07-22 10:13:43', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0071/INT/HLHV/FIN')
BEGIN
  DECLARE @DID_I501 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'FIN');
  IF @DID_I501 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0071/INT/HLHV/FIN', 'INTERNAL', 'Distributed', N'Subject: Monthly Average Dated Brent Price in July 2026', NULL, NULL, @DID_I501, @DocSeedUserId, NULL, '2026-08-04', '2026-08-04 10:43:54', 0);
END
IF NOT EXISTS (SELECT 1 FROM document.Documents WHERE DocumentNumber = N'26-08-0072/INT/HLHV/ADM')
BEGIN
  DECLARE @DID_I502 UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM auth.Departments WHERE Code = N'ADM');
  IF @DID_I502 IS NOT NULL
    INSERT INTO document.Documents (Id, DocumentNumber, DocType, Status, Title, Summary, PartnerId, SenderDepartmentId, CreatedByUserId, ReceivedAt, DistributedAt, CreatedAt, IsDeleted)
    VALUES (NEWID(), N'26-08-0072/INT/HLHV/ADM', 'INTERNAL', 'Draft', N'Additional Donation Activities – 3rd Quarter 2026  – Two (02) Donation Categories for Community and Education Purpose', NULL, NULL, @DID_I502, @DocSeedUserId, NULL, NULL, '2026-08-12 09:28:40', 0);
END

SELECT DocType, COUNT(*) AS Total FROM document.Documents GROUP BY DocType;
GO

-- ============================================================================
-- (Merged from seed_partners.sql) Partner mau bo sung cho luong test nhanh
-- Add Outgoing/Incoming Document ma khong can vao UI "Partners" tao tay.
-- An toan chay lai nhieu lan: chi INSERT neu ShortName chua ton tai.
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM [partner].[Partners] WHERE ShortName = N'VNPT')
BEGIN
    INSERT INTO [partner].[Partners]
        (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES
        (NEWID(), N'Tập đoàn Bưu chính Viễn thông Việt Nam', N'VNPT', N'Both',
         N'contact@vnpt.com.vn', NULL, N'Số 57 Huỳnh Thúc Kháng, Đống Đa, Hà Nội', NULL,
         1, 0, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000001');
END
GO

IF NOT EXISTS (SELECT 1 FROM [partner].[Partners] WHERE ShortName = N'SGDĐT TP.HCM')
BEGIN
    INSERT INTO [partner].[Partners]
        (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES
        (NEWID(), N'Sở Giáo dục và Đào tạo Thành phố Hồ Chí Minh', N'SGDĐT TP.HCM', N'Both',
         NULL, NULL, N'Thành phố Hồ Chí Minh', NULL,
         1, 0, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000001');
END
GO

IF NOT EXISTS (SELECT 1 FROM [partner].[Partners] WHERE ShortName = N'UBND TP.HCM')
BEGIN
    INSERT INTO [partner].[Partners]
        (Id, FullName, ShortName, EntityType, Email, Phone, Address, TaxCode, IsActive, IsDeleted, CreatedAt, CreatedByUserId)
    VALUES
        (NEWID(), N'Ủy ban nhân dân Thành phố Hồ Chí Minh', N'UBND TP.HCM', N'Both',
         NULL, NULL, N'Thành phố Hồ Chí Minh', NULL,
         1, 0, SYSUTCDATETIME(), '00000000-0000-0000-0000-000000000001');
END
GO

SELECT Id, FullName, ShortName, EntityType, IsActive
FROM [partner].[Partners]
ORDER BY CreatedAt DESC;
GO

PRINT N'============================================================';
PRINT N'DATABASE INITIALIZATION / SEED COMPLETED';
PRINT N'============================================================';

SELECT s.name AS SchemaName, t.name AS TableName
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE s.name IN (N'auth', N'partner', N'document', N'files', N'notification', N'emailworker')
ORDER BY s.name, t.name;

SELECT N'auth.Departments' AS [Table], COUNT(*) AS [Rows] FROM auth.Departments
UNION ALL SELECT N'auth.Roles', COUNT(*) FROM auth.Roles
UNION ALL SELECT N'auth.Users', COUNT(*) FROM auth.Users
UNION ALL SELECT N'auth.UserRoles', COUNT(*) FROM auth.UserRoles
UNION ALL SELECT N'partner.Partners', COUNT(*) FROM partner.Partners
UNION ALL SELECT N'document.Documents', COUNT(*) FROM document.Documents
UNION ALL SELECT N'files.Files', COUNT(*) FROM files.Files
UNION ALL SELECT N'notification.NotificationLogs', COUNT(*) FROM notification.NotificationLogs
UNION ALL SELECT N'emailworker.EmailScanLogs', COUNT(*) FROM emailworker.EmailScanLogs
UNION ALL SELECT N'emailworker.EmailScanItemLogs', COUNT(*) FROM emailworker.EmailScanItemLogs;
GO
