package handler

import (
	"fmt"

	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/acorn-server/acorn-go/internal/world"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/client"
)

// ---------------------------------------------------------------------------
// Walk_Player — normal walking
// ---------------------------------------------------------------------------

// WalkPlayer handles Walk_Player packets — the client is walking normally.
type WalkPlayer struct {
	World *world.World
}

func (h *WalkPlayer) Handle(s *session.Session, pkt eonet.Packet) error {
	walkPkt, ok := pkt.(*client.WalkPlayerClientPacket)
	if !ok {
		return fmt.Errorf("expected WalkPlayerClientPacket, got %T", pkt)
	}

	if s.State() != session.StateInGame || s.Character == nil {
		return nil
	}

	ms := h.World.GetMap(s.Character.MapID)
	if ms == nil {
		return nil
	}

	result := ms.Walk(s, int(walkPkt.WalkAction.Direction), false)

	// If walk failed or position desync, send a refresh to resync the client.
	ch := s.Character
	if result == world.WalkFail ||
		ch.MapX != walkPkt.WalkAction.Coords.X ||
		ch.MapY != walkPkt.WalkAction.Coords.Y {
		ms.Refresh(s)
	}

	return nil
}

// ---------------------------------------------------------------------------
// Walk_Spec — walking through ghost players
// ---------------------------------------------------------------------------

// WalkSpec handles Walk_Spec packets — walking through players (ghost timer).
// Treated identically to Walk_Player in our implementation.
type WalkSpec struct {
	World *world.World
}

func (h *WalkSpec) Handle(s *session.Session, pkt eonet.Packet) error {
	walkPkt, ok := pkt.(*client.WalkSpecClientPacket)
	if !ok {
		return fmt.Errorf("expected WalkSpecClientPacket, got %T", pkt)
	}

	if s.State() != session.StateInGame || s.Character == nil {
		return nil
	}

	ms := h.World.GetMap(s.Character.MapID)
	if ms == nil {
		return nil
	}

	result := ms.Walk(s, int(walkPkt.WalkAction.Direction), false)

	ch := s.Character
	if result == world.WalkFail ||
		ch.MapX != walkPkt.WalkAction.Coords.X ||
		ch.MapY != walkPkt.WalkAction.Coords.Y {
		ms.Refresh(s)
	}

	return nil
}

// ---------------------------------------------------------------------------
// Walk_Admin — #nowall admin walk (bypasses wall checks)
// ---------------------------------------------------------------------------

// WalkAdmin handles Walk_Admin packets — admin walking through walls.
type WalkAdmin struct {
	World *world.World
}

func (h *WalkAdmin) Handle(s *session.Session, pkt eonet.Packet) error {
	walkPkt, ok := pkt.(*client.WalkAdminClientPacket)
	if !ok {
		return fmt.Errorf("expected WalkAdminClientPacket, got %T", pkt)
	}

	if s.State() != session.StateInGame || s.Character == nil {
		return nil
	}

	// Admin walk requires at least GM level.
	if s.Character.Admin < AdminLevelGM {
		s.Logger().Warn("non-admin attempted Walk_Admin", "admin_level", s.Character.Admin)
		return nil
	}

	ms := h.World.GetMap(s.Character.MapID)
	if ms == nil {
		return nil
	}

	result := ms.Walk(s, int(walkPkt.WalkAction.Direction), true)

	ch := s.Character
	if result == world.WalkFail ||
		ch.MapX != walkPkt.WalkAction.Coords.X ||
		ch.MapY != walkPkt.WalkAction.Coords.Y {
		ms.Refresh(s)
	}

	return nil
}
