CREATE TABLE IF NOT EXISTS Accounts
(
    Username
    TEXT
    NOT
    NULL,
    Password
    TEXT
    NOT
    NULL,
    Salt
    TEXT
    NOT
    NULL,
    FullName
    TEXT
    NOT
    NULL,
    Location
    TEXT
    NOT
    NULL,
    Email
    TEXT
    NOT
    NULL,
    Country
    TEXT
    NOT
    NULL,
    Created
    DATETIME
    NOT
    NULL,
    LastUsed
    DATETIME
    NOT
    NULL
);

INSERT
OR REPLACE INTO Accounts
(
    Username,
    Password,
    Salt,
    FullName,
    Location,
    Email,
    Country,
    Created,
    LastUsed
)
VALUES
(
    'acorn',
    '1I+dieTmkT9qbF9YjSt1pkRvgAkAHqcStjRxOzuHwSc=',
    'acorn',
    'acorn',
    'acorn',
    'acorn@acorn-eo.dev',
    'acorn',
    '2024-08-31 00:00:00',
    '2024-08-31 00:00:00'
);

CREATE TABLE IF NOT EXISTS Characters
(
    Accounts_Username
    TEXT
    NOT
    NULL,
    Name
    TEXT
    NOT
    NULL,
    Title
    TEXT,
    Home
    TEXT,
    Fiance
    TEXT,
    Partner
    TEXT,
    Admin
    INTEGER
    NOT
    NULL,
    Class
    INTEGER
    NOT
    NULL,
    Gender
    INTEGER
    NOT
    NULL,
    Race
    INTEGER
    NOT
    NULL,
    HairStyle
    INTEGER
    NOT
    NULL,
    HairColor
    INTEGER
    NOT
    NULL,
    Map
    INTEGER
    NOT
    NULL,
    X
    INTEGER
    NOT
    NULL,
    Y
    INTEGER
    NOT
    NULL,
    Direction
    INTEGER
    NOT
    NULL,
    Level
    INTEGER
    NOT
    NULL,
    Exp
    INTEGER
    NOT
    NULL,
    Hp
    INTEGER
    NOT
    NULL,
    Tp
    INTEGER
    NOT
    NULL,
    Str
    INTEGER
    NOT
    NULL,
    Wis
    INTEGER
    NOT
    NULL,
    Agi
    INTEGER
    NOT
    NULL,
    Con
    INTEGER
    NOT
    NULL,
    Cha
    INTEGER
    NOT
    NULL,
    StatPoints
    INTEGER
    NOT
    NULL,
    SkillPoints
    INTEGER
    NOT
    NULL,
    Karma
    INTEGER
    NOT
    NULL,
    SitState
    INTEGER
    NOT
    NULL,
    Hidden
    INTEGER
    NOT
    NULL,
    NoInteract
    INTEGER
    NOT
    NULL,
    BankMax
    INTEGER
    NOT
    NULL,
    GoldBank
    INTEGER
    NOT
    NULL,
    `Usage`
    INTEGER
    NOT
    NULL
);

INSERT
OR REPLACE INTO Characters
(
    Accounts_Username,
    Name,
    Title,
    Home,
    Fiance,
    Partner,
    Admin,
    Class,
    Gender,
    Race,
    HairStyle,
    HairColor,
    Map,
    X,
    Y,
    Direction,
    Level,
    Exp,
    Hp,
    Tp,
    Str,
    Wis,
    Agi,
    Con,
    Cha,
    StatPoints,
    SkillPoints,
    Karma,
    SitState,
    Hidden,
    NoInteract,
    BankMax,
    GoldBank,
    Usage
)
VALUES 
(
    'acorn', --Accounts_Username
    'acorn', --Name
    'acorn', --Title
    'acorn', --Home
    '', --Fiance
    '', --Partner
    5, --Admin
    1, --Class
    1, --Gender
    5, --Race
    1, --HairStyle
    1, --HairColor
    192, --Map
    6, --X
    6, --Y
    0, --Direction
    100, --Level
    0, --Exp
    100, --Hp
    100, --Tp
    10, --Str
    10, --Wis
    10, --Agi
    10, --Con
    10, --Cha
    0, --StatPoints
    0, --SkillPoints
    0, --Karma
    0, --SitState
    0, --Hidden
    0, --NoInteract
    100, --BankMax
    10, --GoldBank
    0 --Usage
);

CREATE TABLE IF NOT EXISTS Inventory
(
    Characters_Name
    TEXT
    NOT
    NULL,
    ItemId
    INTEGER
    NOT
    NULL,
    Amount
    INTEGER
    NOT
    NULL
);

INSERT INTO Inventory
(Characters_Name,
 ItemId,
 Amount)
VALUES ('acorn',
        1,
        1000000);

CREATE TABLE IF NOT EXISTS Bank
(
    Characters_Name
    TEXT
    NOT
    NULL,
    ItemId
    INTEGER
    NOT
    NULL,
    Amount
    INTEGER
    NOT
    NULL
);

CREATE TABLE IF NOT EXISTS Paperdoll
(
    Characters_Name
    TEXT
    NOT
    NULL,
    Hat
    INTEGER
    NULL,
    Necklace
    INTEGER
    NULL,
    Armor
    INTEGER
    NULL,
    Belt
    INTEGER
    NULL,
    Boots
    INTEGER
    NULL,
    Gloves
    INTEGER
    NULL,
    Weapon
    INTEGER
    NULL,
    Shield
    INTEGER
    NULL,
    Accessory
    INTEGER
    NULL,
    Ring1
    INTEGER
    NULL,
    Ring2
    INTEGER
    NULL,
    Bracer1
    INTEGER
    NULL,
    Bracer2
    INTEGER
    NULL,
    Armlet1
    INTEGER
    NULL,
    Armlet2
    INTEGER
    NULL
);