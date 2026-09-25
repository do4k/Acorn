package handler

import (
	"fmt"
	"log/slog"
	"math/rand"
	"time"

	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/ethanmoffat/eolib-go/v3/packet"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/client"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/server"
)

// ConnectionAccept handles the Connection_Accept packet.
// The client echoes back the encryption multiples and player ID to confirm the handshake.
type ConnectionAccept struct{}

func (h *ConnectionAccept) Handle(s *session.Session, pkt eonet.Packet) error {
	acceptPkt, ok := pkt.(*client.ConnectionAcceptClientPacket)
	if !ok {
		return fmt.Errorf("expected ConnectionAcceptClientPacket, got %T", pkt)
	}

	if s.State() != session.StateInitialized {
		s.Logger().Warn("connection accept in wrong state", "state", s.State())
		return nil
	}

	// Verify the client echoed back the correct values.
	if acceptPkt.ServerEncryptionMultiple != s.ServerEncryptionMulti {
		s.Logger().Warn("server encryption multiple mismatch",
			"expected", s.ServerEncryptionMulti,
			"got", acceptPkt.ServerEncryptionMultiple,
		)
		s.Close()
		return nil
	}
	if acceptPkt.ClientEncryptionMultiple != s.ClientEncryptionMulti {
		s.Logger().Warn("client encryption multiple mismatch",
			"expected", s.ClientEncryptionMulti,
			"got", acceptPkt.ClientEncryptionMultiple,
		)
		s.Close()
		return nil
	}
	if acceptPkt.PlayerId != s.ID() {
		s.Logger().Warn("player ID mismatch",
			"expected", s.ID(),
			"got", acceptPkt.PlayerId,
		)
		s.Close()
		return nil
	}

	s.SetState(session.StateAccepted)
	s.Logger().Info("connection accepted")

	return nil
}

// ConnectionPing handles the Connection_Ping (pong) packet from the client.
// This is the client's response to our keep-alive ping.
type ConnectionPing struct{}

func (h *ConnectionPing) Handle(s *session.Session, pkt eonet.Packet) error {
	_, ok := pkt.(*client.ConnectionPingClientPacket)
	if !ok {
		return fmt.Errorf("expected ConnectionPingClientPacket, got %T", pkt)
	}

	s.NeedPong.Store(false)
	s.Logger().Debug("pong received")

	return nil
}

// PingService sends periodic keep-alive pings to all sessions.
type PingService struct {
	manager  *session.Manager
	interval time.Duration
	logger   *slog.Logger
	rng      *rand.Rand
}

// NewPingService creates a new ping service.
func NewPingService(manager *session.Manager, interval time.Duration, logger *slog.Logger) *PingService {
	return &PingService{
		manager:  manager,
		interval: interval,
		logger:   logger,
		rng:      rand.New(rand.NewSource(rand.Int63())),
	}
}

// Run starts the ping loop. It blocks until the done channel is closed.
func (p *PingService) Run(done <-chan struct{}) {
	ticker := time.NewTicker(p.interval)
	defer ticker.Stop()

	for {
		select {
		case <-done:
			return
		case <-ticker.C:
			p.pingAll()
		}
	}
}

func (p *PingService) pingAll() {
	for _, s := range p.manager.All() {
		// Only ping sessions that are past the init handshake.
		if s.State() < session.StateAccepted {
			continue
		}

		// If the client hasn't responded to the last ping, disconnect.
		if s.NeedPong.Load() {
			p.logger.Info("ping timeout, disconnecting", "session", s.ID())
			s.Close()
			continue
		}

		// Generate a new ping sequence and update the sequencer.
		seq := packet.GeneratePingSequence(p.rng)
		s.Sequencer().SetSequenceStart(seq)

		pingPkt := &server.ConnectionPlayerServerPacket{
			Seq1: seq.Seq1(),
			Seq2: seq.Seq2(),
		}

		if err := s.Send(pingPkt); err != nil {
			p.logger.Warn("failed to send ping", "session", s.ID(), "error", err)
			s.Close()
			continue
		}

		s.NeedPong.Store(true)
	}
}
