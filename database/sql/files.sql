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
    WHERE [MigrationId] = N'20260806035121_InitialFileSchema'
)
BEGIN
    IF SCHEMA_ID(N'files') IS NULL EXEC(N'CREATE SCHEMA [files];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035121_InitialFileSchema'
)
BEGIN
    CREATE TABLE [files].[Files] (
        [Id] uniqueidentifier NOT NULL,
        [OriginalName] nvarchar(max) NOT NULL,
        [StoragePath] nvarchar(max) NOT NULL,
        [ContentType] nvarchar(max) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [UploadedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Files] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806035121_InitialFileSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806035121_InitialFileSchema', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004142642_AddManagedPdfUploads'
)
BEGIN
    CREATE TABLE [files].[PdfUploads] (
        [FileId] uniqueidentifier NOT NULL,
        [UploaderUserId] uniqueidentifier NOT NULL,
        [StorageKey] nvarchar(36) NOT NULL,
        [OriginalName] nvarchar(200) NOT NULL,
        [State] nvarchar(16) NOT NULL,
        [FailureCode] nvarchar(64) NULL,
        [SizeBytes] bigint NULL,
        [Sha256] nvarchar(64) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [Version] bigint NOT NULL,
        [DocumentId] uniqueidentifier NULL,
        CONSTRAINT [PK_PdfUploads] PRIMARY KEY ([FileId]),
        CONSTRAINT [CK_PdfUpload_Size] CHECK ([SizeBytes] IS NULL OR [SizeBytes] BETWEEN 1 AND 26214400),
        CONSTRAINT [CK_PdfUpload_State] CHECK ([State] IN ('Receiving','Available','PendingScan','Rejected','Failed','Missing')),
        CONSTRAINT [CK_PdfUpload_Version] CHECK ([Version] >= 1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004142642_AddManagedPdfUploads'
)
BEGIN
    CREATE INDEX [IX_PdfUploads_State_CreatedAt] ON [files].[PdfUploads] ([State], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004142642_AddManagedPdfUploads'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PdfUploads_StorageKey] ON [files].[PdfUploads] ([StorageKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004142642_AddManagedPdfUploads'
)
BEGIN
    CREATE INDEX [IX_PdfUploads_UploaderUserId] ON [files].[PdfUploads] ([UploaderUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004142642_AddManagedPdfUploads'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004142642_AddManagedPdfUploads', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005024053_AddPdfClaims'
)
BEGIN
    CREATE TABLE [files].[PdfClaims] (
        [OperationId] uniqueidentifier NOT NULL,
        [FileId] uniqueidentifier NOT NULL,
        [DocumentId] uniqueidentifier NOT NULL,
        [UploaderUserId] uniqueidentifier NOT NULL,
        [ExpectedVersion] bigint NOT NULL,
        [State] nvarchar(16) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [Version] bigint NOT NULL,
        CONSTRAINT [PK_PdfClaims] PRIMARY KEY ([OperationId]),
        CONSTRAINT [CK_PdfClaim_State] CHECK ([State] IN ('Prepared','Active','Retired','Deleted')),
        CONSTRAINT [CK_PdfClaim_Version] CHECK ([Version] >= 1 AND [ExpectedVersion] >= 1),
        CONSTRAINT [FK_PdfClaims_PdfUploads_FileId] FOREIGN KEY ([FileId]) REFERENCES [files].[PdfUploads] ([FileId]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005024053_AddPdfClaims'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PdfClaims_FileId] ON [files].[PdfClaims] ([FileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005024053_AddPdfClaims'
)
BEGIN
    CREATE INDEX [IX_PdfClaims_State] ON [files].[PdfClaims] ([State]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005024053_AddPdfClaims'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005024053_AddPdfClaims', N'10.0.3');
END;

COMMIT;
GO

