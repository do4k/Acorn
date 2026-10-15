package handler

import (
	"fmt"

	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/acorn-server/acorn-go/internal/world"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/client"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/server"
)

// WarpAccept handles Warp_Accept — client acknowledging a server-initiated warp.
// Server responds with WarpAgreeServerPacket containing nearby entities on the new map.
//
// eoserv flow: client sends WarpAccept after receiving WarpRequest.
// Server validates the pending warp animation, then sends full nearby info.
type WarpAccept struct {
	World *world.World
}

func (h *WarpAccept) Handle(s *session.Session, pkt eonet.Packet) error {
	_, ok := pkt.(*client.WarpAcceptClientPacket)
	if !ok {
		return fmt.Errorf("expected WarpAcceptClientPacket, got %T", pkt)
	}

	if s.Character == nil {
		return nil
	}

	// Check for pending warp (matching eoserv: warp_anim != WARP_ANIMATION_INVALID).
	anim := s.WarpAnimation
	if anim < 0 {
		return nil // No pending warp.
	}

	// Consume the pending warp.
	s.WarpAnimation = -1

	// Get the map the player is now on.
	ms := h.World.GetMap(s.Character.MapID)
	if ms == nil {
		return nil
	}

	// Build nearby info (including self — matching eoserv).
	nearby := ms.GetNearbyInfo(s.ID())

	// Determine warp type for the response.
	// eoserv always sends warp type 2 (map switch) with map ID and animation in WarpAgree.
	reply := &server.WarpAgreeServerPacket{
		WarpType: server.Warp_MapSwitch,
		WarpTypeData: &server.WarpAgreeWarpTypeDataMapSwitch{
			MapId:      s.Character.MapID,
			WarpEffect: server.WarpEffect(anim),
		},
		Nearby: nearby,
	}

	return s.Send(reply)
}

// WarpTake handles Warp_Take — client requesting map file download during warp.
// Responds with the EMF map data via InitInitServerPacket.
type WarpTake struct {
	World *world.World
}

func (h *WarpTake) Handle(s *session.Session, pkt eonet.Packet) error {
	_, ok := pkt.(*client.WarpTakeClientPacket)
	if !ok {
		return fmt.Errorf("expected WarpTakeClientPacket, got %T", pkt)
	}

	if s.Character == nil {
		return nil
	}

	ms := h.World.GetMap(s.Character.MapID)
	if ms == nil || ms.MapFile == nil {
		return nil
	}

	return s.Send(&server.InitInitServerPacket{
		ReplyCode: server.InitReply_FileEmf,
		ReplyCodeData: &server.InitInitReplyCodeDataFileEmf{
			MapFile: server.MapFile{
				Content: ms.MapFile.RawBytes,
			},
		},
	})
}
