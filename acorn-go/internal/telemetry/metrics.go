package telemetry

import (
	"context"
	"log/slog"

	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/attribute"
	"go.opentelemetry.io/otel/metric"
)

const meterName = "acorn-go"

// Metrics holds all the OTel metric instruments for the server.
type Metrics struct {
	// --- Connection metrics ---
	ConnectionsActive  metric.Int64UpDownCounter
	ConnectionsTotal   metric.Int64Counter
	DisconnectsTotal   metric.Int64Counter
	ConnectionsPerIP   metric.Int64UpDownCounter

	// --- Packet metrics ---
	PacketsReceived    metric.Int64Counter
	PacketsSent        metric.Int64Counter
	PacketErrors       metric.Int64Counter
	PacketHandleTime   metric.Float64Histogram

	// --- Game state (async gauge callbacks registered separately) ---
	// These are registered as observable gauges in RegisterGameStateCallbacks.

	logger *slog.Logger
}

// NewMetrics creates and registers all metric instruments.
func NewMetrics(logger *slog.Logger) (*Metrics, error) {
	meter := otel.Meter(meterName)

	m := &Metrics{logger: logger}
	var err error

	// --- Connection metrics ---

	m.ConnectionsActive, err = meter.Int64UpDownCounter("acorn.connections.active",
		metric.WithDescription("Current number of active connections"),
		metric.WithUnit("{connections}"),
	)
	if err != nil {
		return nil, err
	}

	m.ConnectionsTotal, err = meter.Int64Counter("acorn.connections.total",
		metric.WithDescription("Total connections accepted since startup"),
		metric.WithUnit("{connections}"),
	)
	if err != nil {
		return nil, err
	}

	m.DisconnectsTotal, err = meter.Int64Counter("acorn.disconnects.total",
		metric.WithDescription("Total disconnections since startup"),
		metric.WithUnit("{connections}"),
	)
	if err != nil {
		return nil, err
	}

	m.ConnectionsPerIP, err = meter.Int64UpDownCounter("acorn.connections.per_ip",
		metric.WithDescription("Active connections from a given IP"),
		metric.WithUnit("{connections}"),
	)
	if err != nil {
		return nil, err
	}

	// --- Packet metrics ---

	m.PacketsReceived, err = meter.Int64Counter("acorn.packets.received",
		metric.WithDescription("Total packets received"),
		metric.WithUnit("{packets}"),
	)
	if err != nil {
		return nil, err
	}

	m.PacketsSent, err = meter.Int64Counter("acorn.packets.sent",
		metric.WithDescription("Total packets sent"),
		metric.WithUnit("{packets}"),
	)
	if err != nil {
		return nil, err
	}

	m.PacketErrors, err = meter.Int64Counter("acorn.packets.errors",
		metric.WithDescription("Total packet processing errors"),
		metric.WithUnit("{errors}"),
	)
	if err != nil {
		return nil, err
	}

	m.PacketHandleTime, err = meter.Float64Histogram("acorn.packets.handle_duration",
		metric.WithDescription("Time spent handling a packet"),
		metric.WithUnit("ms"),
		metric.WithExplicitBucketBoundaries(0.1, 0.5, 1, 2, 5, 10, 25, 50, 100),
	)
	if err != nil {
		return nil, err
	}

	return m, nil
}

// GameStateProvider is the interface that the world/session layer must implement
// to feed observable gauge callbacks.
type GameStateProvider interface {
	OnlinePlayerCount() int
	MapPlayerCounts() map[int]int // mapID -> player count
	AliveNPCCount() int
	GroundItemCount() int
}

// RegisterGameStateCallbacks registers asynchronous gauge callbacks that read
// live game state on each Prometheus scrape.
func RegisterGameStateCallbacks(provider GameStateProvider, logger *slog.Logger) error {
	meter := otel.Meter(meterName)

	_, err := meter.Int64ObservableGauge("acorn.players.online",
		metric.WithDescription("Number of players currently in-game"),
		metric.WithUnit("{players}"),
		metric.WithInt64Callback(func(_ context.Context, o metric.Int64Observer) error {
			o.Observe(int64(provider.OnlinePlayerCount()))
			return nil
		}),
	)
	if err != nil {
		return err
	}

	_, err = meter.Int64ObservableGauge("acorn.players.per_map",
		metric.WithDescription("Number of players per map"),
		metric.WithUnit("{players}"),
		metric.WithInt64Callback(func(_ context.Context, o metric.Int64Observer) error {
			for mapID, count := range provider.MapPlayerCounts() {
				o.Observe(int64(count), metric.WithAttributes(
					attribute.Int("map_id", mapID),
				))
			}
			return nil
		}),
	)
	if err != nil {
		return err
	}

	_, err = meter.Int64ObservableGauge("acorn.npcs.alive",
		metric.WithDescription("Total alive NPCs across all maps"),
		metric.WithUnit("{npcs}"),
		metric.WithInt64Callback(func(_ context.Context, o metric.Int64Observer) error {
			o.Observe(int64(provider.AliveNPCCount()))
			return nil
		}),
	)
	if err != nil {
		return err
	}

	_, err = meter.Int64ObservableGauge("acorn.items.ground",
		metric.WithDescription("Total items on the ground across all maps"),
		metric.WithUnit("{items}"),
		metric.WithInt64Callback(func(_ context.Context, o metric.Int64Observer) error {
			o.Observe(int64(provider.GroundItemCount()))
			return nil
		}),
	)
	if err != nil {
		return err
	}

	logger.Info("game state metric callbacks registered")
	return nil
}

// --- Convenience recording helpers ---

// RecordConnection increments connection counters.
func (m *Metrics) RecordConnection(ctx context.Context, transport, ip string) {
	attrs := metric.WithAttributes(attribute.String("transport", transport))
	m.ConnectionsActive.Add(ctx, 1, attrs)
	m.ConnectionsTotal.Add(ctx, 1, attrs)
	m.ConnectionsPerIP.Add(ctx, 1, metric.WithAttributes(attribute.String("ip", ip)))
}

// RecordDisconnect decrements active connections and increments disconnects.
func (m *Metrics) RecordDisconnect(ctx context.Context, transport, ip string) {
	attrs := metric.WithAttributes(attribute.String("transport", transport))
	m.ConnectionsActive.Add(ctx, -1, attrs)
	m.DisconnectsTotal.Add(ctx, 1, attrs)
	m.ConnectionsPerIP.Add(ctx, -1, metric.WithAttributes(attribute.String("ip", ip)))
}

// RecordPacketReceived increments the received packet counter.
func (m *Metrics) RecordPacketReceived(ctx context.Context, family, action string) {
	m.PacketsReceived.Add(ctx, 1, metric.WithAttributes(
		attribute.String("family", family),
		attribute.String("action", action),
	))
}

// RecordPacketSent increments the sent packet counter.
func (m *Metrics) RecordPacketSent(ctx context.Context) {
	m.PacketsSent.Add(ctx, 1)
}

// RecordPacketError increments the packet error counter.
func (m *Metrics) RecordPacketError(ctx context.Context, family, action string) {
	m.PacketErrors.Add(ctx, 1, metric.WithAttributes(
		attribute.String("family", family),
		attribute.String("action", action),
	))
}

// RecordPacketHandleTime records the duration of handling a single packet.
func (m *Metrics) RecordPacketHandleTime(ctx context.Context, family, action string, ms float64) {
	m.PacketHandleTime.Record(ctx, ms, metric.WithAttributes(
		attribute.String("family", family),
		attribute.String("action", action),
	))
}
