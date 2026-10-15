package handler

import (
	"fmt"

	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/acorn-server/acorn-go/internal/world"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/client"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/server"
)

// ---------------------------------------------------------------------------
// Sit_Request — sit on floor / stand from floor
// ---------------------------------------------------------------------------

// SitRequest handles Sit_Request packets.
type SitRequest struct {
	World *world.World
}

func (h *SitRequest) Handle(s *session.Session, pkt eonet.Packet) error {
	sitPkt, ok := pkt.(*client.SitRequestClientPacket)
	if !ok {
		return fmt.Errorf("expected SitRequestClientPacket, got %T", pkt)
	}

	if s.State() != session.StateInGame || s.Character == nil {
		return nil
	}

	ms := h.World.GetMap(s.Character.MapID)
	if ms == nil {
		return nil
	}

	ch := s.Character
	switch sitPkt.SitAction {
	case client.SitAction_Sit:
		if ch.Sitting == int(server.SitState_Stand) {
			ms.SitFloor(s)
		}
	default:
		// Any other action while sitting on floor => stand up.
		if ch.Sitting == int(server.SitState_Floor) {
			ms.Stand(s)
		}
	}

	return nil
}

// ---------------------------------------------------------------------------
// Chair_Request — sit on chair / stand from chair
// ---------------------------------------------------------------------------

// ChairRequest handles Chair_Request packets.
type ChairRequest struct {
	World *world.World
}

func (h *ChairRequest) Handle(s *session.Session, pkt eonet.Packet) error {
	chairPkt, ok := pkt.(*client.ChairRequestClientPacket)
	if !ok {
		return fmt.Errorf("expected ChairRequestClientPacket, got %T", pkt)
	}

	if s.State() != session.StateInGame || s.Character == nil {
		return nil
	}

	ms := h.World.GetMap(s.Character.MapID)
	if ms == nil {
		return nil
	}

	ch := s.Character
	switch chairPkt.SitAction {
	case client.SitAction_Sit:
		if ch.Sitting == int(server.SitState_Stand) {
			if data, ok := chairPkt.SitActionData.(*client.ChairRequestSitActionDataSit); ok {
				ms.SitChair(s, data.Coords.X, data.Coords.Y)
			}
		}
	default:
		// Stand from chair.
		if ch.Sitting == int(server.SitState_Chair) {
			ms.StandFromChair(s)
		}
	}

	return nil
}
