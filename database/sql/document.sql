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
    WHERE [MigrationId] = N'20260730115658_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'document') IS NULL EXEC(N'CREATE SCHEMA [document];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730115658_InitialCreate'
)
BEGIN
    CREATE TABLE [document].[DocumentNumberCounters] (
        [DocType] nvarchar(450) NOT NULL,
        [Year] int NOT NULL,
        [CurrentValue] int NOT NULL,
        CONSTRAINT [PK_DocumentNumberCounters] PRIMARY KEY ([DocType], [Year])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730115658_InitialCreate'
)
BEGIN
    CREATE TABLE [document].[Documents] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentNumber] nvarchar(450) NOT NULL,
        [DocType] nvarchar(450) NOT NULL,
        [Status] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Summary] nvarchar(max) NULL,
        [PartnerId] uniqueidentifier NULL,
        [SenderDepartmentId] uniqueidentifier NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [ReceivedAt] datetime2 NULL,
        [DistributedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Documents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730115658_InitialCreate'
)
BEGIN
    CREATE TABLE [document].[DocumentAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [FileId] uniqueidentifier NOT NULL,
        [AttachmentType] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DocumentAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentAttachments_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[Documents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730115658_InitialCreate'
)
BEGIN
    CREATE TABLE [document].[DocumentDepartmentAccess] (
        [DocumentId] uniqueidentifier NOT NULL,
        [DepartmentId] uniqueidentifier NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        [AssignedByUserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_DocumentDepartmentAccess] PRIMARY KEY ([DocumentId], [DepartmentId]),
        CONSTRAINT [FK_DocumentDepartmentAccess_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[Documents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730115658_InitialCreate'
)
BEGIN
    CREATE TABLE [document].[DocumentStatusHistory] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [OldStatus] nvarchar(max) NULL,
        [NewStatus] nvarchar(max) NOT NULL,
        [ChangedByUserId] uniqueidentifier NOT NULL,
        [ChangedAt] datetime2 NOT NULL,
        [Note] nvarchar(max) NULL,
        CONSTRAINT [PK_DocumentStatusHistory] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentStatusHistory_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[Documents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730115658_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DocumentAttachments_DocumentId] ON [document].[DocumentAttachments] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730115658_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Documents_DocType_Status] ON [document].[Documents] ([DocType], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730115658_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Documents_DocumentNumber] ON [document].[Documents] ([DocumentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730115658_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Documents_PartnerId] ON [document].[Documents] ([PartnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730115658_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DocumentStatusHistory_DocumentId] ON [document].[DocumentStatusHistory] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260730115658_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260730115658_InitialCreate', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827080431_AddSoftDeleteSqlServer'
)
BEGIN
    ALTER TABLE [document].[Documents] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827080431_AddSoftDeleteSqlServer'
)
BEGIN
    ALTER TABLE [document].[Documents] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827080431_AddSoftDeleteSqlServer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260827080431_AddSoftDeleteSqlServer', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827110241_AddIncomingSourceMessageId'
)
BEGIN
    ALTER TABLE [document].[Documents] ADD [SourceMessageId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827110241_AddIncomingSourceMessageId'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Documents_SourceMessageId] ON [document].[Documents] ([SourceMessageId]) WHERE [SourceMessageId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827110241_AddIncomingSourceMessageId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260827110241_AddIncomingSourceMessageId', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004060403_AddBusinessCatalogs'
)
BEGIN
    CREATE TABLE [document].[BusinessCatalogEntries] (
        [Id] uniqueidentifier NOT NULL,
        [Group] nvarchar(32) NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [Version] bigint NOT NULL,
        CONSTRAINT [PK_BusinessCatalogEntries] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004060403_AddBusinessCatalogs'
)
BEGIN
    CREATE TABLE [document].[CatalogAuditEvents] (
        [Id] uniqueidentifier NOT NULL,
        [EntryId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(32) NOT NULL,
        [BeforeJson] nvarchar(max) NOT NULL,
        [AfterJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CatalogAuditEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004060403_AddBusinessCatalogs'
)
BEGIN
    CREATE TABLE [document].[DistributionTargets] (
        [Id] uniqueidentifier NOT NULL,
        [LegacyId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Initial] nvarchar(32) NULL,
        [IsActive] bit NOT NULL,
        [MappingState] nvarchar(16) NOT NULL,
        [Version] bigint NOT NULL,
        CONSTRAINT [PK_DistributionTargets] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004060403_AddBusinessCatalogs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Group', N'IsActive', N'Name', N'SortOrder', N'Version') AND [object_id] = OBJECT_ID(N'[document].[BusinessCatalogEntries]'))
        SET IDENTITY_INSERT [document].[BusinessCatalogEntries] ON;
    EXEC(N'INSERT INTO [document].[BusinessCatalogEntries] ([Id], [Code], [Group], [IsActive], [Name], [SortOrder], [Version])
    VALUES (''10000000-0000-4000-8000-000100000001'', N''HL'', N''companies'', CAST(1 AS bit), N''Hoàng Long'', 1, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000100000002'', N''HV'', N''companies'', CAST(1 AS bit), N''Hoàn Vũ'', 2, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000100000003'', N''HLHV'', N''companies'', CAST(1 AS bit), N''Hoàng Long Hoàn Vũ'', 3, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000200000001'', N''FAX'', N''methods'', CAST(1 AS bit), N''Fax'', 1, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000200000002'', N''COURIER'', N''methods'', CAST(1 AS bit), N''Courier'', 2, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000200000003'', N''EMAIL'', N''methods'', CAST(1 AS bit), N''eMail'', 3, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000200000004'', N''HAND_DELIVER'', N''methods'', CAST(1 AS bit), N''Pick-up/Hand-Deliver'', 4, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000200000005'', N''EMAIL_FAX'', N''methods'', CAST(1 AS bit), N''Email & Fax'', 5, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000300000001'', N''LETTER'', N''documentTypes'', CAST(1 AS bit), N''Letter'', 1, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000300000002'', N''NOTIFICATION'', N''documentTypes'', CAST(1 AS bit), N''Notification'', 2, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000300000003'', N''ANNOUNCEMENT'', N''documentTypes'', CAST(1 AS bit), N''Announcement'', 3, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000300000004'', N''APPROVAL_REQUEST'', N''documentTypes'', CAST(1 AS bit), N''Approval / Request'', 4, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000300000005'', N''INVITATION'', N''documentTypes'', CAST(1 AS bit), N''Invitation'', 5, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000300000006'', N''STATEMENT'', N''documentTypes'', CAST(1 AS bit), N''Statement'', 6, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000400000001'', N''MEMO'', N''internalTypes'', CAST(1 AS bit), N''Inter-Office Memo'', 1, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000400000002'', N''REPORT'', N''internalTypes'', CAST(1 AS bit), N''Report'', 2, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000400000003'', N''STATEMENT'', N''internalTypes'', CAST(1 AS bit), N''Statement'', 3, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000400000004'', N''PURCHASE_REQUEST'', N''internalTypes'', CAST(1 AS bit), N''Purchase Request'', 4, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000400000005'', N''OTHERS'', N''internalTypes'', CAST(1 AS bit), N''Others'', 5, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000500000001'', N''Normal'', N''sensitivity'', CAST(1 AS bit), N''Normal'', 1, CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000500000002'', N''Confidential'', N''sensitivity'', CAST(1 AS bit), N''Confidential'', 2, CAST(1 AS bigint))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'Group', N'IsActive', N'Name', N'SortOrder', N'Version') AND [object_id] = OBJECT_ID(N'[document].[BusinessCatalogEntries]'))
        SET IDENTITY_INSERT [document].[BusinessCatalogEntries] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004060403_AddBusinessCatalogs'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Initial', N'IsActive', N'LegacyId', N'MappingState', N'Name', N'Version') AND [object_id] = OBJECT_ID(N'[document].[DistributionTargets]'))
        SET IDENTITY_INSERT [document].[DistributionTargets] ON;
    EXEC(N'INSERT INTO [document].[DistributionTargets] ([Id], [Initial], [IsActive], [LegacyId], [MappingState], [Name], [Version])
    VALUES (''10000000-0000-4000-8000-000600000001'', N''MGM'', CAST(1 AS bit), 1, N''Pending'', N''Management'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000002'', N''PRD'', CAST(1 AS bit), 2, N''Pending'', N''Production'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000003'', N''HSE'', CAST(1 AS bit), 3, N''Pending'', N''HSE'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000004'', N''PRJ'', CAST(1 AS bit), 4, N''Pending'', N''Project'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000005'', N''SUB'', CAST(1 AS bit), 5, N''Pending'', N''Subsurface'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000006'', N''FIN'', CAST(1 AS bit), 6, N''Pending'', N''Finance'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000007'', N''ADM'', CAST(1 AS bit), 7, N''Pending'', N''Administration'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000008'', N''C&P'', CAST(1 AS bit), 8, N''Pending'', N''C&P'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000009'', N''DRI'', CAST(1 AS bit), 9, N''Pending'', N''Drilling'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000010'', NULL, CAST(1 AS bit), 10, N''Pending'', N''HLHV Partners'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000011'', NULL, CAST(1 AS bit), 11, N''Pending'', N''Secretary List'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000012'', NULL, CAST(1 AS bit), 12, N''Pending'', N''VT Shore Base'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000013'', NULL, CAST(1 AS bit), 13, N''Pending'', N''HLHV Members'', CAST(1 AS bigint)),
    (''10000000-0000-4000-8000-000600000014'', N''HLHVM'', CAST(1 AS bit), 14, N''Pending'', N''HLHV Managers'', CAST(1 AS bigint))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Initial', N'IsActive', N'LegacyId', N'MappingState', N'Name', N'Version') AND [object_id] = OBJECT_ID(N'[document].[DistributionTargets]'))
        SET IDENTITY_INSERT [document].[DistributionTargets] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004060403_AddBusinessCatalogs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessCatalogEntries_Group_Code] ON [document].[BusinessCatalogEntries] ([Group], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004060403_AddBusinessCatalogs'
)
BEGIN
    CREATE INDEX [IX_CatalogAuditEvents_EntryId] ON [document].[CatalogAuditEvents] ([EntryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004060403_AddBusinessCatalogs'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DistributionTargets_LegacyId] ON [document].[DistributionTargets] ([LegacyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004060403_AddBusinessCatalogs'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004060403_AddBusinessCatalogs', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004111636_AddV2RegistrationPersistence'
)
BEGIN
    CREATE TABLE [document].[DocumentOutboxEvents] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [Type] nvarchar(64) NOT NULL,
        [PayloadJson] nvarchar(1000) NOT NULL,
        [State] nvarchar(16) NOT NULL,
        [Attempts] int NOT NULL,
        [AggregateVersion] bigint NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_DocumentOutboxEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DocumentOutbox_Attempts] CHECK ([Attempts] >= 0),
        CONSTRAINT [FK_DocumentOutboxEvents_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[Documents] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004111636_AddV2RegistrationPersistence'
)
BEGIN
    CREATE TABLE [document].[DocumentRegistrations] (
        [DocumentId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(16) NOT NULL,
        [RegistrationDate] date NOT NULL,
        [RegistrationYear] int NOT NULL,
        [SequenceNumber] int NOT NULL,
        [RegisteredAt] datetimeoffset NOT NULL,
        [CompanyCode] nvarchar(8) NOT NULL,
        [CompanyNameSnapshot] nvarchar(200) NOT NULL,
        [OwnerDepartmentId] uniqueidentifier NOT NULL,
        [OwnerDepartmentCodeSnapshot] nvarchar(32) NOT NULL,
        [OwnerDepartmentNameSnapshot] nvarchar(200) NOT NULL,
        [InputterUserId] uniqueidentifier NOT NULL,
        [OriginatorUserId] uniqueidentifier NOT NULL,
        [LastModifierUserId] uniqueidentifier NOT NULL,
        [IssuedDate] date NULL,
        [Sensitivity] nvarchar(16) NOT NULL,
        [Remark] nvarchar(4000) NULL,
        [Version] bigint NOT NULL,
        CONSTRAINT [PK_DocumentRegistrations] PRIMARY KEY ([DocumentId]),
        CONSTRAINT [CK_Registration_Company] CHECK ([CompanyCode] IN ('HL', 'HV', 'HLHV')),
        CONSTRAINT [CK_Registration_Kind] CHECK ([Kind] IN ('INCOMING', 'OUTGOING', 'INTERNAL')),
        CONSTRAINT [CK_Registration_Sensitivity] CHECK ([Sensitivity] IN ('Normal', 'Confidential')),
        CONSTRAINT [CK_Registration_Sequence] CHECK ([SequenceNumber] BETWEEN 1 AND 99999),
        CONSTRAINT [CK_Registration_Version] CHECK ([Version] >= 1),
        CONSTRAINT [CK_Registration_Year] CHECK ([RegistrationYear] = YEAR([RegistrationDate])),
        CONSTRAINT [FK_DocumentRegistrations_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[Documents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004111636_AddV2RegistrationPersistence'
)
BEGIN
    CREATE TABLE [document].[RegistrationRequests] (
        [Id] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Kind] nvarchar(16) NOT NULL,
        [KeyHash] nvarchar(64) NOT NULL,
        [BodyHash] nvarchar(64) NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_RegistrationRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RegistrationRequests_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[Documents] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004111636_AddV2RegistrationPersistence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentOutboxEvents_DocumentId_Type_AggregateVersion] ON [document].[DocumentOutboxEvents] ([DocumentId], [Type], [AggregateVersion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004111636_AddV2RegistrationPersistence'
)
BEGIN
    CREATE INDEX [IX_DocumentOutboxEvents_State] ON [document].[DocumentOutboxEvents] ([State]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004111636_AddV2RegistrationPersistence'
)
BEGIN
    CREATE INDEX [IX_DocumentRegistrations_InputterUserId] ON [document].[DocumentRegistrations] ([InputterUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004111636_AddV2RegistrationPersistence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentRegistrations_Kind_RegistrationYear_SequenceNumber] ON [document].[DocumentRegistrations] ([Kind], [RegistrationYear], [SequenceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004111636_AddV2RegistrationPersistence'
)
BEGIN
    CREATE INDEX [IX_DocumentRegistrations_OriginatorUserId] ON [document].[DocumentRegistrations] ([OriginatorUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004111636_AddV2RegistrationPersistence'
)
BEGIN
    CREATE INDEX [IX_DocumentRegistrations_OwnerDepartmentId_RegistrationDate] ON [document].[DocumentRegistrations] ([OwnerDepartmentId], [RegistrationDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004111636_AddV2RegistrationPersistence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RegistrationRequests_ActorUserId_Kind_KeyHash] ON [document].[RegistrationRequests] ([ActorUserId], [Kind], [KeyHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004111636_AddV2RegistrationPersistence'
)
BEGIN
    CREATE INDEX [IX_RegistrationRequests_DocumentId] ON [document].[RegistrationRequests] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004111636_AddV2RegistrationPersistence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004111636_AddV2RegistrationPersistence', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004122637_AddDocumentEditAudit'
)
BEGIN
    CREATE TABLE [document].[DocumentEditAudits] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Version] bigint NOT NULL,
        [ChangedAt] datetimeoffset NOT NULL,
        [ChangesJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_DocumentEditAudits] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DocumentEditAudit_Version] CHECK ([Version] >= 2),
        CONSTRAINT [FK_DocumentEditAudits_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[Documents] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004122637_AddDocumentEditAudit'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentEditAudits_DocumentId_Version] ON [document].[DocumentEditAudits] ([DocumentId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004122637_AddDocumentEditAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004122637_AddDocumentEditAudit', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125541_AddDocumentKindDetails'
)
BEGIN
    CREATE TABLE [document].[DocumentKindDetails] (
        [DocumentId] uniqueidentifier NOT NULL,
        [ReceivingDate] date NULL,
        [SenderPartnerId] uniqueidentifier NULL,
        [SenderNameSnapshot] nvarchar(200) NULL,
        [ReferenceNumber] nvarchar(200) NULL,
        [MethodCode] nvarchar(64) NULL,
        [MethodNameSnapshot] nvarchar(200) NULL,
        [DocumentTypeCode] nvarchar(64) NULL,
        [DocumentTypeNameSnapshot] nvarchar(200) NULL,
        [CategoryCode] nvarchar(64) NULL,
        [CategoryNameSnapshot] nvarchar(200) NULL,
        [ContractNumber] nvarchar(200) NULL,
        [OtherRecipients] nvarchar(4000) NULL,
        [Others] nvarchar(4000) NULL,
        CONSTRAINT [PK_DocumentKindDetails] PRIMARY KEY ([DocumentId]),
        CONSTRAINT [FK_DocumentKindDetails_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[Documents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125541_AddDocumentKindDetails'
)
BEGIN
    CREATE TABLE [document].[DocumentRecipients] (
        [DocumentId] uniqueidentifier NOT NULL,
        [ReferenceType] nvarchar(32) NOT NULL,
        [ReferenceId] uniqueidentifier NOT NULL,
        [NameSnapshot] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_DocumentRecipients] PRIMARY KEY ([DocumentId], [ReferenceType], [ReferenceId]),
        CONSTRAINT [CK_DocumentRecipient_Type] CHECK ([ReferenceType] IN ('ExternalEntity', 'DistributionTarget')),
        CONSTRAINT [FK_DocumentRecipients_Documents_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[Documents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125541_AddDocumentKindDetails'
)
BEGIN
    CREATE INDEX [IX_DocumentKindDetails_SenderPartnerId] ON [document].[DocumentKindDetails] ([SenderPartnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125541_AddDocumentKindDetails'
)
BEGIN
    CREATE INDEX [IX_DocumentRecipients_ReferenceType_ReferenceId] ON [document].[DocumentRecipients] ([ReferenceType], [ReferenceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004125541_AddDocumentKindDetails'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004125541_AddDocumentKindDetails', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004132429_AddDocumentRelations'
)
BEGIN
    CREATE TABLE [document].[DocumentRelations] (
        [IncomingDocumentId] uniqueidentifier NOT NULL,
        [OutgoingDocumentId] uniqueidentifier NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_DocumentRelations] PRIMARY KEY ([IncomingDocumentId], [OutgoingDocumentId]),
        CONSTRAINT [CK_DocumentRelation_DifferentEnds] CHECK ([IncomingDocumentId] <> [OutgoingDocumentId]),
        CONSTRAINT [FK_DocumentRelations_DocumentRegistrations_IncomingDocumentId] FOREIGN KEY ([IncomingDocumentId]) REFERENCES [document].[DocumentRegistrations] ([DocumentId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DocumentRelations_DocumentRegistrations_OutgoingDocumentId] FOREIGN KEY ([OutgoingDocumentId]) REFERENCES [document].[DocumentRegistrations] ([DocumentId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004132429_AddDocumentRelations'
)
BEGIN
    CREATE INDEX [IX_DocumentRelations_OutgoingDocumentId] ON [document].[DocumentRelations] ([OutgoingDocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004132429_AddDocumentRelations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004132429_AddDocumentRelations', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004134524_AddDocumentCancellations'
)
BEGIN
    CREATE TABLE [document].[DocumentCancellations] (
        [DocumentId] uniqueidentifier NOT NULL,
        [PreviousStatus] nvarchar(16) NOT NULL,
        [Reason] nvarchar(4000) NOT NULL,
        [CancelledByUserId] uniqueidentifier NOT NULL,
        [CancelledAt] datetimeoffset NOT NULL,
        [RestoredByUserId] uniqueidentifier NULL,
        [RestoredAt] datetimeoffset NULL,
        CONSTRAINT [PK_DocumentCancellations] PRIMARY KEY ([DocumentId]),
        CONSTRAINT [CK_DocumentCancellation_PreviousStatus] CHECK ([PreviousStatus] IN ('InProgress', 'Distributed')),
        CONSTRAINT [CK_DocumentCancellation_Reason] CHECK (LEN(TRIM([Reason])) > 0),
        CONSTRAINT [CK_DocumentCancellation_Restore] CHECK (([RestoredAt] IS NULL AND [RestoredByUserId] IS NULL) OR ([RestoredAt] IS NOT NULL AND [RestoredByUserId] IS NOT NULL)),
        CONSTRAINT [FK_DocumentCancellations_DocumentRegistrations_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[DocumentRegistrations] ([DocumentId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004134524_AddDocumentCancellations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004134524_AddDocumentCancellations', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005024036_AddCurrentPdfReplacement'
)
BEGIN
    CREATE TABLE [document].[DocumentCurrentPdfs] (
        [DocumentId] uniqueidentifier NOT NULL,
        [OperationId] uniqueidentifier NOT NULL,
        [FileId] uniqueidentifier NOT NULL,
        [OriginalName] nvarchar(200) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [State] nvarchar(16) NOT NULL,
        [Version] bigint NOT NULL,
        [LastCheckedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DocumentCurrentPdfs] PRIMARY KEY ([DocumentId]),
        CONSTRAINT [CK_CurrentPdf_Size] CHECK ([SizeBytes] BETWEEN 1 AND 26214400),
        CONSTRAINT [CK_CurrentPdf_State] CHECK ([State] IN ('Pending','Ready','Missing')),
        CONSTRAINT [CK_CurrentPdf_Version] CHECK ([Version] >= 1),
        CONSTRAINT [FK_DocumentCurrentPdfs_DocumentRegistrations_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[DocumentRegistrations] ([DocumentId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005024036_AddCurrentPdfReplacement'
)
BEGIN
    CREATE TABLE [document].[PdfReplacements] (
        [OperationId] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [FileId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [ExpectedVersion] bigint NOT NULL,
        [CommittedVersion] bigint NULL,
        [State] nvarchar(16) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PdfReplacements] PRIMARY KEY ([OperationId]),
        CONSTRAINT [CK_PdfReplacement_State] CHECK ([State] IN ('Preparing','Committed','Aborted')),
        CONSTRAINT [CK_PdfReplacement_Version] CHECK ([ExpectedVersion] >= 1 AND ([CommittedVersion] IS NULL OR [CommittedVersion] > [ExpectedVersion])),
        CONSTRAINT [FK_PdfReplacements_DocumentRegistrations_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[DocumentRegistrations] ([DocumentId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005024036_AddCurrentPdfReplacement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentCurrentPdfs_FileId] ON [document].[DocumentCurrentPdfs] ([FileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005024036_AddCurrentPdfReplacement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentCurrentPdfs_OperationId] ON [document].[DocumentCurrentPdfs] ([OperationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005024036_AddCurrentPdfReplacement'
)
BEGIN
    CREATE INDEX [IX_PdfReplacements_DocumentId] ON [document].[PdfReplacements] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005024036_AddCurrentPdfReplacement'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PdfReplacements_FileId] ON [document].[PdfReplacements] ([FileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005024036_AddCurrentPdfReplacement'
)
BEGIN
    CREATE INDEX [IX_PdfReplacements_State] ON [document].[PdfReplacements] ([State]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005024036_AddCurrentPdfReplacement'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005024036_AddCurrentPdfReplacement', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052843_AddReminderAndTaskIntents'
)
BEGIN
    CREATE TABLE [document].[DocumentTaskIntent] (
        [Id] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [ActorId] uniqueidentifier NOT NULL,
        [AssigneeId] uniqueidentifier NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [KeyHash] nvarchar(64) NOT NULL,
        [BodyHash] nvarchar(64) NOT NULL,
        [State] nvarchar(32) NOT NULL,
        [RemoteTaskId] nvarchar(200) NULL,
        [LeaseUntilUnix] bigint NOT NULL,
        [LeaseToken] uniqueidentifier NULL,
        [Version] bigint NOT NULL,
        CONSTRAINT [PK_DocumentTaskIntent] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentTaskIntent_DocumentRegistrations_DocumentId] FOREIGN KEY ([DocumentId]) REFERENCES [document].[DocumentRegistrations] ([DocumentId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052843_AddReminderAndTaskIntents'
)
BEGIN
    CREATE TABLE [document].[ReminderBatch] (
        [Id] uniqueidentifier NOT NULL,
        [DepartmentId] uniqueidentifier NOT NULL,
        [Period] date NOT NULL,
        [State] nvarchar(32) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [LeaseUntilUnix] bigint NOT NULL,
        [LeaseToken] uniqueidentifier NULL,
        [Attempts] int NOT NULL,
        [Version] bigint NOT NULL,
        [ErrorCode] nvarchar(100) NULL,
        CONSTRAINT [PK_ReminderBatch] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052843_AddReminderAndTaskIntents'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentTaskIntent_ActorId_KeyHash] ON [document].[DocumentTaskIntent] ([ActorId], [KeyHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052843_AddReminderAndTaskIntents'
)
BEGIN
    CREATE INDEX [IX_DocumentTaskIntent_DocumentId] ON [document].[DocumentTaskIntent] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052843_AddReminderAndTaskIntents'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReminderBatch_DepartmentId_Period] ON [document].[ReminderBatch] ([DepartmentId], [Period]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052843_AddReminderAndTaskIntents'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005052843_AddReminderAndTaskIntents', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005060101_AddDocumentNotificationDelivery'
)
BEGIN
    CREATE TABLE [document].[DocumentNotificationDelivery] (
        [Id] uniqueidentifier NOT NULL,
        [EventId] uniqueidentifier NOT NULL,
        [RecipientId] uniqueidentifier NOT NULL,
        [State] nvarchar(32) NOT NULL,
        [Attempts] int NOT NULL,
        [NextAttemptUnix] bigint NOT NULL,
        [LeaseUntilUnix] bigint NOT NULL,
        [LeaseToken] uniqueidentifier NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [Version] bigint NOT NULL,
        CONSTRAINT [PK_DocumentNotificationDelivery] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DocumentNotificationDelivery_DocumentOutboxEvents_EventId] FOREIGN KEY ([EventId]) REFERENCES [document].[DocumentOutboxEvents] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005060101_AddDocumentNotificationDelivery'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DocumentNotificationDelivery_EventId_RecipientId] ON [document].[DocumentNotificationDelivery] ([EventId], [RecipientId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005060101_AddDocumentNotificationDelivery'
)
BEGIN
    CREATE INDEX [IX_DocumentNotificationDelivery_State_NextAttemptUnix] ON [document].[DocumentNotificationDelivery] ([State], [NextAttemptUnix]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005060101_AddDocumentNotificationDelivery'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005060101_AddDocumentNotificationDelivery', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005072535_AddReminderDeliveryLedger'
)
BEGIN
    CREATE TABLE [document].[ReminderFanoutManifest] (
        [BatchId] uniqueidentifier NOT NULL,
        [PlanHash] nvarchar(64) NOT NULL,
        CONSTRAINT [PK_ReminderFanoutManifest] PRIMARY KEY ([BatchId]),
        CONSTRAINT [FK_ReminderFanoutManifest_ReminderBatch_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [document].[ReminderBatch] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005072535_AddReminderDeliveryLedger'
)
BEGIN
    CREATE TABLE [document].[ReminderDelivery] (
        [Id] uniqueidentifier NOT NULL,
        [BatchId] uniqueidentifier NOT NULL,
        [InputterUserId] uniqueidentifier NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [State] nvarchar(32) NOT NULL,
        [Attempts] int NOT NULL,
        [Failures] int NOT NULL,
        [LeaseUntilUnix] bigint NOT NULL,
        [LeaseToken] uniqueidentifier NULL,
        [NextAttemptUnix] bigint NOT NULL,
        [Version] bigint NOT NULL,
        [NotificationId] uniqueidentifier NULL,
        [NotificationState] nvarchar(32) NULL,
        CONSTRAINT [PK_ReminderDelivery] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReminderDelivery_ReminderFanoutManifest_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [document].[ReminderFanoutManifest] ([BatchId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005072535_AddReminderDeliveryLedger'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReminderDelivery_BatchId_InputterUserId] ON [document].[ReminderDelivery] ([BatchId], [InputterUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005072535_AddReminderDeliveryLedger'
)
BEGIN
    CREATE INDEX [IX_ReminderDelivery_State_NextAttemptUnix] ON [document].[ReminderDelivery] ([State], [NextAttemptUnix]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005072535_AddReminderDeliveryLedger'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005072535_AddReminderDeliveryLedger', N'10.0.3');
END;

COMMIT;
GO

