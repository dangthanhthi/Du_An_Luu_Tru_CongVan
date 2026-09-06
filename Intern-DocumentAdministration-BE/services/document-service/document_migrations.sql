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
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentStatusHistory]') AND [c].[name] = N'OldStatus');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentStatusHistory] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [document].[DocumentStatusHistory] ALTER COLUMN [OldStatus] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentStatusHistory]') AND [c].[name] = N'Note');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentStatusHistory] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [document].[DocumentStatusHistory] ALTER COLUMN [Note] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentStatusHistory]') AND [c].[name] = N'NewStatus');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentStatusHistory] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [document].[DocumentStatusHistory] ALTER COLUMN [NewStatus] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DROP INDEX [IX_DocumentStatusHistory_DocumentId] ON [document].[DocumentStatusHistory];
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentStatusHistory]') AND [c].[name] = N'DocumentId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentStatusHistory] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [document].[DocumentStatusHistory] ALTER COLUMN [DocumentId] TEXT NOT NULL;
    CREATE INDEX [IX_DocumentStatusHistory_DocumentId] ON [document].[DocumentStatusHistory] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentStatusHistory]') AND [c].[name] = N'ChangedByUserId');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentStatusHistory] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [document].[DocumentStatusHistory] ALTER COLUMN [ChangedByUserId] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentStatusHistory]') AND [c].[name] = N'ChangedAt');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentStatusHistory] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [document].[DocumentStatusHistory] ALTER COLUMN [ChangedAt] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentStatusHistory]') AND [c].[name] = N'Id');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentStatusHistory] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [document].[DocumentStatusHistory] ALTER COLUMN [Id] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'UpdatedAt');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [UpdatedAt] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'Title');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [Title] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'Summary');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [Summary] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DROP INDEX [IX_Documents_DocType_Status] ON [document].[Documents];
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'Status');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [Status] TEXT NOT NULL;
    CREATE INDEX [IX_Documents_DocType_Status] ON [document].[Documents] ([DocType], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'SenderDepartmentId');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [SenderDepartmentId] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var12 nvarchar(max);
    SELECT @var12 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'ReceivedAt');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var12 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [ReceivedAt] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DROP INDEX [IX_Documents_PartnerId] ON [document].[Documents];
    DECLARE @var13 nvarchar(max);
    SELECT @var13 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'PartnerId');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var13 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [PartnerId] TEXT NULL;
    CREATE INDEX [IX_Documents_PartnerId] ON [document].[Documents] ([PartnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DROP INDEX [IX_Documents_DocumentNumber] ON [document].[Documents];
    DECLARE @var14 nvarchar(max);
    SELECT @var14 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'DocumentNumber');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var14 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [DocumentNumber] TEXT NOT NULL;
    CREATE UNIQUE INDEX [IX_Documents_DocumentNumber] ON [document].[Documents] ([DocumentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DROP INDEX [IX_Documents_DocType_Status] ON [document].[Documents];
    DECLARE @var15 nvarchar(max);
    SELECT @var15 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'DocType');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var15 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [DocType] TEXT NOT NULL;
    CREATE INDEX [IX_Documents_DocType_Status] ON [document].[Documents] ([DocType], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var16 nvarchar(max);
    SELECT @var16 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'DistributedAt');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var16 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [DistributedAt] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var17 nvarchar(max);
    SELECT @var17 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'CreatedByUserId');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var17 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [CreatedByUserId] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var18 nvarchar(max);
    SELECT @var18 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'CreatedAt');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var18 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [CreatedAt] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var19 nvarchar(max);
    SELECT @var19 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[Documents]') AND [c].[name] = N'Id');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [document].[Documents] DROP CONSTRAINT ' + @var19 + ';');
    ALTER TABLE [document].[Documents] ALTER COLUMN [Id] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    ALTER TABLE [document].[Documents] ADD [DeletedAt] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    ALTER TABLE [document].[Documents] ADD [IsDeleted] INTEGER NOT NULL DEFAULT CAST(0 AS INTEGER);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var20 nvarchar(max);
    SELECT @var20 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentNumberCounters]') AND [c].[name] = N'CurrentValue');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentNumberCounters] DROP CONSTRAINT ' + @var20 + ';');
    ALTER TABLE [document].[DocumentNumberCounters] ALTER COLUMN [CurrentValue] INTEGER NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var21 nvarchar(max);
    SELECT @var21 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentNumberCounters]') AND [c].[name] = N'Year');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentNumberCounters] DROP CONSTRAINT ' + @var21 + ';');
    ALTER TABLE [document].[DocumentNumberCounters] ALTER COLUMN [Year] INTEGER NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var22 nvarchar(max);
    SELECT @var22 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentNumberCounters]') AND [c].[name] = N'DocType');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentNumberCounters] DROP CONSTRAINT ' + @var22 + ';');
    ALTER TABLE [document].[DocumentNumberCounters] ALTER COLUMN [DocType] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var23 nvarchar(max);
    SELECT @var23 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentDepartmentAccess]') AND [c].[name] = N'AssignedByUserId');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentDepartmentAccess] DROP CONSTRAINT ' + @var23 + ';');
    ALTER TABLE [document].[DocumentDepartmentAccess] ALTER COLUMN [AssignedByUserId] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var24 nvarchar(max);
    SELECT @var24 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentDepartmentAccess]') AND [c].[name] = N'AssignedAt');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentDepartmentAccess] DROP CONSTRAINT ' + @var24 + ';');
    ALTER TABLE [document].[DocumentDepartmentAccess] ALTER COLUMN [AssignedAt] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var25 nvarchar(max);
    SELECT @var25 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentDepartmentAccess]') AND [c].[name] = N'DepartmentId');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentDepartmentAccess] DROP CONSTRAINT ' + @var25 + ';');
    ALTER TABLE [document].[DocumentDepartmentAccess] ALTER COLUMN [DepartmentId] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var26 nvarchar(max);
    SELECT @var26 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentDepartmentAccess]') AND [c].[name] = N'DocumentId');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentDepartmentAccess] DROP CONSTRAINT ' + @var26 + ';');
    ALTER TABLE [document].[DocumentDepartmentAccess] ALTER COLUMN [DocumentId] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var27 nvarchar(max);
    SELECT @var27 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentAttachments]') AND [c].[name] = N'FileId');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentAttachments] DROP CONSTRAINT ' + @var27 + ';');
    ALTER TABLE [document].[DocumentAttachments] ALTER COLUMN [FileId] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DROP INDEX [IX_DocumentAttachments_DocumentId] ON [document].[DocumentAttachments];
    DECLARE @var28 nvarchar(max);
    SELECT @var28 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentAttachments]') AND [c].[name] = N'DocumentId');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentAttachments] DROP CONSTRAINT ' + @var28 + ';');
    ALTER TABLE [document].[DocumentAttachments] ALTER COLUMN [DocumentId] TEXT NOT NULL;
    CREATE INDEX [IX_DocumentAttachments_DocumentId] ON [document].[DocumentAttachments] ([DocumentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var29 nvarchar(max);
    SELECT @var29 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentAttachments]') AND [c].[name] = N'CreatedAt');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentAttachments] DROP CONSTRAINT ' + @var29 + ';');
    ALTER TABLE [document].[DocumentAttachments] ALTER COLUMN [CreatedAt] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var30 nvarchar(max);
    SELECT @var30 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentAttachments]') AND [c].[name] = N'AttachmentType');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentAttachments] DROP CONSTRAINT ' + @var30 + ';');
    ALTER TABLE [document].[DocumentAttachments] ALTER COLUMN [AttachmentType] TEXT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    DECLARE @var31 nvarchar(max);
    SELECT @var31 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[document].[DocumentAttachments]') AND [c].[name] = N'Id');
    IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [document].[DocumentAttachments] DROP CONSTRAINT ' + @var31 + ';');
    ALTER TABLE [document].[DocumentAttachments] ALTER COLUMN [Id] TEXT NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806072636_AddSoftDelete'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806072636_AddSoftDelete', N'10.0.3');
END;

COMMIT;
GO

