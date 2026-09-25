package net

import (
	"context"
	"fmt"
	"log/slog"
	"sync"
	"time"

	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/acorn-server/acorn-go/internal/telemetry"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/attribute"
	"go.opentelemetry.io/otel/trace"
)

var tracer = otel.Tracer("acorn-go/router")

// Router dispatches decoded packets to registered handlers based on
// the (family, action) pair. It is safe for concurrent use.
type Router struct {
	mu       sync.RWMutex
	handlers map[int]Handler
	logger   *slog.Logger
	metrics  *telemetry.Metrics // nil if telemetry is disabled
}

// NewRouter creates a new packet router.
func NewRouter(logger *slog.Logger) *Router {
	return &Router{
		handlers: make(map[int]Handler),
		logger:   logger,
	}
}

// SetMetrics attaches telemetry metrics to the router. Must be called before
// any packets are processed.
func (r *Router) SetMetrics(m *telemetry.Metrics) {
	r.metrics = m
}

// packetKey computes a unique key from the family and action, matching
// the convention in eolib-go: (action << 8) | family.
func packetKey(family net.PacketFamily, action net.PacketAction) int {
	return (int(action) << 8) | int(family)
}

// Register adds a handler for a specific (family, action) pair.
// It panics if a handler is already registered for that pair, catching
// configuration errors at startup.
func (r *Router) Register(family net.PacketFamily, action net.PacketAction, h Handler) {
	r.mu.Lock()
	defer r.mu.Unlock()

	key := packetKey(family, action)
	if _, exists := r.handlers[key]; exists {
		panic(fmt.Sprintf("duplicate handler for family=%d action=%d", family, action))
	}
	r.handlers[key] = h
}

// RegisterFunc is a convenience wrapper around Register for function handlers.
func (r *Router) RegisterFunc(family net.PacketFamily, action net.PacketAction, fn HandlerFunc) {
	r.Register(family, action, fn)
}

// Route dispatches a decoded packet to the appropriate handler.
// It creates an OTel span around the handler call and records metrics.
func (r *Router) Route(s *session.Session, family net.PacketFamily, action net.PacketAction, pkt net.Packet) error {
	r.mu.RLock()
	key := packetKey(family, action)
	h, ok := r.handlers[key]
	r.mu.RUnlock()

	familyStr := fmt.Sprintf("%d", family)
	actionStr := fmt.Sprintf("%d", action)
	if s, err := family.String(); err == nil {
		familyStr = s
	}
	if s, err := action.String(); err == nil {
		actionStr = s
	}

	// Record packet received metric.
	if r.metrics != nil {
		r.metrics.RecordPacketReceived(context.Background(), familyStr, actionStr)
	}

	if !ok {
		r.logger.Warn("no handler registered",
			"family", family,
			"action", action,
			"session", s.ID(),
		)
		return nil // Unhandled packets are silently dropped, matching eoserv behaviour.
	}

	// Create a trace span around the handler.
	spanName := fmt.Sprintf("packet/%s_%s", familyStr, actionStr)
	ctx, span := tracer.Start(context.Background(), spanName,
		trace.WithAttributes(
			attribute.Int("session.id", s.ID()),
			attribute.String("packet.family", familyStr),
			attribute.String("packet.action", actionStr),
		),
		trace.WithSpanKind(trace.SpanKindServer),
	)
	defer span.End()

	start := time.Now()

	err := h.Handle(s, pkt)

	elapsed := float64(time.Since(start).Microseconds()) / 1000.0 // ms

	// Record handler duration.
	if r.metrics != nil {
		r.metrics.RecordPacketHandleTime(ctx, familyStr, actionStr, elapsed)
	}

	if err != nil {
		span.RecordError(err)
		if r.metrics != nil {
			r.metrics.RecordPacketError(ctx, familyStr, actionStr)
		}
	}

	return err
}
