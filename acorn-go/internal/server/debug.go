package server

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"net/http"
	"net/http/pprof"
	"runtime"
	"time"

	"github.com/prometheus/client_golang/prometheus/promhttp"
)

// DebugServer exposes /metrics, /healthz, and /debug/pprof/* on a dedicated
// port.  This is the observability entry-point — Prometheus scrapes /metrics,
// operators hit /healthz, and engineers profile via pprof.
type DebugServer struct {
	server *http.Server
	logger *slog.Logger
}

// healthResponse is the JSON body for /healthz.
type healthResponse struct {
	Status     string `json:"status"`
	Goroutines int    `json:"goroutines"`
	Uptime     string `json:"uptime"`
}

// NewDebugServer creates a new debug HTTP server.
func NewDebugServer(port int, logger *slog.Logger) *DebugServer {
	mux := http.NewServeMux()

	startTime := time.Now()

	// Prometheus metrics endpoint.
	mux.Handle("/metrics", promhttp.Handler())

	// Health check.
	mux.HandleFunc("/healthz", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		resp := healthResponse{
			Status:     "ok",
			Goroutines: runtime.NumGoroutine(),
			Uptime:     time.Since(startTime).Round(time.Second).String(),
		}
		_ = json.NewEncoder(w).Encode(resp)
	})

	// pprof endpoints.
	mux.HandleFunc("/debug/pprof/", pprof.Index)
	mux.HandleFunc("/debug/pprof/cmdline", pprof.Cmdline)
	mux.HandleFunc("/debug/pprof/profile", pprof.Profile)
	mux.HandleFunc("/debug/pprof/symbol", pprof.Symbol)
	mux.HandleFunc("/debug/pprof/trace", pprof.Trace)

	return &DebugServer{
		server: &http.Server{
			Addr:    fmt.Sprintf(":%d", port),
			Handler: mux,
		},
		logger: logger.With("component", "debug_server"),
	}
}

// Start begins listening.  Blocks until the server shuts down.
func (d *DebugServer) Start(done <-chan struct{}) error {
	// Shut down when the done channel is closed.
	go func() {
		<-done
		ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		_ = d.server.Shutdown(ctx)
	}()

	d.logger.Info("debug server listening", "addr", d.server.Addr)
	if err := d.server.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		return fmt.Errorf("debug server: %w", err)
	}
	return nil
}
