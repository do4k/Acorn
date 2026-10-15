package handler

import (
	"regexp"
	"strings"

	"github.com/acorn-server/acorn-go/internal/database"
	"github.com/ethanmoffat/eolib-go/v3/protocol"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/server"
)

// MaxCharacterNameLen matches eoserv: character names are 4-12 lowercase a-z only.
const (
	MinCharacterNameLen = 4
	MaxCharacterNameLen = 12

	// ChatMaxLength is the maximum length of a chat message (matches eoserv ChatLength).
	ChatMaxLength = 128
)

// Admin levels matching eoserv (ADMIN_PLAYER..ADMIN_HGM).
const (
	AdminLevelPlayer   = 0 // Normal player
	AdminLevelGuide    = 1 // Guide (eoserv: ADMIN_GUIDE)
	AdminLevelGuardian = 2 // Guardian (eoserv: ADMIN_GUARDIAN)
	AdminLevelGM       = 3 // Game Master (eoserv: ADMIN_GM)
	AdminLevelHGM      = 4 // Highest Game Master (eoserv: ADMIN_HGM)
)

var (
	// validAccountName matches eoserv Player::ValidName: a-z, space, 0-9.
	validAccountName = regexp.MustCompile(`^[a-z0-9 ]+$`)
	// validCharacterName matches eoserv Character::ValidName: a-z only.
	validCharacterName = regexp.MustCompile(`^[a-z]+$`)
)

// ValidateAccountName checks if a username is valid per eoserv rules.
func ValidateAccountName(name string) bool {
	return validAccountName.MatchString(strings.ToLower(name))
}

// ValidateCharacterName checks if a character name is valid per eoserv rules.
// Must be 4-12 lowercase letters, not "server".
func ValidateCharacterName(name string) bool {
	lower := strings.ToLower(name)
	if len(lower) < MinCharacterNameLen || len(lower) > MaxCharacterNameLen {
		return false
	}
	if lower == "server" {
		return false
	}
	return validCharacterName.MatchString(lower)
}

// buildCharacterList converts database rows to the protocol CharacterSelectionListEntry format.
// This matches the format eoserv uses in login, character create, and character delete responses.
func buildCharacterList(chars []database.CharacterRow) []server.CharacterSelectionListEntry {
	entries := make([]server.CharacterSelectionListEntry, 0, len(chars))
	for _, c := range chars {
		entries = append(entries, server.CharacterSelectionListEntry{
			Name:      c.Name,
			Id:        int(c.ID),
			Level:     c.Level,
			Gender:    protocol.Gender(c.Gender),
			HairStyle: c.HairStyle,
			HairColor: c.HairColor,
			Skin:      c.Race,
			Admin:     protocol.AdminLevel(c.Admin),
			Equipment: server.EquipmentCharacterSelect{
				// TODO: load actual paperdoll BAHSW (dollgraphic) from equipment table.
			},
		})
	}
	return entries
}
