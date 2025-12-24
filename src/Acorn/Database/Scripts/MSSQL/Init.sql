IF
NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'EOSERV')
BEGIN
    CREATE
DATABASE EOSERV;
END
GO

USE EOSERV;

IF
NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Accounts]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Accounts]
(
    [
    Username]
    NVARCHAR
(
    16
) NOT NULL,
    [Password] NVARCHAR
(
    64
) NOT NULL,
    [FullName] NVARCHAR
(
    64
) NOT NULL,
    [Location] NVARCHAR
(
    64
) NOT NULL,
    [Email] NVARCHAR
(
    64
) NOT NULL,
    [Country] NVARCHAR
(
    64
) NOT NULL,
    [Created] DATETIME NOT NULL,
    [LastUsed] DATETIME NOT NULL
    );
END

IF
NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Characters]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[Characters]
(
    [
    Accounts_Username]
    NVARCHAR
(
    64
) NOT NULL,
    [Name] NVARCHAR
(
    50
) NOT NULL,
    [Title] NVARCHAR
(
    50
) NOT NULL,
    [Home] NVARCHAR
(
    50
) NOT NULL,
    [Fiance] NVARCHAR
(
    50
) NOT NULL,
    [Partner] NVARCHAR
(
    50
) NOT NULL,
    [Admin] INT NOT NULL,
    [Class] INT NOT NULL,
    [Gender] INT NOT NULL,
    [Race] INT NOT NULL,
    [HairStyle] INT NOT NULL,
    [HairColor] INT NOT NULL,
    [Map] INT NOT NULL,
    [X] INT NOT NULL,
    [Y] INT NOT NULL,
    [Direction] INT NOT NULL,
    [Level] INT NOT NULL,
    [Exp] INT NOT NULL,
    [Hp] INT NOT NULL,
    [Tp] INT NOT NULL,
    [Str] INT NOT NULL,
    [Wis] INT NOT NULL,
    [Agi] INT NOT NULL,
    [Con] INT NOT NULL,
    [Cha] INT NOT NULL,
    [StatPoints] INT NOT NULL,
    [SkillPoints] INT NOT NULL,
    [Karma] INT NOT NULL,
    [Sitting] BIT NOT NULL,
    [Hidden] BIT NOT NULL,
    [NoInteract] BIT NOT NULL,
    [BankMax] INT NOT NULL,
    [GoldBank] INT NOT NULL,
    [Usage] INT NOT NULL,
    [Inventory] NVARCHAR
(
    MAX
) NOT NULL,
    [Bank] NVARCHAR
(
    MAX
) NOT NULL,
    [Paperdoll] NVARCHAR
(
    MAX
) NOT NULL,
    [Spells] NVARCHAR
(
    MAX
) NOT NULL,
    [Guild] NVARCHAR
(
    50
) NOT NULL,
    [GuildRank] INT NOT NULL,
    [GuildRankString] NVARCHAR
(
    50
) NOT NULL,
    [Quest] NVARCHAR
(
    MAX
) NOT NULL
    );
END

IF
NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'EOSERV')
BEGIN
    CREATE
LOGIN EOSERV WITH PASSWORD = 'EOSERV';
END
GO

USE EOSERV;
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'EOSERV')
BEGIN
    CREATE
USER EOSERV FOR LOGIN EOSERV;
END
GO

EXEC sp_addrolemember 'db_owner', 'EOSERV';
GO