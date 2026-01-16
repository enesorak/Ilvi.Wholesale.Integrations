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
    WHERE [MigrationId] = N'20251222025011_InitialCreate'
)
BEGIN
    CREATE TABLE [Companies] (
        [Id] bigint NOT NULL,
        [AccountId] bigint NOT NULL,
        [ResponsibleUserId] bigint NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Contact] nvarchar(max) NULL,
        [Lead] nvarchar(max) NULL,
        [Tag] nvarchar(max) NULL,
        [Raw] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [LastSyncDate] datetime2 NOT NULL,
        [ComputedHash] nchar(64) NOT NULL,
        CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222025011_InitialCreate'
)
BEGIN
    CREATE TABLE [Contacts] (
        [Id] bigint NOT NULL,
        [AccountId] bigint NOT NULL,
        [ResponsibleUserId] bigint NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Lead] nvarchar(max) NULL,
        [Company] nvarchar(max) NULL,
        [Tag] nvarchar(max) NULL,
        [Raw] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [LastSyncDate] datetime2 NOT NULL,
        [ComputedHash] nchar(64) NOT NULL,
        CONSTRAINT [PK_Contacts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222025011_InitialCreate'
)
BEGIN
    CREATE TABLE [Events] (
        [Id] nvarchar(450) NOT NULL,
        [Type] nvarchar(100) NOT NULL,
        [EntityId] bigint NOT NULL,
        [EntityType] nvarchar(50) NOT NULL,
        [CreatedBy] bigint NOT NULL,
        [ValueAfter] nvarchar(max) NULL,
        [ValueBefore] nvarchar(max) NULL,
        [Raw] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [LastSyncDate] datetime2 NOT NULL,
        [ComputedHash] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Events] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222025011_InitialCreate'
)
BEGIN
    CREATE TABLE [Leads] (
        [Id] bigint NOT NULL,
        [AccountId] bigint NOT NULL,
        [ResponsibleUserId] bigint NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Price] int NOT NULL,
        [StatusId] int NOT NULL,
        [PipelineId] int NOT NULL,
        [LossReasonId] int NULL,
        [Contact] nvarchar(max) NULL,
        [Company] nvarchar(max) NULL,
        [Tag] nvarchar(max) NULL,
        [Raw] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [LastSyncDate] datetime2 NOT NULL,
        [ComputedHash] nchar(64) NOT NULL,
        CONSTRAINT [PK_Leads] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222025011_InitialCreate'
)
BEGIN
    CREATE TABLE [Messages] (
        [Id] nvarchar(450) NOT NULL,
        [ChatId] bigint NOT NULL,
        [ContactId] bigint NOT NULL,
        [EntityId] bigint NOT NULL,
        [AuthorId] bigint NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [Text] nvarchar(4000) NOT NULL,
        [Raw] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [LastSyncDate] datetime2 NOT NULL,
        [ComputedHash] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222025011_InitialCreate'
)
BEGIN
    CREATE TABLE [Pipelines] (
        [Id] int NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Sort] int NOT NULL,
        [IsMain] bit NOT NULL,
        [Statuses] nvarchar(max) NULL,
        [Raw] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [LastSyncDate] datetime2 NOT NULL,
        [ComputedHash] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Pipelines] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222025011_InitialCreate'
)
BEGIN
    CREATE TABLE [Tasks] (
        [Id] bigint NOT NULL,
        [AccountId] bigint NOT NULL,
        [ResponsibleUserId] bigint NOT NULL,
        [Text] nvarchar(4000) NOT NULL,
        [TaskTypeId] int NOT NULL,
        [IsCompleted] bit NOT NULL,
        [CompleteTill] datetime2 NULL,
        [ResultText] nvarchar(4000) NULL,
        [Lead] nvarchar(max) NULL,
        [Company] nvarchar(max) NULL,
        [Contact] nvarchar(max) NULL,
        [Raw] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [LastSyncDate] datetime2 NOT NULL,
        [ComputedHash] nchar(64) NOT NULL,
        CONSTRAINT [PK_Tasks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222025011_InitialCreate'
)
BEGIN
    CREATE TABLE [TaskTypes] (
        [Id] int NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [Color] nvarchar(50) NOT NULL,
        [IconId] int NOT NULL,
        [Raw] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [LastSyncDate] datetime2 NOT NULL,
        [ComputedHash] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_TaskTypes] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222025011_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] bigint NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [Raw] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        [LastSyncDate] datetime2 NOT NULL,
        [ComputedHash] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222025011_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251222025011_InitialCreate', N'9.0.2');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20251222062825_FixColumnNames'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20251222062825_FixColumnNames', N'9.0.2');
END;

COMMIT;
GO

