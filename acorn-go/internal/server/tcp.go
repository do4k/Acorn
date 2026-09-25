package server

import (
	"context"
	"fmt"
	"log/slog"
	"net"

	"github.com/acorn-server/acorn-go/internal/config"
	acornnet "github.com/acorn-server/acorn-go/internal/net"
	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/acorn-server/acorn-go/internal/telemetry"
)

// TCPListener accepts raw TCP connections from EO clients.
type TCPListener struct {
	cfg      *config.Config
	manager  *session.Manager
	router   *acornnet.Router
	logger   *slog.Logger
	metrics  *telemetry.Metrics
	listener net.Listener
}

// NewTCPListener creates a new TCP listener.
func NewTCPListener(cfg *config.Config, manager *session.Manager, router *acornnet.Router, metrics *telemetry.Metrics, logger *slog.Logger) *TCPListener {
	return &TCPListener{
		cfg:     cfg,
		manager: manager,
		router:  router,
		metrics: metrics,
		logger:  logger.With("transport", "tcp"),
	}
}

// Start begins accepting TCP connections. It blocks until the done channel is closed.
func (l *TCPListener) Start(done <-chan struct{}) error {
	addr := fmt.Sprintf(":%d", l.cfg.Server.TCPPort)
	ln, err := net.Listen("tcp", addr)
	if err != nil {
		return fmt.Errorf("tcp listen on %s: %w", addr, err)
	}
	l.listener = ln
	l.logger.Info("TCP listener started", "addr", addr)

	// Close the listener when done signal arrives.
	go func() {
		<-done
		_ = ln.Close()
	}()

	for {
		conn, err := ln.Accept()
		if err != nil {
			select {
			case <-done:
				return nil // Clean shutdown.
			default:
				l.logger.Error("accept error", "error", err)
				continue
			}
		}

		go l.handleConn(conn)
	}
}

func (l *TCPListener) handleConn(conn net.Conn) {
	remoteIP := session.RemoteAddr(conn)

	// Check per-IP limit.
	if l.cfg.Server.MaxConnectionsPerIP > 0 && l.manager.CountByIP(remoteIP) >= l.cfg.Server.MaxConnectionsPerIP {
		l.logger.Warn("per-IP connection limit reached", "ip", remoteIP)
		_ = conn.Close()
		return
	}

	// Check global limit.
	if l.cfg.Server.MaxConnections > 0 && l.manager.Count() >= l.cfg.Server.MaxConnections {
		l.logger.Warn("max connections reached")
		_ = conn.Close()
		return
	}

	id := l.manager.GenerateID()
	codec := acornnet.NewCodec()
	s := session.NewSession(id, conn, remoteIP, codec, l.logger)

	if !l.manager.Add(s) {
		l.logger.Error("session ID collision", "id", id)
		_ = conn.Close()
		return
	}

	l.logger.Info("client connected", "session", id, "remote", remoteIP)

	if l.metrics != nil {
		l.metrics.RecordConnection(context.Background(), "tcp", remoteIP)
	}

	// Run the read loop; cleanup on exit.
	defer func() {
		l.manager.Remove(id)
		s.Close()
		if l.metrics != nil {
			l.metrics.RecordDisconnect(context.Background(), "tcp", remoteIP)
		}
		l.logger.Info("client disconnected", "session", id)
	}()

	readLoop(s, l.router, l.logger, l.cfg)
}
