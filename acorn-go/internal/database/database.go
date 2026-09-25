package database

import (
	"context"
	"database/sql"
	"fmt"
	"log/slog"

	_ "modernc.org/sqlite"
)

// DB wraps a sql.DB connection with the schema initialization logic.
type DB struct {
	*sql.DB
	logger *slog.Logger
}

// Open creates a new SQLite database connection and ensures the schema exists.
func Open(dsn string, logger *slog.Logger) (*DB, error) {
	sqlDB, err := sql.Open("sqlite", dsn)
	if err != nil {
		return nil, fmt.Errorf("opening database: %w", err)
	}

	// SQLite performance tuning for a game server.
	if _, err := sqlDB.Exec(`
		PRAGMA journal_mode = WAL;
		PRAGMA synchronous = NORMAL;
		PRAGMA busy_timeout = 5000;
		PRAGMA foreign_keys = ON;
	`); err != nil {
		_ = sqlDB.Close()
		return nil, fmt.Errorf("setting pragmas: %w", err)
	}

	db := &DB{DB: sqlDB, logger: logger}

	if err := db.migrate(context.Background()); err != nil {
		_ = sqlDB.Close()
		return nil, fmt.Errorf("running migrations: %w", err)
	}

	return db, nil
}

// migrate creates the initial database schema if it doesn't exist.
func (db *DB) migrate(ctx context.Context) error {
	schema := `
	CREATE TABLE IF NOT EXISTS accounts (
		id         INTEGER PRIMARY KEY AUTOINCREMENT,
		username   TEXT    NOT NULL UNIQUE COLLATE NOCASE,
		password   TEXT    NOT NULL,
		salt       BLOB   NOT NULL,
		email      TEXT    NOT NULL DEFAULT '',
		created_at TEXT    NOT NULL DEFAULT (datetime('now')),
		updated_at TEXT    NOT NULL DEFAULT (datetime('now'))
	);

	CREATE TABLE IF NOT EXISTS characters (
		id         INTEGER PRIMARY KEY AUTOINCREMENT,
		account_id INTEGER NOT NULL REFERENCES accounts(id) ON DELETE CASCADE,
		name       TEXT    NOT NULL UNIQUE COLLATE NOCASE,
		title      TEXT    NOT NULL DEFAULT '',
		home       TEXT    NOT NULL DEFAULT '',
		partner    TEXT    NOT NULL DEFAULT '',
		admin      INTEGER NOT NULL DEFAULT 0,
		class_id   INTEGER NOT NULL DEFAULT 0,
		gender     INTEGER NOT NULL DEFAULT 0,
		race       INTEGER NOT NULL DEFAULT 0,
		hair_style INTEGER NOT NULL DEFAULT 0,
		hair_color INTEGER NOT NULL DEFAULT 0,
		map_id     INTEGER NOT NULL DEFAULT 192,
		map_x      INTEGER NOT NULL DEFAULT 6,
		map_y      INTEGER NOT NULL DEFAULT 6,
		direction  INTEGER NOT NULL DEFAULT 2,
		level      INTEGER NOT NULL DEFAULT 0,
		experience INTEGER NOT NULL DEFAULT 0,
		hp         INTEGER NOT NULL DEFAULT 10,
		max_hp     INTEGER NOT NULL DEFAULT 10,
		tp         INTEGER NOT NULL DEFAULT 10,
		max_tp     INTEGER NOT NULL DEFAULT 10,
		str        INTEGER NOT NULL DEFAULT 0,
		intl       INTEGER NOT NULL DEFAULT 0,
		wis        INTEGER NOT NULL DEFAULT 0,
		agi        INTEGER NOT NULL DEFAULT 0,
		con        INTEGER NOT NULL DEFAULT 0,
		cha        INTEGER NOT NULL DEFAULT 0,
		stat_points INTEGER NOT NULL DEFAULT 0,
		skill_points INTEGER NOT NULL DEFAULT 0,
		karma      INTEGER NOT NULL DEFAULT 1000,
		sitting    INTEGER NOT NULL DEFAULT 0,
		hidden     INTEGER NOT NULL DEFAULT 0,
		guild_rank INTEGER NOT NULL DEFAULT 0,
		guild_rank_string TEXT NOT NULL DEFAULT '',
		bank_max   INTEGER NOT NULL DEFAULT 0,
		gold_bank  INTEGER NOT NULL DEFAULT 0,
		usage      INTEGER NOT NULL DEFAULT 0,
		created_at TEXT    NOT NULL DEFAULT (datetime('now')),
		updated_at TEXT    NOT NULL DEFAULT (datetime('now'))
	);

	CREATE TABLE IF NOT EXISTS character_inventory (
		character_id INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
		item_id      INTEGER NOT NULL,
		amount       INTEGER NOT NULL DEFAULT 1,
		PRIMARY KEY (character_id, item_id)
	);

	CREATE TABLE IF NOT EXISTS character_spells (
		character_id INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
		spell_id     INTEGER NOT NULL,
		level        INTEGER NOT NULL DEFAULT 0,
		PRIMARY KEY (character_id, spell_id)
	);

	CREATE TABLE IF NOT EXISTS character_equipment (
		character_id INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
		slot         INTEGER NOT NULL,
		item_id      INTEGER NOT NULL DEFAULT 0,
		PRIMARY KEY (character_id, slot)
	);

	CREATE INDEX IF NOT EXISTS idx_characters_account_id ON characters(account_id);
	CREATE INDEX IF NOT EXISTS idx_characters_name ON characters(name);
	`

	if _, err := db.ExecContext(ctx, schema); err != nil {
		return fmt.Errorf("creating schema: %w", err)
	}

	db.logger.Info("database schema verified")
	return nil
}
