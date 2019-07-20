CREATE TABLE [TodoItems] (
    [TodoItemId] int NOT NULL IDENTITY,
    [OwnerId] nvarchar(900) NULL,
    [IsDone] bit NOT NULL,
    [Title] nvarchar(1024) NOT NULL,
    [DueAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_TodoItems] PRIMARY KEY ([TodoItemId])
);

GO

select * from TodoItems

CREATE TABLE [TodoItem] (
    [TodoItemId] int NOT NULL IDENTITY,
    [OwnerId] nvarchar(900) NULL,
    [IsDone] bit NOT NULL,
    [Title] nvarchar(1024) NOT NULL,
    [DueAt] datetimeoffset NOT NULL,
    CONSTRAINT [PK_TodoItem] PRIMARY KEY ([TodoItemId])
);

insert into TodoItem
(OwnerId, IsDone, Title, DueAt)
select OwnerId, IsDone, Title, DueAt
from TodoItems

drop table TodoItems

sp_rename 'TodoItem', 'TodoItems'