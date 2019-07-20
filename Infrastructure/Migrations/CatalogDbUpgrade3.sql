CREATE TABLE [PluginAssembly] (
    [ObjectId] bigint NOT NULL IDENTITY,
    [Name] nvarchar(128) NULL,
    [Description] nvarchar(512) NULL,
    [FileName] nvarchar(1024) NULL,
    [Enabled] bit NOT NULL,
    [PluginType] int NOT NULL,
    CONSTRAINT [PK_PluginAssembly] PRIMARY KEY ([ObjectId])
);

GO

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20190720142945_BaseEntity1', N'2.2.3-servicing-35854');

GO

