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
    WHERE [MigrationId] = N'20260728074907_InitialCreate'
)
BEGIN
    IF SCHEMA_ID(N'auth') IS NULL EXEC(N'CREATE SCHEMA [auth];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728074907_InitialCreate'
)
BEGIN
    CREATE TABLE [auth].[Departments] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728074907_InitialCreate'
)
BEGIN
    CREATE TABLE [auth].[RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Token] nvarchar(450) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [RevokedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728074907_InitialCreate'
)
BEGIN
    CREATE TABLE [auth].[Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728074907_InitialCreate'
)
BEGIN
    CREATE TABLE [auth].[Users] (
        [Id] uniqueidentifier NOT NULL,
        [Username] nvarchar(450) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [FullName] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NULL,
        [Phone] nvarchar(max) NULL,
        [DepartmentId] uniqueidentifier NULL,
        [IsActive] bit NOT NULL,
        [LastLoginAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728074907_InitialCreate'
)
BEGIN
    CREATE TABLE [auth].[UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [auth].[Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [auth].[Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728074907_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Departments_Code] ON [auth].[Departments] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728074907_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_Token] ON [auth].[RefreshTokens] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728074907_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [auth].[UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728074907_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [auth].[Users] ([Username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728074907_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260728074907_InitialCreate', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810130346_ImproveAuthModel'
)
BEGIN
    ALTER TABLE [auth].[Users] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810130346_ImproveAuthModel'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[auth].[Roles]') AND [c].[name] = N'Name');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [auth].[Roles] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [auth].[Roles] ALTER COLUMN [Name] nvarchar(450) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810130346_ImproveAuthModel'
)
BEGIN
    ALTER TABLE [auth].[Departments] ADD [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810130346_ImproveAuthModel'
)
BEGIN
    ALTER TABLE [auth].[Departments] ADD [UpdatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810130346_ImproveAuthModel'
)
BEGIN
    CREATE INDEX [IX_Users_DepartmentId] ON [auth].[Users] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810130346_ImproveAuthModel'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Roles_Name] ON [auth].[Roles] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810130346_ImproveAuthModel'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [auth].[RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810130346_ImproveAuthModel'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE Name = 'Admin')
        INSERT INTO auth.Roles (Id, Name, Description) VALUES (NEWID(), 'Admin', 'System administrator');
    IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE Name = 'SecretaryDirector')
        INSERT INTO auth.Roles (Id, Name, Description) VALUES (NEWID(), 'SecretaryDirector', 'Director secretary');
    IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE Name = 'SecretaryDept')
        INSERT INTO auth.Roles (Id, Name, Description) VALUES (NEWID(), 'SecretaryDept', 'Department secretary');
    IF NOT EXISTS (SELECT 1 FROM auth.Roles WHERE Name = 'Staff')
        INSERT INTO auth.Roles (Id, Name, Description) VALUES (NEWID(), 'Staff', 'Department staff');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810130346_ImproveAuthModel'
)
BEGIN
    ALTER TABLE [auth].[RefreshTokens] ADD CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [auth].[Users] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810130346_ImproveAuthModel'
)
BEGIN
    ALTER TABLE [auth].[Users] ADD CONSTRAINT [FK_Users_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [auth].[Departments] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260810130346_ImproveAuthModel'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260810130346_ImproveAuthModel', N'10.0.3');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004054605_AddDirectoryProjection'
)
BEGIN
    CREATE TABLE [auth].[DirectoryInbox] (
        [SourceId] nvarchar(64) NOT NULL,
        [MessageId] nvarchar(128) NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [Result] nvarchar(max) NOT NULL,
        [ReceivedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_DirectoryInbox] PRIMARY KEY ([SourceId], [MessageId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004054605_AddDirectoryProjection'
)
BEGIN
    CREATE TABLE [auth].[DirectoryOutbox] (
        [Id] uniqueidentifier NOT NULL,
        [SourceId] nvarchar(64) NOT NULL,
        [AuthorizationRevision] bigint NOT NULL,
        [Sequence] bigint NOT NULL,
        [EventType] nvarchar(64) NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [PublishedAt] datetimeoffset NULL,
        CONSTRAINT [PK_DirectoryOutbox] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004054605_AddDirectoryProjection'
)
BEGIN
    CREATE TABLE [auth].[DirectoryProjections] (
        [SourceId] nvarchar(64) NOT NULL,
        [Sequence] bigint NOT NULL,
        [AuthorizationRevision] bigint NOT NULL,
        [Fingerprint] nvarchar(64) NOT NULL,
        [Payload] nvarchar(max) NOT NULL,
        [VerifiedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_DirectoryProjections] PRIMARY KEY ([SourceId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004054605_AddDirectoryProjection'
)
BEGIN
    CREATE INDEX [IX_DirectoryOutbox_PublishedAt] ON [auth].[DirectoryOutbox] ([PublishedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004054605_AddDirectoryProjection'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DirectoryOutbox_SourceId_AuthorizationRevision] ON [auth].[DirectoryOutbox] ([SourceId], [AuthorizationRevision]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261004054605_AddDirectoryProjection'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261004054605_AddDirectoryProjection', N'10.0.3');
END;

COMMIT;
GO

