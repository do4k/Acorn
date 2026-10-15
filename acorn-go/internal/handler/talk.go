package handler

import (
	"fmt"
	"strconv"
	"strings"

	"github.com/acorn-server/acorn-go/internal/config"
	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/acorn-server/acorn-go/internal/world"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/client"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/server"
)

// limitMessage truncates a chat message to the max length, matching eoserv behavior.
func limitMessage(msg string) string {
	if len(msg) > ChatMaxLength {
		return msg[:ChatMaxLength-6] + " [...]"
	}
	return msg
}

// ---------------------------------------------------------------------------
// Talk_Report — public / map chat
// ---------------------------------------------------------------------------

// TalkReport handles Talk_Report packets — public chat on the current map.
// If the message starts with "$" and the player is an admin, it is a server command.
type TalkReport struct {
	World *world.World
	Cfg   *config.Config
}

func (h *TalkReport) Handle(s *session.Session, pkt eonet.Packet) error {
	talkPkt, ok := pkt.(*client.TalkReportClientPacket)
	if !ok {
		return fmt.Errorf("expected TalkReportClientPacket, got %T", pkt)
	}

	if s.State() != session.StateInGame || s.Character == nil {
		return nil
	}

	message := talkPkt.Message
	if message == "" {
		return nil
	}

	// TODO: Check mute status.

	// Admin commands start with "$".
	if strings.HasPrefix(message, "$") && s.Character.Admin > AdminLevelPlayer {
		s.Logger().Info("admin command", "command", message)
		h.dispatchCommand(s, message)
		return nil
	}

	message = limitMessage(message)

	ms := h.World.GetMap(s.Character.MapID)
	if ms == nil {
		return nil
	}

	ms.Msg(s, message)
	return nil
}

// dispatchCommand parses and executes a "$command arg1 arg2 ..." admin command.
func (h *TalkReport) dispatchCommand(s *session.Session, raw string) {
	parts := strings.Fields(raw)
	if len(parts) == 0 {
		return
	}
	cmd := strings.ToLower(strings.TrimPrefix(parts[0], "$"))
	args := parts[1:]

	switch cmd {
	case "w", "warp":
		h.cmdWarp(s, args)
	default:
		h.World.ServerMsg(s, "Unknown command: "+cmd)
	}
}

// cmdWarp handles "$w map x y" — warp to a map position.
func (h *TalkReport) cmdWarp(s *session.Session, args []string) {
	if len(args) < 3 {
		h.World.ServerMsg(s, "Usage: $w map x y")
		return
	}

	mapID, err := strconv.Atoi(args[0])
	if err != nil || mapID <= 0 {
		h.World.ServerMsg(s, "Invalid map ID")
		return
	}

	x, err := strconv.Atoi(args[1])
	if err != nil || x < 0 {
		h.World.ServerMsg(s, "Invalid X coordinate")
		return
	}

	y, err := strconv.Atoi(args[2])
	if err != nil || y < 0 {
		h.World.ServerMsg(s, "Invalid Y coordinate")
		return
	}

	if !h.World.Warp(s, mapID, x, y, server.WarpEffect_Admin) {
		h.World.ServerMsg(s, fmt.Sprintf("Map %d does not exist", mapID))
	}
}

// ---------------------------------------------------------------------------
// Talk_Tell — private message (whisper)
// ---------------------------------------------------------------------------

// TalkTell handles Talk_Tell packets — private message to another player.
type TalkTell struct {
	Manager *session.Manager
}

func (h *TalkTell) Handle(s *session.Session, pkt eonet.Packet) error {
	tellPkt, ok := pkt.(*client.TalkTellClientPacket)
	if !ok {
		return fmt.Errorf("expected TalkTellClientPacket, got %T", pkt)
	}

	if s.State() != session.StateInGame || s.Character == nil {
		return nil
	}

	targetName := tellPkt.Name
	message := tellPkt.Message
	if targetName == "" || message == "" {
		return nil
	}

	// TODO: Check mute status.

	message = limitMessage(message)

	// Find the target player by character name.
	target := h.Manager.FindByCharacterName(targetName)

	if target == nil || target.Character == nil {
		// Target not found — send TALK_REPLY with NOT_FOUND.
		return s.Send(&server.TalkReplyServerPacket{
			ReplyCode: server.TalkReply_NotFound,
			Name:      targetName,
		})
	}

	// TODO: Check if target has whispers disabled or is hide-online.

	// Send the private message to the target.
	return target.Send(&server.TalkTellServerPacket{
		PlayerName: s.Character.Name,
		Message:    message,
	})
}

// ---------------------------------------------------------------------------
// Talk_Msg — global chat
// ---------------------------------------------------------------------------

// TalkMsg handles Talk_Msg packets — global chat broadcast.
type TalkMsg struct {
	World *world.World
	Cfg   *config.Config
}

func (h *TalkMsg) Handle(s *session.Session, pkt eonet.Packet) error {
	msgPkt, ok := pkt.(*client.TalkMsgClientPacket)
	if !ok {
		return fmt.Errorf("expected TalkMsgClientPacket, got %T", pkt)
	}

	if s.State() != session.StateInGame || s.Character == nil {
		return nil
	}

	message := msgPkt.Message
	if message == "" {
		return nil
	}

	// TODO: Check mute status.

	// Block global chat from the jail map (matching eoserv).
	if s.Character.MapID == h.Cfg.Server.JailMap {
		return nil
	}

	message = limitMessage(message)

	h.World.GlobalMsg(s, message)
	return nil
}

// ---------------------------------------------------------------------------
// Talk_Admin — admin-only chat
// ---------------------------------------------------------------------------

// TalkAdmin handles Talk_Admin packets — chat visible only to admins.
type TalkAdmin struct {
	World *world.World
}

func (h *TalkAdmin) Handle(s *session.Session, pkt eonet.Packet) error {
	adminPkt, ok := pkt.(*client.TalkAdminClientPacket)
	if !ok {
		return fmt.Errorf("expected TalkAdminClientPacket, got %T", pkt)
	}

	if s.State() != session.StateInGame || s.Character == nil {
		return nil
	}

	// Requires at least Guardian level (eoserv: ADMIN_GUARDIAN = 2).
	if s.Character.Admin < AdminLevelGuardian {
		return nil
	}

	message := adminPkt.Message
	if message == "" {
		return nil
	}

	// TODO: Check mute status.

	message = limitMessage(message)

	// Broadcast to all admins at or above Guardian level.
	h.World.AdminMsg(s, message, AdminLevelGuardian)
	return nil
}

// ---------------------------------------------------------------------------
// Talk_Announce — server-wide announcement (admin only)
// ---------------------------------------------------------------------------

// TalkAnnounce handles Talk_Announce packets — announcement visible to all players.
type TalkAnnounce struct {
	World *world.World
}

func (h *TalkAnnounce) Handle(s *session.Session, pkt eonet.Packet) error {
	announcePkt, ok := pkt.(*client.TalkAnnounceClientPacket)
	if !ok {
		return fmt.Errorf("expected TalkAnnounceClientPacket, got %T", pkt)
	}

	if s.State() != session.StateInGame || s.Character == nil {
		return nil
	}

	// Requires at least Guardian level (eoserv: ADMIN_GUARDIAN = 2).
	if s.Character.Admin < AdminLevelGuardian {
		return nil
	}

	message := announcePkt.Message
	if message == "" {
		return nil
	}

	message = limitMessage(message)

	h.World.AnnounceMsg(s, message)
	return nil
}
