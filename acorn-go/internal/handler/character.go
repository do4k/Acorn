package handler

import (
	"context"
	"fmt"
	"strings"

	"github.com/acorn-server/acorn-go/internal/config"
	"github.com/acorn-server/acorn-go/internal/database"
	"github.com/acorn-server/acorn-go/internal/session"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/client"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/server"
)

// CharacterRequest handles PACKET_CHARACTER + PACKET_REQUEST.
// First step of character creation — eoserv just returns a CreateID (1000) + "OK".
// In eolib-go, the response uses reply_code > 9 (the CreateID) hitting the Default case.
type CharacterRequest struct{}

func (h *CharacterRequest) Handle(s *session.Session, pkt eonet.Packet) error {
	if s.Account == nil {
		s.Logger().Warn("character request without login")
		return nil
	}

	// eoserv sends: AddShort(1000) + AddString("OK")
	// reply_code = 1000 (> 9) → Default case in eolib-go.
	return s.Send(&server.CharacterReplyServerPacket{
		ReplyCode:     server.CharacterReply(1000),
		ReplyCodeData: &server.CharacterReplyReplyCodeDataDefault{},
	})
}

// CharacterCreate handles PACKET_CHARACTER + PACKET_CREATE.
// Second step of character creation.
//
// eoserv read order: createID(short), gender(short), hairstyle(short),
// haircolor(short), race(short), unknown(byte), name(break string).
type CharacterCreate struct {
	DB  *database.DB
	Cfg *config.Config
}

func (h *CharacterCreate) Handle(s *session.Session, pkt eonet.Packet) error {
	createPkt, ok := pkt.(*client.CharacterCreateClientPacket)
	if !ok {
		return fmt.Errorf("expected CharacterCreateClientPacket, got %T", pkt)
	}

	if s.Account == nil {
		s.Logger().Warn("character create without login")
		return nil
	}

	name := strings.ToLower(createPkt.Name)
	gender := int(createPkt.Gender)
	hairStyle := createPkt.HairStyle
	hairColor := createPkt.HairColor
	race := createPkt.Skin

	// Check character count limit (eoserv: MaxCharacters config).
	count, err := h.DB.CharacterCountForAccount(context.Background(), s.Account.ID)
	if err != nil {
		return fmt.Errorf("counting characters: %w", err)
	}
	if count >= h.Cfg.Server.MaxCharacters {
		return s.Send(&server.CharacterReplyServerPacket{
			ReplyCode:     server.CharacterReply_Full,
			ReplyCodeData: &server.CharacterReplyReplyCodeDataFull{},
		})
	}

	// Validate character name (eoserv: Character::ValidName).
	if !ValidateCharacterName(name) {
		return s.Send(&server.CharacterReplyServerPacket{
			ReplyCode:     server.CharacterReply_NotApproved,
			ReplyCodeData: &server.CharacterReplyReplyCodeDataNotApproved{},
		})
	}

	// Check if name is taken.
	exists, err := h.DB.CharacterExists(context.Background(), name)
	if err != nil {
		return fmt.Errorf("checking character exists: %w", err)
	}
	if exists {
		return s.Send(&server.CharacterReplyServerPacket{
			ReplyCode:     server.CharacterReply_Exists,
			ReplyCodeData: &server.CharacterReplyReplyCodeDataExists{},
		})
	}

	// Create the character at the configured spawn location.
	charID, err := h.DB.CreateCharacter(context.Background(), s.Account.ID, name, gender, hairStyle, hairColor, race,
		h.Cfg.Server.RescueMap, h.Cfg.Server.RescueX, h.Cfg.Server.RescueY)
	if err != nil {
		return fmt.Errorf("creating character: %w", err)
	}

	// First character on the server gets HGM admin (matching eoserv FirstCharacterAdmin).
	totalChars, _ := h.DB.TotalCharacterCount(context.Background())
	if totalChars == 1 {
		_ = h.DB.SetCharacterAdmin(context.Background(), charID, AdminLevelHGM)
		s.Logger().Info("first character promoted to HGM", "name", name)
	}

	s.Logger().Info("character created", "name", name)

	// Re-load character list and send success.
	chars, err := h.DB.GetCharactersByAccount(context.Background(), s.Account.ID)
	if err != nil {
		return fmt.Errorf("loading characters: %w", err)
	}

	return s.Send(&server.CharacterReplyServerPacket{
		ReplyCode: server.CharacterReply_Ok,
		ReplyCodeData: &server.CharacterReplyReplyCodeDataOk{
			Characters: buildCharacterList(chars),
		},
	})
}

// CharacterTake handles PACKET_CHARACTER + PACKET_TAKE.
// First step of character deletion — eoserv responds with a delete req ID + character ID.
// This uses CharacterPlayerServerPacket (family=Character, action=Player).
type CharacterTake struct {
	DB *database.DB
}

func (h *CharacterTake) Handle(s *session.Session, pkt eonet.Packet) error {
	takePkt, ok := pkt.(*client.CharacterTakeClientPacket)
	if !ok {
		return fmt.Errorf("expected CharacterTakeClientPacket, got %T", pkt)
	}

	if s.Account == nil {
		s.Logger().Warn("character take without login")
		return nil
	}

	// Verify the character belongs to this account.
	char, err := h.DB.GetCharacterByID(context.Background(), int64(takePkt.CharacterId))
	if err != nil {
		return fmt.Errorf("looking up character: %w", err)
	}
	if char == nil || char.AccountID != s.Account.ID {
		s.Logger().Warn("character take: not found or not owned",
			"character_id", takePkt.CharacterId)
		return nil
	}

	// eoserv sends: AddShort(1000) + AddInt(character_id)
	return s.Send(&server.CharacterPlayerServerPacket{
		SessionId:   1000,
		CharacterId: takePkt.CharacterId,
	})
}

// CharacterRemove handles PACKET_CHARACTER + PACKET_REMOVE.
// Second step of character deletion — confirms and performs the delete.
//
// eoserv read order: deleteID(short, discarded), characterID(int).
type CharacterRemove struct {
	DB *database.DB
}

func (h *CharacterRemove) Handle(s *session.Session, pkt eonet.Packet) error {
	removePkt, ok := pkt.(*client.CharacterRemoveClientPacket)
	if !ok {
		return fmt.Errorf("expected CharacterRemoveClientPacket, got %T", pkt)
	}

	if s.Account == nil {
		s.Logger().Warn("character remove without login")
		return nil
	}

	// Delete the character (verifies account ownership).
	err := h.DB.DeleteCharacter(context.Background(), int64(removePkt.CharacterId), s.Account.ID)
	if err != nil {
		s.Logger().Warn("character delete failed",
			"character_id", removePkt.CharacterId,
			"error", err)
		return nil
	}

	s.Logger().Info("character deleted", "character_id", removePkt.CharacterId)

	// Re-load character list and send success.
	chars, err := h.DB.GetCharactersByAccount(context.Background(), s.Account.ID)
	if err != nil {
		return fmt.Errorf("loading characters: %w", err)
	}

	return s.Send(&server.CharacterReplyServerPacket{
		ReplyCode: server.CharacterReply_Deleted,
		ReplyCodeData: &server.CharacterReplyReplyCodeDataDeleted{
			Characters: buildCharacterList(chars),
		},
	})
}
