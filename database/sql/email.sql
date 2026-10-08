IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006121013_EmailWorkerBaseline'
)
BEGIN
    IF SCHEMA_ID(N'emailworker') IS NULL EXEC(N'CREATE SCHEMA [emailworker];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006121013_EmailWorkerBaseline'
)
BEGIN
    CREATE TABLE [emailworker].[EmailImapSettings] (
        [Id] int NOT NULL IDENTITY,
        [ImapHost] nvarchar(200) NOT NULL,
        [ImapPort] int NOT NULL,
        [UseSsl] bit NOT NULL,
        [EmailAddress] nvarchar(200) NOT NULL,
        [AppPassword] nvarchar(500) NOT NULL,
        [WhitelistedDomains] nvarchar(1000) NOT NULL,
        [AutoScanIntervalMinutes] int NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_EmailImapSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006121013_EmailWorkerBaseline'
)
BEGIN
    CREATE TABLE [emailworker].[EmailScanLogs] (
        [Id] uniqueidentifier NOT NULL,
        [StartedAt] datetime2 NOT NULL,
        [FinishedAt] datetime2 NULL,
        [EmailsScanned] int NOT NULL,
        [TotalEmails] int NOT NULL,
        [DocumentsCreated] int NOT NULL,
        [ReadyForIntakeCount] int NOT NULL,
        [SkippedCount] int NOT NULL,
        [FailedCount] int NOT NULL,
        [CurrentEmailSubject] nvarchar(1000) NULL,
        [CurrentSenderEmail] nvarchar(320) NULL,
        [Success] bit NOT NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        [TriggerType] nvarchar(20) NOT NULL,
        CONSTRAINT [PK_EmailScanLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006121013_EmailWorkerBaseline'
)
BEGIN
    CREATE TABLE [emailworker].[EmailScanItemLogs] (
        [Id] uniqueidentifier NOT NULL,
        [ScanLogId] uniqueidentifier NOT NULL,
        [ReceivedAt] datetime2 NOT NULL,
        [SenderEmail] nvarchar(320) NOT NULL,
        [Subject] nvarchar(1000) NULL,
        [AttachmentName] nvarchar(500) NULL,
        [FileId] uniqueidentifier NULL,
        [PartnerId] uniqueidentifier NULL,
        [ExtractedReferenceNumber] nvarchar(200) NULL,
        [ExtractedSubject] nvarchar(1000) NULL,
        [DocumentId] nvarchar(100) NULL,
        [Status] nvarchar(50) NOT NULL,
        [ErrorMessage] nvarchar(2000) NULL,
        [ProcessedAt] datetime2 NULL,
        [IntakeConfirmedAt] datetime2 NULL,
        CONSTRAINT [PK_EmailScanItemLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EmailScanItemLogs_EmailScanLogs_ScanLogId] FOREIGN KEY ([ScanLogId]) REFERENCES [emailworker].[EmailScanLogs] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006121013_EmailWorkerBaseline'
)
BEGIN
    CREATE INDEX [IX_EmailScanItemLogs_ScanLogId] ON [emailworker].[EmailScanItemLogs] ([ScanLogId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006121013_EmailWorkerBaseline'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006121013_EmailWorkerBaseline', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006154722_FixedEmailSettingsKey'
)
BEGIN
    IF (SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'emailworker.EmailImapSettings')) <> 9
        OR COLUMNPROPERTY(OBJECT_ID(N'emailworker.EmailImapSettings'),N'Id','IsIdentity') <> 1
        THROW 51000, 'Unexpected email settings schema; audited manual migration required.', 1;
    IF EXISTS (SELECT 1 FROM [emailworker].[EmailImapSettings] WHERE [Id] <> 1)
        THROW 51001, 'Unexpected email settings keys; audited manual migration required.', 1;
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE referenced_object_id=OBJECT_ID(N'emailworker.EmailImapSettings') OR parent_object_id=OBJECT_ID(N'emailworker.EmailImapSettings'))
        OR EXISTS (SELECT 1 FROM sys.triggers WHERE parent_id=OBJECT_ID(N'emailworker.EmailImapSettings'))
        OR EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'emailworker.EmailImapSettings') AND index_id>0 AND is_primary_key=0)
        OR EXISTS (SELECT 1 FROM sys.database_permissions WHERE class=1 AND major_id=OBJECT_ID(N'emailworker.EmailImapSettings'))
        OR EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id=OBJECT_ID(N'emailworker.EmailImapSettings'))
        OR EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'emailworker.EmailImapSettings')
            )
        THROW 51002, 'Custom email settings objects or permissions require audited manual migration.', 1;
    CREATE TABLE [emailworker].[EmailImapSettings_KeySwap] (
        [Id] int  NOT NULL,
        [ImapHost] nvarchar(200) NOT NULL,
        [ImapPort] int NOT NULL,
        [UseSsl] bit NOT NULL,
        [EmailAddress] nvarchar(200) NOT NULL,
        [AppPassword] nvarchar(500) NOT NULL,
        [WhitelistedDomains] nvarchar(1000) NOT NULL,
        [AutoScanIntervalMinutes] int NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_EmailImapSettings_KeySwap] PRIMARY KEY ([Id]), CONSTRAINT [CK_EmailImapSettings_Singleton] CHECK ([Id] = 1)
    );

    INSERT INTO [emailworker].[EmailImapSettings_KeySwap]
        ([Id],[ImapHost],[ImapPort],[UseSsl],[EmailAddress],[AppPassword],[WhitelistedDomains],[AutoScanIntervalMinutes],[UpdatedAt])
        SELECT [Id],[ImapHost],[ImapPort],[UseSsl],[EmailAddress],[AppPassword],[WhitelistedDomains],[AutoScanIntervalMinutes],[UpdatedAt]
        FROM [emailworker].[EmailImapSettings];

    DROP TABLE [emailworker].[EmailImapSettings];
    EXEC sp_rename N'emailworker.EmailImapSettings_KeySwap', N'EmailImapSettings';
    EXEC sp_rename N'emailworker.PK_EmailImapSettings_KeySwap', N'PK_EmailImapSettings', N'OBJECT';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006154722_FixedEmailSettingsKey'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006154722_FixedEmailSettingsKey', N'9.0.0');
END;

COMMIT;
GO

