package server

import (
	"log/slog"
	"time"

	"github.com/acorn-server/acorn-go/internal/config"
	"github.com/acorn-server/acorn-go/internal/database"
	"github.com/acorn-server/acorn-go/internal/handler"
	acornnet "github.com/acorn-server/acorn-go/internal/net"
	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/acorn-server/acorn-go/internal/telemetry"
	"github.com/acorn-server/acorn-go/internal/world"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
)

// Server is the top-level game server. It owns the session manager, packet router,
// transport listeners, database, and world state.
type Server struct {
	cfg     *config.Config
	manager *session.Manager
	router  *acornnet.Router
	db      *database.DB
	world   *world.World
	metrics *telemetry.Metrics
	logger  *slog.Logger

	tcp   *TCPListener
	ws    *WSListener
	debug *DebugServer
	ping  *handler.PingService

	done chan struct{}
}

// New creates a new Server with all subsystems wired together.
func New(cfg *config.Config, db *database.DB, w *world.World, metrics *telemetry.Metrics, logger *slog.Logger) *Server {
	mgr := session.NewManager(logger.With("component", "session_manager"))
	router := acornnet.NewRouter(logger.With("component", "router"))

	// Attach metrics to the router for per-packet instrumentation.
	if metrics != nil {
		router.SetMetrics(metrics)
	}

	s := &Server{
		cfg:     cfg,
		manager: mgr,
		router:  router,
		db:      db,
		world:   w,
		metrics: metrics,
		logger:  logger,
		done:    make(chan struct{}),
	}

	// Register packet handlers.
	s.registerHandlers()

	// Create transport listeners (pass metrics for connection tracking).
	s.tcp = NewTCPListener(cfg, mgr, router, metrics, logger)
	s.ws = NewWSListener(cfg, mgr, router, metrics, logger)

	// Create the debug HTTP server (pprof, /healthz, /metrics).
	s.debug = NewDebugServer(cfg.Telemetry.DebugPort, logger)

	// Create the ping service.
	s.ping = handler.NewPingService(
		mgr,
		time.Duration(cfg.Server.PingInterval)*time.Second,
		logger.With("component", "ping"),
	)

	return s
}

// registerHandlers wires up all packet handlers to the router.
// Handler registration follows eoserv's packet family/action mapping.
func (s *Server) registerHandlers() {
	// === Init handshake ===
	// Init_Init (family=255, action=255): First packet from client.
	s.router.Register(eonet.PacketFamily_Init, eonet.PacketAction_Init,
		&handler.InitInit{})

	// === Connection ===
	// Connection_Accept (family=1, action=2): Client confirms handshake.
	s.router.Register(eonet.PacketFamily_Connection, eonet.PacketAction_Accept,
		&handler.ConnectionAccept{})
	// Connection_Ping (family=1, action=240): Client pong response.
	s.router.Register(eonet.PacketFamily_Connection, eonet.PacketAction_Ping,
		&handler.ConnectionPing{})

	// === Account ===
	// Account_Request (family=2, action=1): Check username availability.
	s.router.Register(eonet.PacketFamily_Account, eonet.PacketAction_Request,
		&handler.AccountRequest{DB: s.db})
	// Account_Create (family=2, action=6): Create account.
	s.router.Register(eonet.PacketFamily_Account, eonet.PacketAction_Create,
		&handler.AccountCreate{DB: s.db})

	// === Login ===
	// Login_Request (family=4, action=1): Login with username/password.
	s.router.Register(eonet.PacketFamily_Login, eonet.PacketAction_Request,
		&handler.LoginRequest{DB: s.db, Manager: s.manager})

	// === Character ===
	// Character_Request (family=3, action=1): Request to create character.
	s.router.Register(eonet.PacketFamily_Character, eonet.PacketAction_Request,
		&handler.CharacterRequest{})
	// Character_Create (family=3, action=6): Create character.
	s.router.Register(eonet.PacketFamily_Character, eonet.PacketAction_Create,
		&handler.CharacterCreate{DB: s.db, Cfg: s.cfg})
	// Character_Take (family=3, action=9): Request to delete character.
	s.router.Register(eonet.PacketFamily_Character, eonet.PacketAction_Take,
		&handler.CharacterTake{DB: s.db})
	// Character_Remove (family=3, action=4): Confirm character deletion.
	s.router.Register(eonet.PacketFamily_Character, eonet.PacketAction_Remove,
		&handler.CharacterRemove{DB: s.db})

	// === Welcome (enter game) ===
	// Welcome_Request (family=5, action=1): Select character.
	s.router.Register(eonet.PacketFamily_Welcome, eonet.PacketAction_Request,
		&handler.WelcomeRequest{DB: s.db, World: s.world, Cfg: s.cfg})
	// Welcome_Msg (family=5, action=15): Enter game.
	s.router.Register(eonet.PacketFamily_Welcome, eonet.PacketAction_Msg,
		&handler.WelcomeMsg{DB: s.db, World: s.world, Cfg: s.cfg})
	// Welcome_Agree (family=5, action=5): Request pub/map file.
	s.router.Register(eonet.PacketFamily_Welcome, eonet.PacketAction_Agree,
		&handler.WelcomeAgree{World: s.world})

	// === Walk (movement) ===
	// Walk_Player: Normal walking.
	s.router.Register(eonet.PacketFamily_Walk, eonet.PacketAction_Player,
		&handler.WalkPlayer{World: s.world})
	// Walk_Spec: Walk through ghost players.
	s.router.Register(eonet.PacketFamily_Walk, eonet.PacketAction_Spec,
		&handler.WalkSpec{World: s.world})
	// Walk_Admin: Admin walk through walls (#nowall).
	s.router.Register(eonet.PacketFamily_Walk, eonet.PacketAction_Admin,
		&handler.WalkAdmin{World: s.world})

	// === Face (direction change) ===
	// Face_Player: Change facing direction.
	s.router.Register(eonet.PacketFamily_Face, eonet.PacketAction_Player,
		&handler.FacePlayer{World: s.world})

	// === Sit / Chair ===
	// Sit_Request: Sit on floor / stand from floor.
	s.router.Register(eonet.PacketFamily_Sit, eonet.PacketAction_Request,
		&handler.SitRequest{World: s.world})
	// Chair_Request: Sit on chair / stand from chair.
	s.router.Register(eonet.PacketFamily_Chair, eonet.PacketAction_Request,
		&handler.ChairRequest{World: s.world})

	// === Talk (chat) ===
	// Talk_Report: Public/map chat.
	s.router.Register(eonet.PacketFamily_Talk, eonet.PacketAction_Report,
		&handler.TalkReport{World: s.world, Cfg: s.cfg})
	// Talk_Tell: Private message (whisper).
	s.router.Register(eonet.PacketFamily_Talk, eonet.PacketAction_Tell,
		&handler.TalkTell{Manager: s.manager})
	// Talk_Msg: Global chat.
	s.router.Register(eonet.PacketFamily_Talk, eonet.PacketAction_Msg,
		&handler.TalkMsg{World: s.world, Cfg: s.cfg})
	// Talk_Admin: Admin-only chat.
	s.router.Register(eonet.PacketFamily_Talk, eonet.PacketAction_Admin,
		&handler.TalkAdmin{World: s.world})
	// Talk_Announce: Server-wide announcement (admin only).
	s.router.Register(eonet.PacketFamily_Talk, eonet.PacketAction_Announce,
		&handler.TalkAnnounce{World: s.world})

	// === Warp ===
	// Warp_Accept: Client acknowledges server warp.
	s.router.Register(eonet.PacketFamily_Warp, eonet.PacketAction_Accept,
		&handler.WarpAccept{World: s.world})
	// Warp_Take: Client requests map file during warp.
	s.router.Register(eonet.PacketFamily_Warp, eonet.PacketAction_Take,
		&handler.WarpTake{World: s.world})
}

// Start launches the TCP listener, WebSocket listener, debug server, world tick loop,
// and ping service. It blocks until Stop is called.
func (s *Server) Start() error {
	s.logger.Info("starting server",
		"tcp_port", s.cfg.Server.TCPPort,
		"ws_port", s.cfg.Server.WSPort,
		"debug_port", s.cfg.Telemetry.DebugPort,
	)

	errCh := make(chan error, 3)

	// Start TCP listener.
	go func() {
		if err := s.tcp.Start(s.done); err != nil {
			errCh <- err
		}
	}()

	// Start WebSocket listener.
	go func() {
		if err := s.ws.Start(s.done); err != nil {
			errCh <- err
		}
	}()

	// Start the debug HTTP server (pprof, /healthz, /metrics).
	go func() {
		if err := s.debug.Start(s.done); err != nil {
			errCh <- err
		}
	}()

	// Start the world tick loop.
	go s.world.Run(s.done)

	// Start the ping service.
	go s.ping.Run(s.done)

	// Wait for an error or shutdown signal.
	select {
	case err := <-errCh:
		return err
	case <-s.done:
		return nil
	}
}

// Stop signals all goroutines to shut down.
func (s *Server) Stop() {
	s.logger.Info("stopping server")
	close(s.done)

	// Save all characters and close sessions.
	for _, sess := range s.manager.All() {
		if sess.Character != nil {
			if err := s.db.SaveCharacter(sess.Context(), sess.Character); err != nil {
				s.logger.Warn("failed to save character on shutdown",
					"session", sess.ID(),
					"error", err,
				)
			}
		}
		sess.Close()
	}
}
