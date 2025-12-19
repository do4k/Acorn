-- Migration: Convert from serialized strings to relational tables for Inventory and Paperdoll
-- This migration creates the new tables and migrates existing data

-- Create CharacterItems table
CREATE TABLE IF NOT EXISTS CharacterItems
(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CharacterName TEXT NOT NULL,
    ItemId INTEGER NOT NULL,
    Amount INTEGER NOT NULL,
    Slot INTEGER NOT NULL,  -- 0 = Inventory, 1 = Bank
    FOREIGN KEY (CharacterName) REFERENCES Characters(Name) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_CharacterItems_CharacterName_Slot 
    ON CharacterItems(CharacterName, Slot);

-- Create CharacterPaperdolls table
CREATE TABLE IF NOT EXISTS CharacterPaperdolls
(
    CharacterName TEXT PRIMARY KEY NOT NULL,
    Hat INTEGER NOT NULL DEFAULT 0,
    Necklace INTEGER NOT NULL DEFAULT 0,
    Armor INTEGER NOT NULL DEFAULT 0,
    Belt INTEGER NOT NULL DEFAULT 0,
    Boots INTEGER NOT NULL DEFAULT 0,
    Gloves INTEGER NOT NULL DEFAULT 0,
    Weapon INTEGER NOT NULL DEFAULT 0,
    Shield INTEGER NOT NULL DEFAULT 0,
    Accessory INTEGER NOT NULL DEFAULT 0,
    Ring1 INTEGER NOT NULL DEFAULT 0,
    Ring2 INTEGER NOT NULL DEFAULT 0,
    Bracer1 INTEGER NOT NULL DEFAULT 0,
    Bracer2 INTEGER NOT NULL DEFAULT 0,
    Armlet1 INTEGER NOT NULL DEFAULT 0,
    Armlet2 INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (CharacterName) REFERENCES Characters(Name) ON DELETE CASCADE
);

-- Note: Data migration would need to be done programmatically or manually
-- as SQLite doesn't support complex string parsing in SQL.
-- The old Inventory, Bank, and Paperdoll columns can be dropped after migration:
-- ALTER TABLE Characters DROP COLUMN Inventory;
-- ALTER TABLE Characters DROP COLUMN Bank;
-- ALTER TABLE Characters DROP COLUMN Paperdoll;
