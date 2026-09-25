package world

import (
	"github.com/ethanmoffat/eolib-go/v3/protocol"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/server"
)

// Helper functions for converting between internal int values and eolib-go protocol types.

func protocolDirection(d int) protocol.Direction {
	return protocol.Direction(d)
}

func protocolGender(g int) protocol.Gender {
	return protocol.Gender(g)
}

func protocolCoords(x, y int) protocol.Coords {
	return protocol.Coords{X: x, Y: y}
}

func protocolAdminLevel(a int) protocol.AdminLevel {
	return protocol.AdminLevel(a)
}

// CharacterToSelectionEntry converts a session Character to the list entry sent during login/character list.
func CharacterToSelectionEntry(c CharSelectInfo) server.CharacterSelectionListEntry {
	return server.CharacterSelectionListEntry{
		Name:      c.Name,
		Id:        int(c.ID),
		Level:     c.Level,
		Gender:    protocolGender(c.Gender),
		HairStyle: c.HairStyle,
		HairColor: c.HairColor,
		Skin:      c.Race,
		Admin:     protocolAdminLevel(c.Admin),
		Equipment: server.EquipmentCharacterSelect{},
	}
}

// CharSelectInfo holds the minimal info needed for character selection list entries.
type CharSelectInfo struct {
	ID        int64
	Name      string
	Level     int
	Gender    int
	HairStyle int
	HairColor int
	Race      int
	Admin     int
}
