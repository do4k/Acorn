package world

import (
	"log/slog"
	"sync"
	"time"

	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/server"
)

// TickRate is the interval between world tick iterations.
const TickRate = 50 * time.Millisecond

// World holds all global game state: maps, pub files, and tick-driven systems.
//
// Design: A single goroutine runs the tick loop. Packet handlers interact with
// map/world state through mutex-protected methods. This gives us eoserv's simplicity
// (single logical thread for game state mutations) with Go's concurrency model.
type World struct {
	mu sync.RWMutex

	Maps     map[int]*MapState
	PubFiles *PubFiles

	logger *slog.Logger

	// Tick counters for interval-based events.
	tick         int64
	npcSpawnRate int64 // Check every 20 ticks (1s at 50ms tick)
	recoveryRate int64 // Player HP/TP recovery every 1800 ticks (90s)
}

// New creates a new World, loading maps and pub files.
func New(pubDir, mapDir string, maxMaps int, logger *slog.Logger) (*World, error) {
	pubFiles, err := LoadPubFiles(pubDir, logger.With("component", "pub"))
	if err != nil {
		return nil, err
	}

	maps, err := LoadAllMaps(mapDir, maxMaps, logger.With("component", "maps"))
	if err != nil {
		return nil, err
	}

	// Create MapState for each loaded map.
	mapStates := make(map[int]*MapState, len(maps))
	for id, mf := range maps {
		mapStates[id] = NewMapState(id, mf)
	}

	return &World{
		Maps:         mapStates,
		PubFiles:     pubFiles,
		logger:       logger.With("component", "world"),
		npcSpawnRate: 20,   // 20 ticks = 1s
		recoveryRate: 1800, // 1800 ticks = 90s
	}, nil
}

// GetMap returns the MapState for a given map ID, or nil if it doesn't exist.
func (w *World) GetMap(id int) *MapState {
	w.mu.RLock()
	defer w.mu.RUnlock()
	return w.Maps[id]
}

// GlobalMsg broadcasts a global chat message to all online players (excluding sender).
func (w *World) GlobalMsg(sender *session.Session, message string) {
	ch := sender.Character
	if ch == nil {
		return
	}

	pkt := &server.TalkMsgServerPacket{
		PlayerName: ch.Name,
		Message:    message,
	}

	w.mu.RLock()
	defer w.mu.RUnlock()

	for _, ms := range w.Maps {
		ms.mu.RLock()
		for _, s := range ms.Players {
			if s.ID() == sender.ID() || s.Character == nil {
				continue
			}
			_ = s.Send(pkt)
		}
		ms.mu.RUnlock()
	}
}

// AdminMsg broadcasts an admin chat message to all online admins at or above minLevel.
func (w *World) AdminMsg(sender *session.Session, message string, minLevel int) {
	ch := sender.Character
	if ch == nil {
		return
	}

	pkt := &server.TalkAdminServerPacket{
		PlayerName: ch.Name,
		Message:    message,
	}

	w.mu.RLock()
	defer w.mu.RUnlock()

	for _, ms := range w.Maps {
		ms.mu.RLock()
		for _, s := range ms.Players {
			if s.ID() == sender.ID() || s.Character == nil {
				continue
			}
			if s.Character.Admin < minLevel {
				continue
			}
			_ = s.Send(pkt)
		}
		ms.mu.RUnlock()
	}
}

// AnnounceMsg broadcasts a server announcement to all online players (excluding sender).
func (w *World) AnnounceMsg(sender *session.Session, message string) {
	ch := sender.Character
	if ch == nil {
		return
	}

	pkt := &server.TalkAnnounceServerPacket{
		PlayerName: ch.Name,
		Message:    message,
	}

	w.mu.RLock()
	defer w.mu.RUnlock()

	for _, ms := range w.Maps {
		ms.mu.RLock()
		for _, s := range ms.Players {
			if s.ID() == sender.ID() || s.Character == nil {
				continue
			}
			_ = s.Send(pkt)
		}
		ms.mu.RUnlock()
	}
}

// ServerMsg sends a server message to a single player (appears in the chat log).
func (w *World) ServerMsg(s *session.Session, message string) {
	_ = s.Send(&server.TalkServerServerPacket{
		Message: message,
	})
}

// Warp moves a player to a new map position. Handles leave/enter broadcasts,
// sends the WarpRequest to the client, and stores the pending warp animation
// for when the client responds with WarpAccept.
// Returns false if the target map doesn't exist.
func (w *World) Warp(s *session.Session, mapID, x, y int, animation server.WarpEffect) bool {
	ch := s.Character
	if ch == nil {
		return false
	}

	targetMap := w.GetMap(mapID)
	if targetMap == nil {
		return false
	}

	// Leave the old map (broadcasts AvatarRemove to nearby).
	oldMap := w.GetMap(ch.MapID)
	if oldMap != nil {
		var anim *server.WarpEffect
		if animation != 0 {
			anim = &animation
		}
		oldMap.Leave(s, anim)
	}

	// Update character position.
	ch.MapID = mapID
	ch.MapX = x
	ch.MapY = y
	ch.Sitting = 0 // Stand on warp.

	// Enter the new map (broadcasts PlayersAgree to nearby).
	targetMap.Enter(s)

	// Store warp animation for the WarpAccept response.
	s.WarpAnimation = int(animation)

	// Build the WarpRequest packet.
	sameMap := oldMap != nil && oldMap.ID == mapID
	if sameMap {
		// Local warp (same map).
		_ = s.Send(&server.WarpRequestServerPacket{
			WarpType:  server.Warp_Local,
			MapId:     mapID,
			SessionId: s.ID(),
		})
	} else {
		// Map switch.
		var mapRid []int
		var mapFileSize int
		if targetMap.MapFile != nil {
			mapRid = targetMap.MapFile.EMF.Rid
			mapFileSize = len(targetMap.MapFile.RawBytes)
		}
		for len(mapRid) < 4 {
			mapRid = append(mapRid, 0)
		}

		_ = s.Send(&server.WarpRequestServerPacket{
			WarpType: server.Warp_MapSwitch,
			MapId:    mapID,
			WarpTypeData: &server.WarpRequestWarpTypeDataMapSwitch{
				MapRid:      mapRid,
				MapFileSize: mapFileSize,
			},
			SessionId: s.ID(),
		})
	}

	return true
}

// ---------------------------------------------------------------------------
// GameStateProvider implementation (for telemetry observable gauges)
// ---------------------------------------------------------------------------

// OnlinePlayerCount returns the total number of players currently in-game.
func (w *World) OnlinePlayerCount() int {
	w.mu.RLock()
	defer w.mu.RUnlock()

	count := 0
	for _, ms := range w.Maps {
		ms.mu.RLock()
		count += len(ms.Players)
		ms.mu.RUnlock()
	}
	return count
}

// MapPlayerCounts returns a map of mapID -> player count for all populated maps.
func (w *World) MapPlayerCounts() map[int]int {
	w.mu.RLock()
	defer w.mu.RUnlock()

	counts := make(map[int]int)
	for id, ms := range w.Maps {
		ms.mu.RLock()
		n := len(ms.Players)
		ms.mu.RUnlock()
		if n > 0 {
			counts[id] = n
		}
	}
	return counts
}

// AliveNPCCount returns the total number of alive NPCs across all maps.
func (w *World) AliveNPCCount() int {
	w.mu.RLock()
	defer w.mu.RUnlock()

	count := 0
	for _, ms := range w.Maps {
		ms.mu.RLock()
		for _, npc := range ms.NPCs {
			if npc.Alive {
				count++
			}
		}
		ms.mu.RUnlock()
	}
	return count
}

// GroundItemCount returns the total number of ground items across all maps.
func (w *World) GroundItemCount() int {
	w.mu.RLock()
	defer w.mu.RUnlock()

	count := 0
	for _, ms := range w.Maps {
		ms.mu.RLock()
		count += len(ms.Items)
		ms.mu.RUnlock()
	}
	return count
}

// Run starts the world tick loop. It blocks until the done channel is closed.
func (w *World) Run(done <-chan struct{}) {
	ticker := time.NewTicker(TickRate)
	defer ticker.Stop()

	w.logger.Info("world tick loop started", "rate", TickRate)

	for {
		select {
		case <-done:
			w.logger.Info("world tick loop stopped")
			return
		case <-ticker.C:
			w.doTick()
		}
	}
}

// doTick executes one world tick. Called every TickRate (50ms).
func (w *World) doTick() {
	w.tick++

	// NPC spawning check (every ~1s).
	if w.tick%w.npcSpawnRate == 0 {
		w.tickNPCSpawns()
	}

	// NPC acting (every tick — 50ms, but each NPC has its own speed gating).
	w.tickNPCAct()

	// Player recovery (every ~90s).
	if w.tick%w.recoveryRate == 0 {
		w.tickPlayerRecovery()
	}
}

// tickNPCSpawns checks dead NPCs across all maps and respawns them if their timer has elapsed.
func (w *World) tickNPCSpawns() {
	w.mu.RLock()
	defer w.mu.RUnlock()

	for _, ms := range w.Maps {
		ms.mu.Lock()
		for _, npc := range ms.NPCs {
			if npc.Alive {
				continue
			}
			// Check if enough time has passed since death.
			ticksSinceDeath := int(w.tick) - npc.DeadSinceTick
			respawnTicks := npc.SpawnTime * int(w.npcSpawnRate) // Convert seconds to ticks.
			if ticksSinceDeath >= respawnTicks {
				npc.X = npc.SpawnX
				npc.Y = npc.SpawnY
				npc.HP = npc.MaxHP
				npc.Alive = true
			}
		}
		ms.mu.Unlock()
	}
}

// tickNPCAct processes NPC behavior (movement, combat) based on each NPC's act speed.
func (w *World) tickNPCAct() {
	// NPC AI will be implemented in a future iteration.
	// For now this is a placeholder that establishes the tick infrastructure.
}

// tickPlayerRecovery restores HP/TP for all players on all maps.
func (w *World) tickPlayerRecovery() {
	// Player recovery will be implemented in a future iteration.
	// For now this is a placeholder that establishes the tick infrastructure.
}
