Script-Migration -From InitCatalogDb -To CatalogDbUpgrade1 -Context CatalogContext -Project Infrastructure

insert into AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
LockoutEnd, LockoutEnabled, AccessFailedCount)
select Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
LockoutEnd, LockoutEnabled, AccessFailedCount from [TrunkDB].dbo.AspNetUsers
GO
insert into UserProfile (FirstName, LastName, Email, ApplicationUserId, 
AchievedLevel, AchievedPoints, Continent, Created, ExperienceLevel,
LastUpdated, LatestLogin, UserStatus)
select UserName, Email, Email, Id, 
AchievedLevel, AchievedPoints, Continent, Created, ExperienceLevel,
LastUpdated, LatestLogin, UserStatus from [TrunkDB].dbo.AspNetUsers
GO
alter table TestImages add Id nvarchar(900) null
GO
insert into TestImages (Id, ContentType, Data, Height, Length, Name, Width)
select Id, ContentType, Data, Height, Length, Name, Width
from [TrunkDB].dbo.TestImages
GO
insert into TestQuestions (Category, Level, QuestionImageId, AnswerStream)
select q.Category, q.Level, g.ObjectId, q.AnswerStream from TestImages g, [TrunkDB].dbo.TestQuestions q
where g.Id = q.QuestionImageId
GO
insert into TestSessions (Name, Description, TestQuestionData, PlannedStart, PlannedEnd, TesterData, LastUpdated, TargetGrade)
select Name, Description, TestQuestionData, PlannedStart, PlannedEnd, TesterData, LastUpdated, TargetGrade
from [TrunkDB].dbo.TestSessions
Go
insert into TestResults (TestSessionId, UserId, FinalScore, MaximumScore, Percentile, TestStarted, TestEnded, TestResultData)
select TestSessionId, newP.UserProfileId, FinalScore, MaximumScore, Percentile, TestStarted, TestEnded, TestResultData
From 
[TrunkDB].dbo.TestResults old, [TrunkDb].dbo.AspNetUsers oldUser, dbo.UserProfile newP
where old.UserId = oldUser.ObjectId and oldUser.Id = newP.ApplicationUserId
insert into TestGroups
(Name, Description, LastUpdated, TeamHeadId, MembersInfo, EnrolledSessionsInfo)
select Name, Description, LastUpdated, TeamHeadId, MembersInfo, EnrolledSessionsInfo
  from 
[TrunkDB].dbo.TestGroups 