package world

import (
	"sync"

	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/ethanmoffat/eolib-go/v3/protocol"
	eomap "github.com/ethanmoffat/eolib-go/v3/protocol/map"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/server"
)

// SeeDistance is the Manhattan distance for player visibility (matches eoserv default).
const SeeDistance = 11

// WalkResult describes the outcome of a walk attempt.
type WalkResult int

const (
	WalkFail   WalkResult = 0
	WalkOK     WalkResult = 1
	WalkWarped WalkResult = 2
)

// MapState holds the runtime state for a single map.
type MapState struct {
	mu sync.RWMutex

	ID      int
	MapFile *MapFile

	// Players currently on this map, keyed by session ID.
	Players map[int]*session.Session

	// NPCs on this map.
	NPCs []*NPCState

	// Ground items.
	Items []GroundItem

	nextItemUID int
}

// GroundItem represents an item on the ground.
type GroundItem struct {
	UID    int
	ItemID int
	Amount int
	X      int
	Y      int
}

// NPCState holds the runtime state for a single NPC instance on a map.
type NPCState struct {
	Index     int
	ID        int // NPC type ID (from ENF)
	SpawnX    int
	SpawnY    int
	X         int
	Y         int
	Direction int
	HP        int
	MaxHP     int
	Alive     bool
	SpawnType int
	SpawnTime int // Seconds until respawn

	// Timing (in ticks)
	DeadSinceTick int
	LastActTick   int
}

// NewMapState creates a new MapState from a loaded map file.
func NewMapState(id int, mf *MapFile) *MapState {
	ms := &MapState{
		ID:      id,
		MapFile: mf,
		Players: make(map[int]*session.Session),
	}

	// Spawn NPCs from the map file.
	if mf != nil && mf.EMF != nil {
		for _, npcSpawn := range mf.EMF.Npcs {
			for i := 0; i < npcSpawn.Amount; i++ {
				npc := &NPCState{
					Index:     len(ms.NPCs),
					ID:        npcSpawn.Id,
					SpawnX:    npcSpawn.Coords.X,
					SpawnY:    npcSpawn.Coords.Y,
					X:         npcSpawn.Coords.X,
					Y:         npcSpawn.Coords.Y,
					Direction: 0,
					Alive:     true,
					SpawnType: npcSpawn.SpawnType,
					SpawnTime: npcSpawn.SpawnTime,
				}
				ms.NPCs = append(ms.NPCs, npc)
			}
		}
	}

	return ms
}

// ---------------------------------------------------------------------------
// Player add / remove
// ---------------------------------------------------------------------------

// AddPlayer adds a session to this map.
func (ms *MapState) AddPlayer(s *session.Session) {
	ms.mu.Lock()
	defer ms.mu.Unlock()
	ms.Players[s.ID()] = s
}

// RemovePlayer removes a session from this map.
func (ms *MapState) RemovePlayer(id int) {
	ms.mu.Lock()
	defer ms.mu.Unlock()
	delete(ms.Players, id)
}

// GetPlayers returns a snapshot of all players on this map.
func (ms *MapState) GetPlayers() []*session.Session {
	ms.mu.RLock()
	defer ms.mu.RUnlock()

	result := make([]*session.Session, 0, len(ms.Players))
	for _, s := range ms.Players {
		result = append(result, s)
	}
	return result
}

// ---------------------------------------------------------------------------
// Enter / Leave (broadcast to nearby players)
// ---------------------------------------------------------------------------

// Enter announces a player's arrival to all nearby players on this map.
// Sends PLAYERS_AGREE with the joining character's info.
func (ms *MapState) Enter(s *session.Session) {
	ms.mu.Lock()
	ms.Players[s.ID()] = s
	ms.mu.Unlock()

	ch := s.Character
	if ch == nil {
		return
	}

	info := ms.buildCharacterMapInfo(s)

	ms.mu.RLock()
	defer ms.mu.RUnlock()

	for _, other := range ms.Players {
		if other.ID() == s.ID() || other.Character == nil {
			continue
		}
		if !inRange(ch.MapX, ch.MapY, other.Character.MapX, other.Character.MapY) {
			continue
		}
		_ = other.Send(&server.PlayersAgreeServerPacket{
			Nearby: server.NearbyInfo{
				Characters: []server.CharacterMapInfo{info},
			},
		})
	}
}

// Leave announces a player's departure to all nearby players on this map.
// Sends AVATAR_REMOVE. Optionally includes a warp effect.
func (ms *MapState) Leave(s *session.Session, effect *server.WarpEffect) {
	ch := s.Character

	ms.mu.Lock()
	delete(ms.Players, s.ID())
	ms.mu.Unlock()

	if ch == nil {
		return
	}

	pkt := &server.AvatarRemoveServerPacket{
		PlayerId:   s.ID(),
		WarpEffect: effect,
	}

	ms.mu.RLock()
	defer ms.mu.RUnlock()

	for _, other := range ms.Players {
		if other.Character == nil {
			continue
		}
		if !inRange(ch.MapX, ch.MapY, other.Character.MapX, other.Character.MapY) {
			continue
		}
		_ = other.Send(pkt)
	}
}

// ---------------------------------------------------------------------------
// Walk
// ---------------------------------------------------------------------------

// Walk attempts to move a player in the given direction.
// Returns the WalkResult and handles all broadcasting.
// admin=true bypasses wall checks (for #nowall admin walk).
func (ms *MapState) Walk(s *session.Session, direction int, admin bool) WalkResult {
	ch := s.Character
	if ch == nil {
		return WalkFail
	}

	// Cannot walk while sitting.
	if ch.Sitting != 0 {
		return WalkFail
	}

	// Validate direction.
	if direction < 0 || direction > 3 {
		return WalkFail
	}

	// Compute target position.
	targetX, targetY := ch.MapX, ch.MapY
	switch direction {
	case 0: // Down
		targetY++
	case 1: // Left
		targetX--
	case 2: // Up
		targetY--
	case 3: // Right
		targetX++
	}

	// Bounds check.
	if !ms.inBounds(targetX, targetY) {
		return WalkFail
	}

	// Walkability check (skip for admin walk).
	if !admin && !ms.isWalkable(targetX, targetY) {
		return WalkFail
	}

	// Check for player collision (non-admin only).
	if !admin && ms.isOccupiedByPlayer(targetX, targetY, s.ID()) {
		return WalkFail
	}

	// TODO: Check for warps (level requirements, doors) and handle warp case.
	// For now, warps are not yet implemented.

	// Save old position for edge-tile calculations.
	oldX, oldY := ch.MapX, ch.MapY

	// Update position and direction.
	ms.mu.Lock()
	ch.MapX = targetX
	ch.MapY = targetY
	ch.Direction = direction
	ms.mu.Unlock()

	// Broadcast the walk to all in-range players.
	walkPkt := &server.WalkPlayerServerPacket{
		PlayerId:  s.ID(),
		Direction: protocol.Direction(direction),
		Coords:    protocol.Coords{X: targetX, Y: targetY},
	}

	ms.mu.RLock()
	players := ms.getPlayersSnapshot()
	ms.mu.RUnlock()

	for _, other := range players {
		if other.ID() == s.ID() || other.Character == nil {
			continue
		}
		ox, oy := other.Character.MapX, other.Character.MapY

		wasInRange := inRange(oldX, oldY, ox, oy)
		nowInRange := inRange(targetX, targetY, ox, oy)

		switch {
		case !wasInRange && nowInRange:
			// Player entered other's view — send appearance to both.
			myInfo := ms.buildCharacterMapInfo(s)
			_ = other.Send(&server.PlayersAgreeServerPacket{
				Nearby: server.NearbyInfo{
					Characters: []server.CharacterMapInfo{myInfo},
				},
			})
			otherInfo := ms.buildCharacterMapInfo(other)
			_ = s.Send(&server.PlayersAgreeServerPacket{
				Nearby: server.NearbyInfo{
					Characters: []server.CharacterMapInfo{otherInfo},
				},
			})

		case wasInRange && !nowInRange:
			// Player left other's view — remove from both.
			_ = other.Send(&server.AvatarRemoveServerPacket{PlayerId: s.ID()})
			_ = s.Send(&server.AvatarRemoveServerPacket{PlayerId: other.ID()})

		case wasInRange && nowInRange:
			// Still in range — send walk animation.
			_ = other.Send(walkPkt)
		}
	}

	// Send WALK_REPLY to the walking player with items entering view.
	// TODO: Also populate NPCs entering view.
	itemsInView := ms.getItemsEnteringView(oldX, oldY, targetX, targetY)
	if len(itemsInView) > 0 {
		_ = s.Send(&server.WalkReplyServerPacket{
			Items: itemsInView,
		})
	}

	return WalkOK
}

// ---------------------------------------------------------------------------
// Face
// ---------------------------------------------------------------------------

// Face changes a player's direction and broadcasts to nearby players.
func (ms *MapState) Face(s *session.Session, direction int) {
	ch := s.Character
	if ch == nil {
		return
	}

	// Cannot face while sitting.
	if ch.Sitting != 0 {
		return
	}

	if direction < 0 || direction > 3 {
		return
	}

	ms.mu.Lock()
	ch.Direction = direction
	ms.mu.Unlock()

	pkt := &server.FacePlayerServerPacket{
		PlayerId:  s.ID(),
		Direction: protocol.Direction(direction),
	}

	ms.mu.RLock()
	defer ms.mu.RUnlock()

	for _, other := range ms.Players {
		if other.ID() == s.ID() || other.Character == nil {
			continue
		}
		if !inRange(ch.MapX, ch.MapY, other.Character.MapX, other.Character.MapY) {
			continue
		}
		_ = other.Send(pkt)
	}
}

// ---------------------------------------------------------------------------
// Sit / Stand
// ---------------------------------------------------------------------------

// SitFloor makes a player sit on the floor and broadcasts.
func (ms *MapState) SitFloor(s *session.Session) {
	ch := s.Character
	if ch == nil || ch.Sitting != 0 {
		return
	}

	ms.mu.Lock()
	ch.Sitting = int(server.SitState_Floor)
	ms.mu.Unlock()

	// Notify the sitting player.
	_ = s.Send(&server.SitReplyServerPacket{
		PlayerId:  s.ID(),
		Coords:    protocol.Coords{X: ch.MapX, Y: ch.MapY},
		Direction: protocol.Direction(ch.Direction),
	})

	// Broadcast to nearby.
	pkt := &server.SitPlayerServerPacket{
		PlayerId:  s.ID(),
		Coords:    protocol.Coords{X: ch.MapX, Y: ch.MapY},
		Direction: protocol.Direction(ch.Direction),
	}
	ms.broadcastInRange(s, pkt)
}

// Stand makes a sitting player stand up and broadcasts.
func (ms *MapState) Stand(s *session.Session) {
	ch := s.Character
	if ch == nil || ch.Sitting == int(server.SitState_Stand) {
		return
	}

	ms.mu.Lock()
	ch.Sitting = int(server.SitState_Stand)
	ms.mu.Unlock()

	// Notify the standing player.
	_ = s.Send(&server.SitCloseServerPacket{
		PlayerId: s.ID(),
		Coords:   protocol.Coords{X: ch.MapX, Y: ch.MapY},
	})

	// Broadcast to nearby.
	pkt := &server.SitRemoveServerPacket{
		PlayerId: s.ID(),
		Coords:   protocol.Coords{X: ch.MapX, Y: ch.MapY},
	}
	ms.broadcastInRange(s, pkt)
}

// SitChair makes a player sit on a chair at the given coords.
func (ms *MapState) SitChair(s *session.Session, chairX, chairY int) bool {
	ch := s.Character
	if ch == nil || ch.Sitting != int(server.SitState_Stand) {
		return false
	}

	// Must be adjacent (Manhattan distance <= 1).
	if abs(ch.MapX-chairX)+abs(ch.MapY-chairY) > 1 {
		return false
	}

	// Must be a chair tile.
	tileSpec, hasSpec := ms.getTileSpec(chairX, chairY)
	if !hasSpec {
		return false
	}

	dir := -1
	switch tileSpec {
	case eomap.MapTileSpec_ChairDown:
		dir = 0
	case eomap.MapTileSpec_ChairLeft:
		dir = 1
	case eomap.MapTileSpec_ChairUp:
		dir = 2
	case eomap.MapTileSpec_ChairRight:
		dir = 3
	case eomap.MapTileSpec_ChairDownRight:
		dir = 0 // face down
	case eomap.MapTileSpec_ChairUpLeft:
		dir = 2 // face up
	case eomap.MapTileSpec_ChairAll:
		dir = ch.Direction // keep current direction
	default:
		return false
	}

	// Check no other player sitting at this chair tile.
	ms.mu.RLock()
	for _, other := range ms.Players {
		if other.ID() == s.ID() || other.Character == nil {
			continue
		}
		oc := other.Character
		if oc.MapX == chairX && oc.MapY == chairY && oc.Sitting == int(server.SitState_Chair) {
			ms.mu.RUnlock()
			return false
		}
	}
	ms.mu.RUnlock()

	ms.mu.Lock()
	ch.MapX = chairX
	ch.MapY = chairY
	ch.Direction = dir
	ch.Sitting = int(server.SitState_Chair)
	ms.mu.Unlock()

	// Notify the sitting player.
	_ = s.Send(&server.ChairReplyServerPacket{
		PlayerId:  s.ID(),
		Coords:    protocol.Coords{X: chairX, Y: chairY},
		Direction: protocol.Direction(dir),
	})

	// Broadcast to nearby.
	pkt := &server.ChairPlayerServerPacket{
		PlayerId:  s.ID(),
		Coords:    protocol.Coords{X: chairX, Y: chairY},
		Direction: protocol.Direction(dir),
	}
	ms.broadcastInRange(s, pkt)
	return true
}

// StandFromChair makes a player stand from a chair, moving them back one tile.
func (ms *MapState) StandFromChair(s *session.Session) {
	ch := s.Character
	if ch == nil || ch.Sitting != int(server.SitState_Chair) {
		return
	}

	// Move back one tile opposite the chair direction.
	newX, newY := ch.MapX, ch.MapY
	switch ch.Direction {
	case 0: // facing down -> move up
		newY--
	case 1: // facing left -> move right
		newX++
	case 2: // facing up -> move down
		newY++
	case 3: // facing right -> move left
		newX--
	}

	// If target tile isn't valid, just stand in place.
	if !ms.inBounds(newX, newY) || !ms.isWalkable(newX, newY) {
		newX, newY = ch.MapX, ch.MapY
	}

	ms.mu.Lock()
	ch.MapX = newX
	ch.MapY = newY
	ch.Sitting = int(server.SitState_Stand)
	ms.mu.Unlock()

	// Notify the standing player.
	_ = s.Send(&server.ChairCloseServerPacket{
		PlayerId: s.ID(),
		Coords:   protocol.Coords{X: newX, Y: newY},
	})

	// Broadcast to nearby.
	pkt := &server.ChairRemoveServerPacket{
		PlayerId: s.ID(),
		Coords:   protocol.Coords{X: newX, Y: newY},
	}
	ms.broadcastInRange(s, pkt)
}

// ---------------------------------------------------------------------------
// Chat (map-scoped)
// ---------------------------------------------------------------------------

// Msg broadcasts a public chat message to nearby players (excluding sender).
func (ms *MapState) Msg(s *session.Session, message string) {
	ch := s.Character
	if ch == nil {
		return
	}

	pkt := &server.TalkPlayerServerPacket{
		PlayerId: s.ID(),
		Message:  message,
	}

	ms.mu.RLock()
	defer ms.mu.RUnlock()

	for _, other := range ms.Players {
		if other.ID() == s.ID() || other.Character == nil {
			continue
		}
		if !inRange(ch.MapX, ch.MapY, other.Character.MapX, other.Character.MapY) {
			continue
		}
		_ = other.Send(pkt)
	}
}

// ---------------------------------------------------------------------------
// Refresh / NearbyInfo
// ---------------------------------------------------------------------------

// Refresh sends a full state resync to the player (all nearby characters, NPCs, items).
// Used for desync recovery and map re-entry.
func (ms *MapState) Refresh(s *session.Session) {
	nearby := ms.GetNearbyInfo(s.ID())
	_ = s.Send(&server.RefreshReplyServerPacket{
		Nearby: nearby,
	})
}

// GetNearbyInfo builds the NearbyInfo struct for the Welcome enter-game packet.
// Includes the player themselves (matching eoserv behaviour — no self-exclusion).
func (ms *MapState) GetNearbyInfo(sessionID int) server.NearbyInfo {
	ms.mu.RLock()
	defer ms.mu.RUnlock()

	info := server.NearbyInfo{}

	// Characters on this map (including the requesting player — eoserv includes self).
	for _, s := range ms.Players {
		if s.Character == nil {
			continue
		}
		info.Characters = append(info.Characters, ms.buildCharacterMapInfoLocked(s))
	}

	// Alive NPCs on this map.
	for _, npc := range ms.NPCs {
		if !npc.Alive {
			continue
		}
		info.Npcs = append(info.Npcs, server.NpcMapInfo{
			Index:     npc.Index,
			Id:        npc.ID,
			Coords:    protocolCoords(npc.X, npc.Y),
			Direction: protocolDirection(npc.Direction),
		})
	}

	// Ground items.
	for _, item := range ms.Items {
		info.Items = append(info.Items, server.ItemMapInfo{
			Uid:    item.UID,
			Id:     item.ItemID,
			Coords: protocolCoords(item.X, item.Y),
			Amount: item.Amount,
		})
	}

	return info
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

// broadcastInRange sends a packet to all nearby players excluding the sender.
func (ms *MapState) broadcastInRange(s *session.Session, pkt eonet.Packet) {
	ch := s.Character
	if ch == nil {
		return
	}

	ms.mu.RLock()
	defer ms.mu.RUnlock()

	for _, other := range ms.Players {
		if other.ID() == s.ID() || other.Character == nil {
			continue
		}
		if !inRange(ch.MapX, ch.MapY, other.Character.MapX, other.Character.MapY) {
			continue
		}
		_ = other.Send(pkt)
	}
}

// getPlayersSnapshot returns a copy of the players map (caller should NOT hold mu).
func (ms *MapState) getPlayersSnapshot() []*session.Session {
	result := make([]*session.Session, 0, len(ms.Players))
	for _, s := range ms.Players {
		result = append(result, s)
	}
	return result
}

// buildCharacterMapInfo creates the protocol CharacterMapInfo for broadcasting.
// Must NOT be called while holding ms.mu.
func (ms *MapState) buildCharacterMapInfo(s *session.Session) server.CharacterMapInfo {
	ms.mu.RLock()
	defer ms.mu.RUnlock()
	return ms.buildCharacterMapInfoLocked(s)
}

// buildCharacterMapInfoLocked creates CharacterMapInfo (caller must hold ms.mu for read).
func (ms *MapState) buildCharacterMapInfoLocked(s *session.Session) server.CharacterMapInfo {
	ch := s.Character
	return server.CharacterMapInfo{
		Name:      ch.Name,
		PlayerId:  s.ID(),
		MapId:     ch.MapID,
		Coords:    server.BigCoords{X: ch.MapX, Y: ch.MapY},
		Direction: protocolDirection(ch.Direction),
		ClassId:   ch.ClassID,
		GuildTag:  "   ",
		Level:     ch.Level,
		Gender:    protocolGender(ch.Gender),
		HairStyle: ch.HairStyle,
		HairColor: ch.HairColor,
		Skin:      ch.Race,
		MaxHp:     ch.MaxHP,
		Hp:        ch.HP,
		MaxTp:     ch.MaxTP,
		Tp:        ch.TP,
		SitState:  server.SitState(ch.Sitting),
		Invisible: ch.Hidden != 0,
	}
}

// inBounds checks whether (x, y) is within the map dimensions.
func (ms *MapState) inBounds(x, y int) bool {
	if ms.MapFile == nil || ms.MapFile.EMF == nil {
		return false
	}
	return x >= 0 && y >= 0 && x < ms.MapFile.EMF.Width && y < ms.MapFile.EMF.Height
}

// getTileSpec returns the tile spec at (x, y) from the sparse EMF data.
func (ms *MapState) getTileSpec(x, y int) (eomap.MapTileSpec, bool) {
	if ms.MapFile == nil || ms.MapFile.EMF == nil {
		return 0, false
	}
	for _, row := range ms.MapFile.EMF.TileSpecRows {
		if row.Y == y {
			for _, tile := range row.Tiles {
				if tile.X == x {
					return tile.TileSpec, true
				}
			}
			return 0, false
		}
	}
	return 0, false
}

// isWalkable checks if a tile can be walked on.
// Tiles with no spec are walkable. Walls, edges, and certain special tiles block.
func (ms *MapState) isWalkable(x, y int) bool {
	spec, hasSpec := ms.getTileSpec(x, y)
	if !hasSpec {
		return true // No tile spec = normal ground = walkable.
	}

	switch spec {
	case eomap.MapTileSpec_Wall,
		eomap.MapTileSpec_Edge,
		eomap.MapTileSpec_ChairDown,
		eomap.MapTileSpec_ChairLeft,
		eomap.MapTileSpec_ChairRight,
		eomap.MapTileSpec_ChairUp,
		eomap.MapTileSpec_ChairDownRight,
		eomap.MapTileSpec_ChairUpLeft,
		eomap.MapTileSpec_ChairAll,
		eomap.MapTileSpec_Chest,
		eomap.MapTileSpec_BankVault,
		eomap.MapTileSpec_Board1,
		eomap.MapTileSpec_Board2,
		eomap.MapTileSpec_Board3,
		eomap.MapTileSpec_Board4,
		eomap.MapTileSpec_Board5,
		eomap.MapTileSpec_Board6,
		eomap.MapTileSpec_Board7,
		eomap.MapTileSpec_Board8,
		eomap.MapTileSpec_Jukebox:
		return false
	default:
		return true
	}
}

// isOccupiedByPlayer checks if another player is standing on (x, y).
func (ms *MapState) isOccupiedByPlayer(x, y, excludeID int) bool {
	ms.mu.RLock()
	defer ms.mu.RUnlock()

	for _, other := range ms.Players {
		if other.ID() == excludeID || other.Character == nil {
			continue
		}
		if other.Character.MapX == x && other.Character.MapY == y {
			return true
		}
	}
	return false
}

// getItemsEnteringView returns ground items that are now in view after a walk.
func (ms *MapState) getItemsEnteringView(oldX, oldY, newX, newY int) []server.ItemMapInfo {
	ms.mu.RLock()
	defer ms.mu.RUnlock()

	var items []server.ItemMapInfo
	for _, item := range ms.Items {
		wasInView := inRange(oldX, oldY, item.X, item.Y)
		nowInView := inRange(newX, newY, item.X, item.Y)
		if !wasInView && nowInView {
			items = append(items, server.ItemMapInfo{
				Uid:    item.UID,
				Id:     item.ItemID,
				Coords: protocolCoords(item.X, item.Y),
				Amount: item.Amount,
			})
		}
	}
	return items
}

// inRange checks if two positions are within SeeDistance (Manhattan distance).
func inRange(x1, y1, x2, y2 int) bool {
	return abs(x1-x2)+abs(y1-y2) <= SeeDistance
}

func abs(n int) int {
	if n < 0 {
		return -n
	}
	return n
}
