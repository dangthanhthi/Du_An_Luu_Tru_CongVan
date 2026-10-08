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
    WHERE [MigrationId] = N'20260810113448_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'partner') IS NULL EXEC(N'CREATE SCHEMA [partner];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810113448_InitialCreate'
)
BEGIN
    CREATE TABLE [partner].[Partners] (
        [Id] uniqueidentifier NOT NULL,
        [FullName] nvarchar(max) NOT NULL,
        [ShortName] nvarchar(450) NOT NULL,
        [EntityType] nvarchar(450) NOT NULL,
        [Email] nvarchar(max) NULL,
        [Phone] nvarchar(max) NULL,
        [Address] nvarchar(max) NULL,
        [TaxCode] nvarchar(450) NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_Partners] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810113448_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Partners_EntityType] ON [partner].[Partners] ([EntityType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810113448_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Partners_IsDeleted_IsActive] ON [partner].[Partners] ([IsDeleted], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810113448_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Partners_ShortName] ON [partner].[Partners] ([ShortName]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810113448_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Partners_TaxCode] ON [partner].[Partners] ([TaxCode]) WHERE [TaxCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810113448_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260810113448_InitialCreate', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    DROP INDEX [IX_Partners_ShortName] ON [partner].[Partners];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    DROP INDEX [IX_Partners_TaxCode] ON [partner].[Partners];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[partner].[Partners]') AND [c].[name] = N'ShortName');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [partner].[Partners] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [partner].[Partners] ALTER COLUMN [ShortName] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    ALTER TABLE [partner].[Partners] ADD [ContactInformation] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    ALTER TABLE [partner].[Partners] ADD [ContactPerson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    ALTER TABLE [partner].[Partners] ADD [NormalizedShortName] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    ALTER TABLE [partner].[Partners] ADD [NormalizedTaxCode] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    ALTER TABLE [partner].[Partners] ADD [Version] bigint NOT NULL DEFAULT CAST(1 AS bigint);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    CREATE TABLE [partner].[PartnerAudits] (
        [Id] uniqueidentifier NOT NULL,
        [PartnerId] uniqueidentifier NOT NULL,
        [ActorUserId] uniqueidentifier NOT NULL,
        [Action] nvarchar(20) NOT NULL,
        [Version] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PartnerAudits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PartnerAudits_Partners_PartnerId] FOREIGN KEY ([PartnerId]) REFERENCES [partner].[Partners] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    UPDATE [partner].[Partners] SET
      [NormalizedShortName] = NULLIF(UPPER(LTRIM(RTRIM([ShortName]))), N''),
      [NormalizedTaxCode] = NULLIF(UPPER(LTRIM(RTRIM([TaxCode]))), N'');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Partners_NormalizedShortName] ON [partner].[Partners] ([NormalizedShortName]) WHERE [NormalizedShortName] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Partners_NormalizedTaxCode] ON [partner].[Partners] ([NormalizedTaxCode]) WHERE [NormalizedTaxCode] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PartnerAudits_PartnerId_Version] ON [partner].[PartnerAudits] ([PartnerId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005043235_ExternalEntityContactsAndConcurrency'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005043235_ExternalEntityContactsAndConcurrency', N'10.0.3');
END;

COMMIT;
GO

