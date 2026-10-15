package handler

import (
	"fmt"

	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/acorn-server/acorn-go/internal/world"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/client"
)

// FacePlayer handles Face_Player packets — the client changed facing direction.
type FacePlayer struct {
	World *world.World
}

func (h *FacePlayer) Handle(s *session.Session, pkt eonet.Packet) error {
	facePkt, ok := pkt.(*client.FacePlayerClientPacket)
	if !ok {
		return fmt.Errorf("expected FacePlayerClientPacket, got %T", pkt)
	}

	if s.State() != session.StateInGame || s.Character == nil {
		return nil
	}

	ms := h.World.GetMap(s.Character.MapID)
	if ms == nil {
		return nil
	}

	ms.Face(s, int(facePkt.Direction))
	return nil
}
