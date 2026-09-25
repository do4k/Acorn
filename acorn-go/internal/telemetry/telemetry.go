// Package telemetry initialises OpenTelemetry tracing and metrics for the
// acorn-go server.  Traces are exported via OTLP/HTTP (to Tempo or any
// OTLP-compatible backend).  Metrics are exposed via a Prometheus /metrics
// endpoint.
package telemetry

import (
	"context"
	"fmt"
	"log/slog"
	"time"

	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracehttp"
	"go.opentelemetry.io/otel/exporters/prometheus"
	"go.opentelemetry.io/otel/propagation"
	sdkmetric "go.opentelemetry.io/otel/sdk/metric"
	"go.opentelemetry.io/otel/sdk/resource"
	sdktrace "go.opentelemetry.io/otel/sdk/trace"
	semconv "go.opentelemetry.io/otel/semconv/v1.26.0"
)

const (
	serviceName    = "acorn-go"
	serviceVersion = "0.1.0"
)

// Provider bundles the trace and metric providers so the caller can shut them
// down cleanly.
type Provider struct {
	Tracer   *sdktrace.TracerProvider
	Meter    *sdkmetric.MeterProvider
	PromExporter *prometheus.Exporter
	logger   *slog.Logger
}

// Config holds telemetry-specific configuration.
type Config struct {
	// Enabled turns telemetry on/off globally.
	Enabled bool `yaml:"enabled"`
	// OTLPEndpoint is the OTLP/HTTP endpoint for traces (e.g. "localhost:4318").
	// Leave empty to disable trace export.
	OTLPEndpoint string `yaml:"otlp_endpoint"`
	// DebugPort is the port for the debug HTTP server (/metrics, /healthz, pprof).
	DebugPort int `yaml:"debug_port"`
}

// DefaultConfig returns telemetry defaults suitable for local development.
func DefaultConfig() Config {
	return Config{
		Enabled:      true,
		OTLPEndpoint: "localhost:4318",
		DebugPort:    6060,
	}
}

// Init creates and registers the global OTel trace and metric providers.
// The returned Provider must be shut down when the server exits.
func Init(ctx context.Context, cfg Config, logger *slog.Logger) (*Provider, error) {
	if !cfg.Enabled {
		logger.Info("telemetry disabled")
		return &Provider{logger: logger}, nil
	}

	res, err := resource.New(ctx,
		resource.WithAttributes(
			semconv.ServiceName(serviceName),
			semconv.ServiceVersion(serviceVersion),
		),
		resource.WithProcessRuntimeDescription(),
		resource.WithHost(),
	)
	if err != nil {
		return nil, fmt.Errorf("creating OTel resource: %w", err)
	}

	// --- Traces ---
	var tp *sdktrace.TracerProvider
	if cfg.OTLPEndpoint != "" {
		traceExporter, err := otlptracehttp.New(ctx,
			otlptracehttp.WithEndpoint(cfg.OTLPEndpoint),
			otlptracehttp.WithInsecure(),
		)
		if err != nil {
			return nil, fmt.Errorf("creating OTLP trace exporter: %w", err)
		}

		tp = sdktrace.NewTracerProvider(
			sdktrace.WithBatcher(traceExporter, sdktrace.WithBatchTimeout(5*time.Second)),
			sdktrace.WithResource(res),
			sdktrace.WithSampler(sdktrace.AlwaysSample()),
		)
		otel.SetTracerProvider(tp)
		logger.Info("OTel trace provider initialized", "endpoint", cfg.OTLPEndpoint)
	}

	otel.SetTextMapPropagator(propagation.NewCompositeTextMapPropagator(
		propagation.TraceContext{},
		propagation.Baggage{},
	))

	// --- Metrics (Prometheus) ---
	promExporter, err := prometheus.New()
	if err != nil {
		return nil, fmt.Errorf("creating Prometheus exporter: %w", err)
	}

	mp := sdkmetric.NewMeterProvider(
		sdkmetric.WithResource(res),
		sdkmetric.WithReader(promExporter),
	)
	otel.SetMeterProvider(mp)
	logger.Info("OTel metric provider initialized (Prometheus)")

	return &Provider{
		Tracer:       tp,
		Meter:        mp,
		PromExporter: promExporter,
		logger:       logger,
	}, nil
}

// Shutdown flushes and shuts down all providers. Call from main on exit.
func (p *Provider) Shutdown(ctx context.Context) {
	if p.Tracer != nil {
		if err := p.Tracer.Shutdown(ctx); err != nil {
			p.logger.Warn("trace provider shutdown error", "error", err)
		}
	}
	if p.Meter != nil {
		if err := p.Meter.Shutdown(ctx); err != nil {
			p.logger.Warn("metric provider shutdown error", "error", err)
		}
	}
}
