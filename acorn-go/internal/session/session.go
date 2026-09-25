package session

import (
	"context"
	"fmt"
	"io"
	"log/slog"
	"net"
	"sync"
	"sync/atomic"

	"github.com/ethanmoffat/eolib-go/v3/packet"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
)

// Codec defines the methods Session needs from the packet codec.
// This avoids an import cycle between session and net packages.
type Codec interface {
	ReadPacket(r io.Reader) ([]byte, error)
	WritePacket(w io.Writer, pkt eonet.Packet) error
	SetEncryptionMultiples(clientMulti, serverMulti int)
	IsEncryptionActive() bool
}

// Account holds authenticated account data for a session.
type Account struct {
	ID       int64
	Username string
}

// Character holds the selected character data for a session.
type Character struct {
	ID         int64
	AccountID  int64
	Name       string
	Title      string
	Home       string
	Partner    string
	Admin      int
	ClassID    int
	Gender     int
	Race       int
	HairStyle  int
	HairColor  int
	MapID      int
	MapX       int
	MapY       int
	Direction  int
	Level      int
	Experience int
	HP         int
	MaxHP      int
	TP         int
	MaxTP      int
	Str        int
	Intl       int
	Wis        int
	Agi        int
	Con        int
	Cha        int
	StatPoints int
	SkillPoints int
	Karma      int
	Sitting    int
	Hidden     int
	GuildRank  int
	GuildRankString string
	BankMax    int
	GoldBank   int
	Usage      int
}

// Session represents a single client connection, regardless of transport (TCP or WebSocket).
// It owns the connection lifecycle, codec state, and packet sequencing.
type Session struct {
	id       int
	conn     io.ReadWriteCloser
	remoteIP string
	codec    Codec
	logger   *slog.Logger

	// State
	state atomic.Int32

	// Sequencing
	sequencer *packet.PacketSequencer

	// Encryption multiples (stored here for handler access).
	ClientEncryptionMulti int
	ServerEncryptionMulti int

	// Authenticated account (nil until login).
	Account *Account

	// Selected character (nil until character selection).
	Character *Character

	// Lifecycle
	ctx    context.Context
	cancel context.CancelFunc
	done   chan struct{}

	// Write serialization — only one goroutine may write at a time.
	writeMu sync.Mutex

	// NeedPong tracks whether we're waiting for a ping response.
	NeedPong atomic.Bool

	// WarpAnimation stores the pending warp animation for the WarpAccept response.
	// Set by the warp logic, consumed by the WarpAccept handler.
	// -1 means no pending warp (invalid).
	WarpAnimation int
}

// NewSession creates a new session wrapping the given connection.
func NewSession(id int, conn io.ReadWriteCloser, remoteIP string, codec Codec, logger *slog.Logger) *Session {
	ctx, cancel := context.WithCancel(context.Background())
	seq := packet.NewPacketSequencer(packet.NewZeroSequence())
	s := &Session{
		id:            id,
		conn:          conn,
		remoteIP:      remoteIP,
		codec:         codec,
		logger:        logger.With("session", id, "remote", remoteIP),
		sequencer:     &seq,
		ctx:           ctx,
		cancel:        cancel,
		done:          make(chan struct{}),
		WarpAnimation: -1,
	}
	s.state.Store(int32(StateUninitialized))
	return s
}

// ID returns the unique session/player ID.
func (s *Session) ID() int { return s.id }

// RemoteIP returns the client's IP address.
func (s *Session) RemoteIP() string { return s.remoteIP }

// State returns the current client connection state.
func (s *Session) State() ClientState { return ClientState(s.state.Load()) }

// SetState atomically updates the client connection state.
func (s *Session) SetState(state ClientState) {
	s.state.Store(int32(state))
	s.logger.Info("state changed", "state", state)
}

// GetCodec returns the underlying codec for handler access (e.g., to set encryption).
func (s *Session) GetCodec() Codec { return s.codec }

// Sequencer returns the packet sequencer for this session.
func (s *Session) Sequencer() *packet.PacketSequencer { return s.sequencer }

// Send serializes, encrypts, and writes a packet to the client.
// Safe for concurrent use.
func (s *Session) Send(pkt eonet.Packet) error {
	s.writeMu.Lock()
	defer s.writeMu.Unlock()

	if err := s.codec.WritePacket(s.conn, pkt); err != nil {
		return fmt.Errorf("sending packet family=%d action=%d: %w", pkt.Family(), pkt.Action(), err)
	}
	return nil
}

// ReadRawPacket reads a single raw (decrypted) packet from the connection.
// Returns the body bytes (action + family + sequence + payload).
func (s *Session) ReadRawPacket() ([]byte, error) {
	return s.codec.ReadPacket(s.conn)
}

// Context returns the session's context, which is cancelled on disconnect.
func (s *Session) Context() context.Context { return s.ctx }

// Done returns a channel that is closed when the session ends.
func (s *Session) Done() <-chan struct{} { return s.done }

// Close terminates the session, closing the underlying connection.
func (s *Session) Close() {
	s.cancel()
	_ = s.conn.Close()
	select {
	case <-s.done:
	default:
		close(s.done)
	}
}

// Logger returns the session's logger (pre-tagged with session info).
func (s *Session) Logger() *slog.Logger { return s.logger }

// RemoteAddr attempts to extract the remote address from the underlying connection.
func RemoteAddr(conn io.ReadWriteCloser) string {
	type hasRemoteAddr interface {
		RemoteAddr() net.Addr
	}
	if c, ok := conn.(hasRemoteAddr); ok {
		host, _, err := net.SplitHostPort(c.RemoteAddr().String())
		if err == nil {
			return host
		}
		return c.RemoteAddr().String()
	}
	return "unknown"
}
