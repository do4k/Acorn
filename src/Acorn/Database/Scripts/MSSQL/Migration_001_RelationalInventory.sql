-- Migration: Convert from serialized strings to relational tables for Inventory and Paperdoll
-- This migration creates the new tables and migrates existing data

USE EOSERV;
GO

-- Create CharacterItems table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CharacterItems]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CharacterItems]
    (
        [Id] INT IDENTITY(1,1) PRIMARY KEY,
        [CharacterName] NVARCHAR(16) NOT NULL,
        [ItemId] INT NOT NULL,
        [Amount] INT NOT NULL,
        [Slot] INT NOT NULL,  -- 0 = Inventory, 1 = Bank
        CONSTRAINT FK_CharacterItems_Characters FOREIGN KEY (CharacterName) 
            REFERENCES [dbo].[Characters](Name) ON DELETE CASCADE
    );
    
    CREATE INDEX IX_CharacterItems_CharacterName_Slot 
        ON [dbo].[CharacterItems](CharacterName, Slot);
END
GO

-- Create CharacterPaperdolls table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CharacterPaperdolls]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[CharacterPaperdolls]
    (
        [CharacterName] NVARCHAR(16) PRIMARY KEY NOT NULL,
        [Hat] INT NOT NULL DEFAULT 0,
        [Necklace] INT NOT NULL DEFAULT 0,
        [Armor] INT NOT NULL DEFAULT 0,
        [Belt] INT NOT NULL DEFAULT 0,
        [Boots] INT NOT NULL DEFAULT 0,
        [Gloves] INT NOT NULL DEFAULT 0,
        [Weapon] INT NOT NULL DEFAULT 0,
        [Shield] INT NOT NULL DEFAULT 0,
        [Accessory] INT NOT NULL DEFAULT 0,
        [Ring1] INT NOT NULL DEFAULT 0,
        [Ring2] INT NOT NULL DEFAULT 0,
        [Bracer1] INT NOT NULL DEFAULT 0,
        [Bracer2] INT NOT NULL DEFAULT 0,
        [Armlet1] INT NOT NULL DEFAULT 0,
        [Armlet2] INT NOT NULL DEFAULT 0,
        CONSTRAINT FK_CharacterPaperdolls_Characters FOREIGN KEY (CharacterName) 
            REFERENCES [dbo].[Characters](Name) ON DELETE CASCADE
    );
END
GO

-- Note: Data migration would need to be done programmatically
-- The old Inventory, Bank, and Paperdoll columns can be dropped after migration:
-- ALTER TABLE Characters DROP COLUMN Inventory;
-- ALTER TABLE Characters DROP COLUMN Bank;
-- ALTER TABLE Characters DROP COLUMN Paperdoll;
GO
