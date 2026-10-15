package handler

import (
	"bufio"
	"context"
	"fmt"
	"os"
	"strings"

	"github.com/acorn-server/acorn-go/internal/config"
	"github.com/acorn-server/acorn-go/internal/database"
	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/acorn-server/acorn-go/internal/world"
	"github.com/ethanmoffat/eolib-go/v3/protocol"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/client"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/server"
)

// WelcomeRequest handles PACKET_WELCOME + PACKET_REQUEST (character selection, sub-id 1).
//
// eoserv: reads character ID (int), loads character, sends massive reply with
// pub file RIDs, character stats, equipment, and server settings.
type WelcomeRequest struct {
	DB    *database.DB
	World *world.World
	Cfg   *config.Config
}

func (h *WelcomeRequest) Handle(s *session.Session, pkt eonet.Packet) error {
	reqPkt, ok := pkt.(*client.WelcomeRequestClientPacket)
	if !ok {
		return fmt.Errorf("expected WelcomeRequestClientPacket, got %T", pkt)
	}

	if s.Account == nil {
		s.Logger().Warn("welcome request without login")
		return nil
	}

	// Load the character from the database.
	charRow, err := h.DB.GetCharacterByID(context.Background(), int64(reqPkt.CharacterId))
	if err != nil {
		return fmt.Errorf("loading character: %w", err)
	}
	if charRow == nil || charRow.AccountID != s.Account.ID {
		s.Logger().Warn("welcome request: character not found or not owned",
			"character_id", reqPkt.CharacterId)
		return nil
	}

	// Set the character on the session.
	s.Character = charRow.ToSessionCharacter()

	// Fix invalid spawn position from old DB records.
	if s.Character.MapID == 0 || h.World.GetMap(s.Character.MapID) == nil {
		s.Character.MapID = h.Cfg.Server.RescueMap
		s.Character.MapX = h.Cfg.Server.RescueX
		s.Character.MapY = h.Cfg.Server.RescueY
	}
	if s.Character.MapX == 0 && s.Character.MapY == 0 {
		s.Character.MapX = h.Cfg.Server.RescueX
		s.Character.MapY = h.Cfg.Server.RescueY
	}

	s.SetState(session.StateEnteringGame)

	// Get map data.
	mapState := h.World.GetMap(s.Character.MapID)
	var mapRid []int
	var mapFileSize int
	if mapState != nil && mapState.MapFile != nil {
		emf := mapState.MapFile.EMF
		mapRid = emf.Rid
		mapFileSize = len(mapState.MapFile.RawBytes)
	}
	// Pad RID to 2 elements if needed.
	for len(mapRid) < 2 {
		mapRid = append(mapRid, 0)
	}

	// Get pub file RIDs and lengths.
	pf := h.World.PubFiles
	eifRid := padRid(pf.EIF.Rid)
	enfRid := padRid(pf.ENF.Rid)
	esfRid := padRid(pf.ESF.Rid)
	ecfRid := padRid(pf.ECF.Rid)

	ch := s.Character

	// Determine login message (eoserv: usage == 0 → first time).
	loginMsg := server.LoginMessageCode_No
	if ch.Usage == 0 {
		loginMsg = server.LoginMessageCode_Yes
	}

	reply := &server.WelcomeReplyServerPacket{
		WelcomeCode: server.WelcomeCode_SelectCharacter,
		WelcomeCodeData: &server.WelcomeReplyWelcomeCodeDataSelectCharacter{
			SessionId:   s.ID(),
			CharacterId: int(ch.ID),
			MapId:       ch.MapID,
			MapRid:      mapRid,
			MapFileSize: mapFileSize,
			EifRid:      eifRid,
			EifLength:   pf.EIF.TotalItemsCount,
			EnfRid:      enfRid,
			EnfLength:   pf.ENF.TotalNpcsCount,
			EsfRid:      esfRid,
			EsfLength:   pf.ESF.TotalSkillsCount,
			EcfRid:      ecfRid,
			EcfLength:   pf.ECF.TotalClassesCount,
			Name:        ch.Name,
			Title:       ch.Title,
			GuildName:   "",
			GuildRankName: ch.GuildRankString,
			ClassId:     ch.ClassID,
			GuildTag:    "   ", // 3-char padded guild tag.
			Admin:       protocol.AdminLevel(ch.Admin),
			Level:       ch.Level,
			Experience:  ch.Experience,
			Usage:       ch.Usage,
			Stats: server.CharacterStatsWelcome{
				Hp:          ch.HP,
				MaxHp:       ch.MaxHP,
				Tp:          ch.TP,
				MaxTp:       ch.MaxTP,
				MaxSp:       0,
				StatPoints:  ch.StatPoints,
				SkillPoints: ch.SkillPoints,
				Karma:       ch.Karma,
				Secondary: server.CharacterSecondaryStats{
					MinDamage: 0,
					MaxDamage: 0,
					Accuracy:  0,
					Evade:     0,
					Armor:     0,
				},
				Base: server.CharacterBaseStatsWelcome{
					Str:  ch.Str,
					Wis:  ch.Wis,
					Intl: ch.Intl,
					Agi:  ch.Agi,
					Con:  ch.Con,
					Cha:  ch.Cha,
				},
			},
			Equipment: server.EquipmentWelcome{
				// TODO: load from character_equipment table.
				Ring:   []int{0, 0},
				Armlet: []int{0, 0},
				Bracer: []int{0, 0},
			},
			GuildRank: ch.GuildRank,
			Settings: server.ServerSettings{
				JailMap:   h.Cfg.Server.JailMap,
				RescueMap: h.Cfg.Server.RescueMap,
				RescueCoords: protocol.Coords{
					X: h.Cfg.Server.RescueX,
					Y: h.Cfg.Server.RescueY,
				},
			},
			LoginMessageCode: loginMsg,
		},
	}

	s.Logger().Info("character selected",
		"name", ch.Name,
		"map", ch.MapID,
		"level", ch.Level,
	)

	return s.Send(reply)
}

// WelcomeMsg handles PACKET_WELCOME + PACKET_MSG (enter game, sub-id 2).
//
// eoserv: reads unknown(three) + character_id(int), logs player into world,
// sends news, inventory, spells, and nearby entities.
type WelcomeMsg struct {
	DB    *database.DB
	World *world.World
	Cfg   *config.Config
}

func (h *WelcomeMsg) Handle(s *session.Session, pkt eonet.Packet) error {
	_, ok := pkt.(*client.WelcomeMsgClientPacket)
	if !ok {
		return fmt.Errorf("expected WelcomeMsgClientPacket, got %T", pkt)
	}

	if s.Character == nil {
		s.Logger().Warn("welcome msg without character selection")
		return nil
	}

	// Add player to the map.
	mapState := h.World.GetMap(s.Character.MapID)
	if mapState == nil || s.Character.MapID == 0 {
		// Map doesn't exist or invalid — warp to rescue/spawn.
		s.Character.MapID = h.Cfg.Server.RescueMap
		s.Character.MapX = h.Cfg.Server.RescueX
		s.Character.MapY = h.Cfg.Server.RescueY
		mapState = h.World.GetMap(s.Character.MapID)
		if mapState == nil {
			return fmt.Errorf("rescue map %d does not exist", s.Character.MapID)
		}
	}
	// Ensure valid coordinates (e.g. old DB records with 0,0).
	if s.Character.MapX == 0 && s.Character.MapY == 0 {
		s.Character.MapX = h.Cfg.Server.RescueX
		s.Character.MapY = h.Cfg.Server.RescueY
	}

	mapState.Enter(s)
	s.SetState(session.StateInGame)

	// Load news (eoserv: up to 9 lines from NewsFile).
	news := loadNews(h.Cfg.Server.NewsFile, 9)

	// Build nearby entity info.
	nearby := mapState.GetNearbyInfo(s.ID())

	// TODO: load inventory and spells from database.
	var items []eonet.Item
	var spells []eonet.Spell

	reply := &server.WelcomeReplyServerPacket{
		WelcomeCode: server.WelcomeCode_EnterGame,
		WelcomeCodeData: &server.WelcomeReplyWelcomeCodeDataEnterGame{
			News: news,
			Weight: eonet.Weight{
				Current: 0,
				Max:     70,
			},
			Items:  items,
			Spells: spells,
			Nearby: nearby,
		},
	}

	// Increment usage counter (eoserv: tracks login count).
	s.Character.Usage++

	s.Logger().Info("entered game",
		"name", s.Character.Name,
		"map", s.Character.MapID,
		"x", s.Character.MapX,
		"y", s.Character.MapY,
	)

	return s.Send(reply)
}

// WelcomeAgree handles PACKET_WELCOME + PACKET_AGREE (file requests).
//
// eoserv: client requests pub/map files when RID/length doesn't match local copies.
// Server responds with file data via INIT_INIT packet with appropriate InitReply code.
type WelcomeAgree struct {
	World *world.World
}

func (h *WelcomeAgree) Handle(s *session.Session, pkt eonet.Packet) error {
	agreePkt, ok := pkt.(*client.WelcomeAgreeClientPacket)
	if !ok {
		return fmt.Errorf("expected WelcomeAgreeClientPacket, got %T", pkt)
	}

	switch agreePkt.FileType {
	case client.File_Emf:
		// Send the map file for the player's current map.
		if s.Character == nil {
			return nil
		}
		mapState := h.World.GetMap(s.Character.MapID)
		if mapState == nil || mapState.MapFile == nil {
			return nil
		}
		return s.Send(&server.InitInitServerPacket{
			ReplyCode: server.InitReply_FileEmf,
			ReplyCodeData: &server.InitInitReplyCodeDataFileEmf{
				MapFile: server.MapFile{
					Content: mapState.MapFile.RawBytes,
				},
			},
		})

	case client.File_Eif:
		return s.Send(&server.InitInitServerPacket{
			ReplyCode: server.InitReply_FileEif,
			ReplyCodeData: &server.InitInitReplyCodeDataFileEif{
				PubFile: server.PubFile{
					FileId:  1,
					Content: h.World.PubFiles.EIFBytes,
				},
			},
		})

	case client.File_Enf:
		return s.Send(&server.InitInitServerPacket{
			ReplyCode: server.InitReply_FileEnf,
			ReplyCodeData: &server.InitInitReplyCodeDataFileEnf{
				PubFile: server.PubFile{
					FileId:  1,
					Content: h.World.PubFiles.ENFBytes,
				},
			},
		})

	case client.File_Esf:
		return s.Send(&server.InitInitServerPacket{
			ReplyCode: server.InitReply_FileEsf,
			ReplyCodeData: &server.InitInitReplyCodeDataFileEsf{
				PubFile: server.PubFile{
					FileId:  1,
					Content: h.World.PubFiles.ESFBytes,
				},
			},
		})

	case client.File_Ecf:
		return s.Send(&server.InitInitServerPacket{
			ReplyCode: server.InitReply_FileEcf,
			ReplyCodeData: &server.InitInitReplyCodeDataFileEcf{
				PubFile: server.PubFile{
					FileId:  1,
					Content: h.World.PubFiles.ECFBytes,
				},
			},
		})
	}

	return nil
}

// loadNews reads up to maxLines from the news file.
// Returns a slice padded to exactly maxLines (empty strings for missing lines).
func loadNews(path string, maxLines int) []string {
	news := make([]string, maxLines)

	f, err := os.Open(path)
	if err != nil {
		return news
	}
	defer f.Close()

	scanner := bufio.NewScanner(f)
	for i := 0; i < maxLines && scanner.Scan(); i++ {
		line := strings.TrimSpace(scanner.Text())
		if line == "" {
			line = " " // eoserv: pad to min 2 chars, but at least non-empty.
		}
		news[i] = line
	}

	return news
}

// padRid ensures an RID slice has exactly 2 elements.
func padRid(rid []int) []int {
	result := make([]int, 2)
	copy(result, rid)
	return result
}
