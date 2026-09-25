package database

import (
	"context"
	"crypto/rand"
	"crypto/sha256"
	"database/sql"
	"encoding/hex"
	"fmt"

	"github.com/acorn-server/acorn-go/internal/session"
)

// AccountRow is the database representation of an account.
type AccountRow struct {
	ID       int64
	Username string
	Password string
	Salt     []byte
}

// CreateAccount inserts a new account. Returns the account ID.
func (db *DB) CreateAccount(ctx context.Context, username, password string) (int64, error) {
	salt := make([]byte, 16)
	if _, err := rand.Read(salt); err != nil {
		return 0, fmt.Errorf("generating salt: %w", err)
	}

	hash := hashPassword(username, password, salt)

	result, err := db.ExecContext(ctx,
		`INSERT INTO accounts (username, password, salt) VALUES (?, ?, ?)`,
		username, hash, salt,
	)
	if err != nil {
		return 0, fmt.Errorf("inserting account: %w", err)
	}

	return result.LastInsertId()
}

// GetAccount looks up an account by username. Returns nil if not found.
func (db *DB) GetAccount(ctx context.Context, username string) (*AccountRow, error) {
	row := db.QueryRowContext(ctx,
		`SELECT id, username, password, salt FROM accounts WHERE username = ?`,
		username,
	)

	var a AccountRow
	if err := row.Scan(&a.ID, &a.Username, &a.Password, &a.Salt); err != nil {
		if err == sql.ErrNoRows {
			return nil, nil
		}
		return nil, fmt.Errorf("querying account: %w", err)
	}

	return &a, nil
}

// AccountExists checks whether an account with the given username exists.
func (db *DB) AccountExists(ctx context.Context, username string) (bool, error) {
	var count int
	err := db.QueryRowContext(ctx,
		`SELECT COUNT(*) FROM accounts WHERE username = ?`,
		username,
	).Scan(&count)
	if err != nil {
		return false, fmt.Errorf("checking account exists: %w", err)
	}
	return count > 0, nil
}

// VerifyPassword checks if the given password matches the stored hash.
func (db *DB) VerifyPassword(account *AccountRow, password string) bool {
	hash := hashPassword(account.Username, password, account.Salt)
	return hash == account.Password
}

// hashPassword computes SHA-256(salt + username + password) and returns the hex digest.
// This matches eoserv's approach but uses a random salt per account for better security.
func hashPassword(username, password string, salt []byte) string {
	h := sha256.New()
	h.Write(salt)
	h.Write([]byte(username))
	h.Write([]byte(password))
	return hex.EncodeToString(h.Sum(nil))
}

// CharacterRow is the database representation of a character.
type CharacterRow struct {
	ID              int64
	AccountID       int64
	Name            string
	Title           string
	Home            string
	Partner         string
	Admin           int
	ClassID         int
	Gender          int
	Race            int
	HairStyle       int
	HairColor       int
	MapID           int
	MapX            int
	MapY            int
	Direction       int
	Level           int
	Experience      int
	HP              int
	MaxHP           int
	TP              int
	MaxTP           int
	Str             int
	Intl            int
	Wis             int
	Agi             int
	Con             int
	Cha             int
	StatPoints      int
	SkillPoints     int
	Karma           int
	Sitting         int
	Hidden          int
	GuildRank       int
	GuildRankString string
	BankMax         int
	GoldBank        int
	Usage           int
}

// ToSessionCharacter converts a database row to a session Character.
func (r *CharacterRow) ToSessionCharacter() *session.Character {
	return &session.Character{
		ID:              r.ID,
		AccountID:       r.AccountID,
		Name:            r.Name,
		Title:           r.Title,
		Home:            r.Home,
		Partner:         r.Partner,
		Admin:           r.Admin,
		ClassID:         r.ClassID,
		Gender:          r.Gender,
		Race:            r.Race,
		HairStyle:       r.HairStyle,
		HairColor:       r.HairColor,
		MapID:           r.MapID,
		MapX:            r.MapX,
		MapY:            r.MapY,
		Direction:       r.Direction,
		Level:           r.Level,
		Experience:      r.Experience,
		HP:              r.HP,
		MaxHP:           r.MaxHP,
		TP:              r.TP,
		MaxTP:           r.MaxTP,
		Str:             r.Str,
		Intl:            r.Intl,
		Wis:             r.Wis,
		Agi:             r.Agi,
		Con:             r.Con,
		Cha:             r.Cha,
		StatPoints:      r.StatPoints,
		SkillPoints:     r.SkillPoints,
		Karma:           r.Karma,
		Sitting:         r.Sitting,
		Hidden:          r.Hidden,
		GuildRank:       r.GuildRank,
		GuildRankString: r.GuildRankString,
		BankMax:         r.BankMax,
		GoldBank:        r.GoldBank,
		Usage:           r.Usage,
	}
}

// GetCharactersByAccount returns all characters belonging to an account.
func (db *DB) GetCharactersByAccount(ctx context.Context, accountID int64) ([]CharacterRow, error) {
	rows, err := db.QueryContext(ctx,
		`SELECT id, account_id, name, title, home, partner, admin, class_id, gender, race,
		        hair_style, hair_color, map_id, map_x, map_y, direction, level, experience,
		        hp, max_hp, tp, max_tp, str, intl, wis, agi, con, cha,
		        stat_points, skill_points, karma, sitting, hidden,
		        guild_rank, guild_rank_string, bank_max, gold_bank, usage
		 FROM characters WHERE account_id = ? ORDER BY experience DESC`,
		accountID,
	)
	if err != nil {
		return nil, fmt.Errorf("querying characters: %w", err)
	}
	defer rows.Close()

	var chars []CharacterRow
	for rows.Next() {
		var c CharacterRow
		if err := rows.Scan(
			&c.ID, &c.AccountID, &c.Name, &c.Title, &c.Home, &c.Partner,
			&c.Admin, &c.ClassID, &c.Gender, &c.Race,
			&c.HairStyle, &c.HairColor, &c.MapID, &c.MapX, &c.MapY, &c.Direction,
			&c.Level, &c.Experience, &c.HP, &c.MaxHP, &c.TP, &c.MaxTP,
			&c.Str, &c.Intl, &c.Wis, &c.Agi, &c.Con, &c.Cha,
			&c.StatPoints, &c.SkillPoints, &c.Karma, &c.Sitting, &c.Hidden,
			&c.GuildRank, &c.GuildRankString, &c.BankMax, &c.GoldBank, &c.Usage,
		); err != nil {
			return nil, fmt.Errorf("scanning character row: %w", err)
		}
		chars = append(chars, c)
	}

	return chars, rows.Err()
}

// GetCharacterByID loads a single character by ID.
func (db *DB) GetCharacterByID(ctx context.Context, id int64) (*CharacterRow, error) {
	row := db.QueryRowContext(ctx,
		`SELECT id, account_id, name, title, home, partner, admin, class_id, gender, race,
		        hair_style, hair_color, map_id, map_x, map_y, direction, level, experience,
		        hp, max_hp, tp, max_tp, str, intl, wis, agi, con, cha,
		        stat_points, skill_points, karma, sitting, hidden,
		        guild_rank, guild_rank_string, bank_max, gold_bank, usage
		 FROM characters WHERE id = ?`,
		id,
	)

	var c CharacterRow
	if err := row.Scan(
		&c.ID, &c.AccountID, &c.Name, &c.Title, &c.Home, &c.Partner,
		&c.Admin, &c.ClassID, &c.Gender, &c.Race,
		&c.HairStyle, &c.HairColor, &c.MapID, &c.MapX, &c.MapY, &c.Direction,
		&c.Level, &c.Experience, &c.HP, &c.MaxHP, &c.TP, &c.MaxTP,
		&c.Str, &c.Intl, &c.Wis, &c.Agi, &c.Con, &c.Cha,
		&c.StatPoints, &c.SkillPoints, &c.Karma, &c.Sitting, &c.Hidden,
		&c.GuildRank, &c.GuildRankString, &c.BankMax, &c.GoldBank, &c.Usage,
	); err != nil {
		if err == sql.ErrNoRows {
			return nil, nil
		}
		return nil, fmt.Errorf("querying character: %w", err)
	}

	return &c, nil
}

// TotalCharacterCount returns the total number of characters across all accounts.
func (db *DB) TotalCharacterCount(ctx context.Context) (int, error) {
	var count int
	err := db.QueryRowContext(ctx, `SELECT COUNT(*) FROM characters`).Scan(&count)
	if err != nil {
		return 0, fmt.Errorf("counting total characters: %w", err)
	}
	return count, nil
}

// SetCharacterAdmin sets the admin level on a character.
func (db *DB) SetCharacterAdmin(ctx context.Context, characterID int64, admin int) error {
	_, err := db.ExecContext(ctx,
		`UPDATE characters SET admin = ? WHERE id = ?`,
		admin, characterID,
	)
	if err != nil {
		return fmt.Errorf("setting admin level: %w", err)
	}
	return nil
}

// CreateCharacter inserts a new character with spawn position from config. Returns the character ID.
func (db *DB) CreateCharacter(ctx context.Context, accountID int64, name string, gender, hairStyle, hairColor, race, mapID, mapX, mapY int) (int64, error) {
	result, err := db.ExecContext(ctx,
		`INSERT INTO characters (account_id, name, gender, hair_style, hair_color, race, map_id, map_x, map_y, level, hp, max_hp, tp, max_tp, karma)
		 VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, 1, 10, 10, 10, 10, 1000)`,
		accountID, name, gender, hairStyle, hairColor, race, mapID, mapX, mapY,
	)
	if err != nil {
		return 0, fmt.Errorf("inserting character: %w", err)
	}

	return result.LastInsertId()
}

// CharacterExists checks whether a character with the given name exists.
func (db *DB) CharacterExists(ctx context.Context, name string) (bool, error) {
	var count int
	err := db.QueryRowContext(ctx,
		`SELECT COUNT(*) FROM characters WHERE name = ?`,
		name,
	).Scan(&count)
	if err != nil {
		return false, fmt.Errorf("checking character exists: %w", err)
	}
	return count > 0, nil
}

// CharacterCountForAccount returns the number of characters on an account.
func (db *DB) CharacterCountForAccount(ctx context.Context, accountID int64) (int, error) {
	var count int
	err := db.QueryRowContext(ctx,
		`SELECT COUNT(*) FROM characters WHERE account_id = ?`,
		accountID,
	).Scan(&count)
	if err != nil {
		return 0, fmt.Errorf("counting characters: %w", err)
	}
	return count, nil
}

// DeleteCharacter removes a character by ID, verifying it belongs to the given account.
func (db *DB) DeleteCharacter(ctx context.Context, characterID int64, accountID int64) error {
	result, err := db.ExecContext(ctx,
		`DELETE FROM characters WHERE id = ? AND account_id = ?`,
		characterID, accountID,
	)
	if err != nil {
		return fmt.Errorf("deleting character: %w", err)
	}
	affected, _ := result.RowsAffected()
	if affected == 0 {
		return fmt.Errorf("character %d not found for account %d", characterID, accountID)
	}
	return nil
}

// SaveCharacter persists updated character data to the database.
func (db *DB) SaveCharacter(ctx context.Context, c *session.Character) error {
	_, err := db.ExecContext(ctx,
		`UPDATE characters SET
			map_id = ?, map_x = ?, map_y = ?, direction = ?,
			level = ?, experience = ?, hp = ?, max_hp = ?, tp = ?, max_tp = ?,
			str = ?, intl = ?, wis = ?, agi = ?, con = ?, cha = ?,
			stat_points = ?, skill_points = ?, karma = ?,
			sitting = ?, hidden = ?, usage = ?,
			updated_at = datetime('now')
		 WHERE id = ?`,
		c.MapID, c.MapX, c.MapY, c.Direction,
		c.Level, c.Experience, c.HP, c.MaxHP, c.TP, c.MaxTP,
		c.Str, c.Intl, c.Wis, c.Agi, c.Con, c.Cha,
		c.StatPoints, c.SkillPoints, c.Karma,
		c.Sitting, c.Hidden, c.Usage,
		c.ID,
	)
	if err != nil {
		return fmt.Errorf("saving character %d: %w", c.ID, err)
	}
	return nil
}
