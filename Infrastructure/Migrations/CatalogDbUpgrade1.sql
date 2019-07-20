
CREATE TABLE [Baskets] (
    [BasketId] int NOT NULL IDENTITY,
    [BuyerId] nvarchar(900) NULL,
    CONSTRAINT [PK_Baskets] PRIMARY KEY ([BasketId])
);

GO

CREATE TABLE [TestGroup] (
    [TestGroupId] int NOT NULL IDENTITY,
    [Name] nvarchar(128) NULL,
    [Description] nvarchar(512) NULL,
    [LastUpdated] datetime2 NOT NULL,
    [TeamHeadId] int NOT NULL,
    [MembersInfo] nvarchar(max) NULL,
    [EnrolledSessionsInfo] nvarchar(max) NULL,
    CONSTRAINT [PK_TestGroup] PRIMARY KEY ([TestGroupId])
);

GO

CREATE TABLE [TestImage] (
    [ObjectId] int NOT NULL IDENTITY,
    [Name] nvarchar(128) NULL,
    [Data] varbinary(max) NULL,
    [Length] int NOT NULL,
    [Width] int NOT NULL,
    [Height] int NOT NULL,
    [ContentType] nvarchar(32) NULL,
    CONSTRAINT [PK_TestImage] PRIMARY KEY ([ObjectId])
);

GO

CREATE TABLE [TestQuestion] (
    [ObjectId] int NOT NULL IDENTITY,
    [Category] int NOT NULL,
    [Level] int NOT NULL,
    [QuestionImageId] int NOT NULL,
    [AnswerStream] varbinary(max) NULL,
    CONSTRAINT [PK_TestQuestion] PRIMARY KEY ([ObjectId])
);

GO

CREATE TABLE [TestResult] (
    [Id] int NOT NULL IDENTITY,
    [TestSessionId] int NOT NULL,
    [UserId] int NOT NULL,
    [FinalScore] float NOT NULL,
    [MaximumScore] float NOT NULL,
    [Percentile] float NOT NULL,
    [TestStarted] datetime2 NOT NULL,
    [TestEnded] datetime2 NOT NULL,
    [TestResultData] varbinary(max) NULL,
    CONSTRAINT [PK_TestResult] PRIMARY KEY ([Id])
);

GO

CREATE TABLE [TestSession] (
    [ObjectId] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NULL,
    [Description] nvarchar(max) NULL,
    [TestQuestionData] varbinary(max) NULL,
    [PlannedStart] datetime2 NOT NULL,
    [PlannedEnd] datetime2 NOT NULL,
    [TesterData] varbinary(max) NULL,
    [LastUpdated] datetime2 NOT NULL,
    [TargetGrade] int NOT NULL,
    CONSTRAINT [PK_TestSession] PRIMARY KEY ([ObjectId])
);

GO

CREATE TABLE [BasketItems] (
    [BasketItemId] int NOT NULL IDENTITY,
    [UnitPrice] decimal(18,2) NOT NULL,
    [Quantity] int NOT NULL,
    [CatalogItemId] int NOT NULL,
    [BasketId] int NULL,
    CONSTRAINT [PK_BasketItems] PRIMARY KEY ([BasketItemId]),
    CONSTRAINT [FK_BasketItems_Baskets_BasketId] FOREIGN KEY ([BasketId]) REFERENCES [Baskets] ([BasketId]) ON DELETE NO ACTION
);

GO

CREATE INDEX [IX_BasketItems_BasketId] ON [BasketItems] ([BasketId]);

GO


