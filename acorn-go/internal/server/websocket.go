package server

import (
	"context"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"sync"

	"github.com/acorn-server/acorn-go/internal/config"
	acornnet "github.com/acorn-server/acorn-go/internal/net"
	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/acorn-server/acorn-go/internal/telemetry"
	"github.com/coder/websocket"
)

// WSListener accepts WebSocket connections from EO clients using nhooyr/websocket.
// This enables browser-based clients and nginx reverse-proxy configurations.
type WSListener struct {
	cfg     *config.Config
	manager *session.Manager
	router  *acornnet.Router
	metrics *telemetry.Metrics
	logger  *slog.Logger
}

// NewWSListener creates a new WebSocket listener.
func NewWSListener(cfg *config.Config, manager *session.Manager, router *acornnet.Router, metrics *telemetry.Metrics, logger *slog.Logger) *WSListener {
	return &WSListener{
		cfg:     cfg,
		manager: manager,
		router:  router,
		metrics: metrics,
		logger:  logger.With("transport", "websocket"),
	}
}

// Start begins accepting WebSocket connections. It blocks until the done channel is closed.
func (l *WSListener) Start(done <-chan struct{}) error {
	addr := fmt.Sprintf(":%d", l.cfg.Server.WSPort)

	mux := http.NewServeMux()
	mux.HandleFunc("/", l.handleWS)

	srv := &http.Server{
		Addr:    addr,
		Handler: mux,
	}

	// Shutdown cleanly when done signal arrives.
	go func() {
		<-done
		_ = srv.Close()
	}()

	l.logger.Info("WebSocket listener started", "addr", addr)

	if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		return fmt.Errorf("websocket listen on %s: %w", addr, err)
	}
	return nil
}

func (l *WSListener) handleWS(w http.ResponseWriter, r *http.Request) {
	c, err := websocket.Accept(w, r, &websocket.AcceptOptions{
		// Allow any origin — nginx will handle CORS in production.
		InsecureSkipVerify: true,
		Subprotocols:       []string{"binary"},
	})
	if err != nil {
		l.logger.Warn("websocket accept error", "error", err)
		return
	}

	// Wrap the websocket.Conn in an io.ReadWriteCloser adapter.
	ctx := r.Context()
	rwc := newWSReadWriteCloser(ctx, c)
	remoteIP := r.RemoteAddr

	// Check per-IP limit.
	if l.cfg.Server.MaxConnectionsPerIP > 0 && l.manager.CountByIP(remoteIP) >= l.cfg.Server.MaxConnectionsPerIP {
		l.logger.Warn("per-IP connection limit reached", "ip", remoteIP)
		_ = c.CloseNow()
		return
	}

	// Check global limit.
	if l.cfg.Server.MaxConnections > 0 && l.manager.Count() >= l.cfg.Server.MaxConnections {
		l.logger.Warn("max connections reached")
		_ = c.CloseNow()
		return
	}

	id := l.manager.GenerateID()
	codec := acornnet.NewCodec()
	s := session.NewSession(id, rwc, remoteIP, codec, l.logger)

	if !l.manager.Add(s) {
		l.logger.Error("session ID collision", "id", id)
		_ = c.CloseNow()
		return
	}

	l.logger.Info("client connected (ws)", "session", id, "remote", remoteIP)

	if l.metrics != nil {
		l.metrics.RecordConnection(context.Background(), "websocket", remoteIP)
	}

	defer func() {
		l.manager.Remove(id)
		s.Close()
		if l.metrics != nil {
			l.metrics.RecordDisconnect(context.Background(), "websocket", remoteIP)
		}
		l.logger.Info("client disconnected (ws)", "session", id)
	}()

	readLoop(s, l.router, l.logger, l.cfg)
}

// wsReadWriteCloser adapts a websocket.Conn to io.ReadWriteCloser.
// WebSocket messages are binary frames that contain raw EO protocol data.
type wsReadWriteCloser struct {
	ctx    context.Context
	conn   *websocket.Conn
	mu     sync.Mutex
	reader io.Reader
}

func newWSReadWriteCloser(ctx context.Context, conn *websocket.Conn) *wsReadWriteCloser {
	return &wsReadWriteCloser{ctx: ctx, conn: conn}
}

func (w *wsReadWriteCloser) Read(p []byte) (int, error) {
	// If we have leftover data from a previous message, drain it first.
	if w.reader != nil {
		n, err := w.reader.Read(p)
		if err == io.EOF {
			w.reader = nil
			if n > 0 {
				return n, nil
			}
			// Fall through to read next message.
		} else {
			return n, err
		}
	}

	// Read the next WebSocket message.
	_, reader, err := w.conn.Reader(w.ctx)
	if err != nil {
		return 0, err
	}
	w.reader = reader

	return w.reader.Read(p)
}

func (w *wsReadWriteCloser) Write(p []byte) (int, error) {
	w.mu.Lock()
	defer w.mu.Unlock()

	writer, err := w.conn.Writer(w.ctx, websocket.MessageBinary)
	if err != nil {
		return 0, err
	}
	n, err := writer.Write(p)
	if err != nil {
		return n, err
	}
	return n, writer.Close()
}

func (w *wsReadWriteCloser) Close() error {
	return w.conn.CloseNow()
}
