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
    WHERE [MigrationId] = N'20261005052849_DurableNotificationsBaseline'
)
BEGIN
    IF SCHEMA_ID(N'notification') IS NULL EXEC(N'CREATE SCHEMA [notification];');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052849_DurableNotificationsBaseline'
)
BEGIN
    CREATE TABLE [notification].[DeliveryInbox] (
        [Id] uniqueidentifier NOT NULL,
        [SenderId] uniqueidentifier NOT NULL,
        [KeyHash] nvarchar(64) NOT NULL,
        [BodyHash] nvarchar(64) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [State] nvarchar(32) NOT NULL,
        [Attempts] int NOT NULL,
        [LeaseToken] uniqueidentifier NULL,
        [LeaseUntilUnix] bigint NOT NULL,
        [NextAttemptUnix] bigint NOT NULL,
        [Version] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DeliveryInbox] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052849_DurableNotificationsBaseline'
)
BEGIN
    CREATE TABLE [notification].[InAppNotifications] (
        [Id] uniqueidentifier NOT NULL,
        [RecipientUserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [Message] nvarchar(1000) NOT NULL,
        [ActionUrl] nvarchar(500) NULL,
        [RelatedDocumentId] uniqueidentifier NULL,
        [NotificationType] nvarchar(50) NOT NULL,
        [IsRead] bit NOT NULL,
        [ReadAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_InAppNotifications] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052849_DurableNotificationsBaseline'
)
BEGIN
    CREATE TABLE [notification].[NotificationLogs] (
        [Id] uniqueidentifier NOT NULL,
        [RecipientUserId] uniqueidentifier NULL,
        [RecipientEmail] nvarchar(200) NOT NULL,
        [Subject] nvarchar(300) NOT NULL,
        [RelatedDocumentId] uniqueidentifier NULL,
        [Status] nvarchar(20) NOT NULL,
        [SentAt] datetime2 NOT NULL,
        [ErrorMessage] nvarchar(1000) NULL,
        CONSTRAINT [PK_NotificationLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052849_DurableNotificationsBaseline'
)
BEGIN
    CREATE TABLE [notification].[UserNotificationPreferences] (
        [UserId] uniqueidentifier NOT NULL,
        [EmailEnabled] bit NOT NULL,
        [InAppEnabled] bit NOT NULL,
        [UrgentOnly] bit NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_UserNotificationPreferences] PRIMARY KEY ([UserId])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052849_DurableNotificationsBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DeliveryInbox_SenderId_KeyHash] ON [notification].[DeliveryInbox] ([SenderId], [KeyHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052849_DurableNotificationsBaseline'
)
BEGIN
    CREATE INDEX [IX_DeliveryInbox_State_NextAttemptUnix] ON [notification].[DeliveryInbox] ([State], [NextAttemptUnix]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052849_DurableNotificationsBaseline'
)
BEGIN
    CREATE INDEX [IX_InAppNotifications_RecipientUserId_IsRead_CreatedAt] ON [notification].[InAppNotifications] ([RecipientUserId], [IsRead], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052849_DurableNotificationsBaseline'
)
BEGIN
    CREATE INDEX [IX_NotificationLogs_SentAt] ON [notification].[NotificationLogs] ([SentAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261005052849_DurableNotificationsBaseline'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261005052849_DurableNotificationsBaseline', N'10.0.0');
END;

COMMIT;
GO

