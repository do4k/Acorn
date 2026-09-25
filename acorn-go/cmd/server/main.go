package main

import (
	"context"
	"flag"
	"fmt"
	"log/slog"
	"os"
	"os/signal"
	"syscall"
	"time"

	"github.com/acorn-server/acorn-go/internal/config"
	"github.com/acorn-server/acorn-go/internal/database"
	"github.com/acorn-server/acorn-go/internal/server"
	"github.com/acorn-server/acorn-go/internal/telemetry"
	"github.com/acorn-server/acorn-go/internal/world"
)

func main() {
	configPath := flag.String("config", "config.yaml", "path to configuration file")
	flag.Parse()

	// Set up structured logging.
	logger := slog.New(slog.NewTextHandler(os.Stdout, &slog.HandlerOptions{
		Level: slog.LevelDebug,
	}))
	slog.SetDefault(logger)

	logger.Info("acorn-go starting", "config", *configPath)

	// Load configuration.
	cfg, err := config.Load(*configPath)
	if err != nil {
		logger.Error("failed to load config", "error", err)
		os.Exit(1)
	}

	logger.Info("configuration loaded",
		"tcp_port", cfg.Server.TCPPort,
		"ws_port", cfg.Server.WSPort,
		"db_engine", cfg.Database.Engine,
		"pub_dir", cfg.Data.PubDir,
		"map_dir", cfg.Data.MapDir,
	)

	// Initialize OpenTelemetry (tracing + metrics).
	ctx := context.Background()
	teleCfg := telemetry.Config{
		Enabled:      cfg.Telemetry.Enabled,
		OTLPEndpoint: cfg.Telemetry.OTLPEndpoint,
		DebugPort:    cfg.Telemetry.DebugPort,
	}
	tp, err := telemetry.Init(ctx, teleCfg, logger.With("component", "telemetry"))
	if err != nil {
		logger.Error("failed to initialize telemetry", "error", err)
		os.Exit(1)
	}
	defer func() {
		shutdownCtx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		tp.Shutdown(shutdownCtx)
	}()

	// Create metrics instruments.
	var metrics *telemetry.Metrics
	if cfg.Telemetry.Enabled {
		metrics, err = telemetry.NewMetrics(logger.With("component", "metrics"))
		if err != nil {
			logger.Error("failed to create metrics", "error", err)
			os.Exit(1)
		}
	}

	// Open the database.
	db, err := database.Open(cfg.Database.ConnectionString, logger.With("component", "database"))
	if err != nil {
		logger.Error("failed to open database", "error", err)
		os.Exit(1)
	}
	defer db.Close()

	logger.Info("database connected", "engine", cfg.Database.Engine)

	// Load the game world (pub files + maps).
	w, err := world.New(cfg.Data.PubDir, cfg.Data.MapDir, cfg.Data.MaxMaps, logger)
	if err != nil {
		logger.Error("failed to load world data", "error", err)
		os.Exit(1)
	}

	// Register game state gauge callbacks for telemetry.
	if cfg.Telemetry.Enabled {
		if err := telemetry.RegisterGameStateCallbacks(w, logger.With("component", "metrics")); err != nil {
			logger.Error("failed to register game state callbacks", "error", err)
			os.Exit(1)
		}
	}

	// Create and start the server.
	srv := server.New(cfg, db, w, metrics, logger)

	// Handle OS signals for graceful shutdown.
	sigCh := make(chan os.Signal, 1)
	signal.Notify(sigCh, syscall.SIGINT, syscall.SIGTERM)

	go func() {
		sig := <-sigCh
		logger.Info("received signal, shutting down", "signal", sig)
		srv.Stop()
	}()

	fmt.Print(`
   _                          
  /_\   ___ ___  _ __ _ __    
 //_\\ / __/ _ \| '__| '_ \   
/  _  \ (_| (_) | |  | | | |  
\_/ \_/\___\___/|_|  |_| |_|  
                               
  Endless Online Server (Go)
`)
	fmt.Println()

	if err := srv.Start(); err != nil {
		logger.Error("server error", "error", err)
		os.Exit(1)
	}

	logger.Info("server stopped")
}
