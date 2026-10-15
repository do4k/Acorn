package config

import (
	"fmt"
	"os"

	"gopkg.in/yaml.v3"
)

// Config holds the top-level server configuration.
type Config struct {
	Server    ServerConfig    `yaml:"server"`
	Database  DatabaseConfig  `yaml:"database"`
	Data      DataConfig      `yaml:"data"`
	Telemetry TelemetryConfig `yaml:"telemetry"`
}

// ServerConfig holds network and gameplay settings.
type ServerConfig struct {
	// TCPPort is the port the server listens on for raw TCP connections.
	TCPPort int `yaml:"tcp_port"`
	// WSPort is the port the server listens on for WebSocket connections.
	WSPort int `yaml:"ws_port"`
	// MaxConnections is the maximum number of concurrent connections.
	MaxConnections int `yaml:"max_connections"`
	// MaxConnectionsPerIP limits connections from a single IP address.
	MaxConnectionsPerIP int `yaml:"max_connections_per_ip"`
	// PingInterval is the number of seconds between keep-alive pings.
	PingInterval int `yaml:"ping_interval"`
	// EnforceSequence disconnects clients that send out-of-order packets.
	EnforceSequence bool `yaml:"enforce_sequence"`
	// LogPackets enables verbose packet logging.
	LogPackets bool `yaml:"log_packets"`
	// MaxCharacters is the maximum number of characters per account.
	MaxCharacters int `yaml:"max_characters"`
	// JailMap is the map ID for the jail (sent in ServerSettings).
	JailMap int `yaml:"jail_map"`
	// RescueMap is the default spawn/rescue map ID.
	RescueMap int `yaml:"rescue_map"`
	// RescueX is the default spawn X coordinate.
	RescueX int `yaml:"rescue_x"`
	// RescueY is the default spawn Y coordinate.
	RescueY int `yaml:"rescue_y"`
	// NewsFile is the path to the server news/MOTD file.
	NewsFile string `yaml:"news_file"`
}

// DatabaseConfig holds persistence settings.
type DatabaseConfig struct {
	// Engine is the database engine to use (sqlite).
	Engine string `yaml:"engine"`
	// ConnectionString is the DSN for the database connection.
	ConnectionString string `yaml:"connection_string"`
}

// DataConfig holds file paths for game data.
type DataConfig struct {
	// PubDir is the directory containing pub files (EIF, ENF, ESF, ECF).
	PubDir string `yaml:"pub_dir"`
	// MapDir is the directory containing map files (EMF).
	MapDir string `yaml:"map_dir"`
	// MaxMaps is the highest map ID to attempt loading.
	MaxMaps int `yaml:"max_maps"`
}

// TelemetryConfig holds observability settings.
type TelemetryConfig struct {
	// Enabled turns telemetry on/off globally.
	Enabled bool `yaml:"enabled"`
	// OTLPEndpoint is the OTLP/HTTP endpoint for traces (e.g. "localhost:4318").
	OTLPEndpoint string `yaml:"otlp_endpoint"`
	// DebugPort is the port for the debug HTTP server (/metrics, /healthz, pprof).
	DebugPort int `yaml:"debug_port"`
}

// Default returns a Config with sensible defaults matching eoserv conventions.
func Default() *Config {
	return &Config{
		Server: ServerConfig{
			TCPPort:             8078,
			WSPort:              8079,
			MaxConnections:      300,
			MaxConnectionsPerIP: 3,
			PingInterval:        8,
			EnforceSequence:     false,
			LogPackets:          false,
			MaxCharacters:       3,
			JailMap:             76,
			RescueMap:           192,
			RescueX:             6,
			RescueY:             6,
			NewsFile:            "data/news.txt",
		},
		Database: DatabaseConfig{
			Engine:           "sqlite",
			ConnectionString: "file:acorn.db?_journal_mode=WAL&_busy_timeout=5000",
		},
		Data: DataConfig{
			PubDir:  "data/pub",
			MapDir:  "data/maps",
			MaxMaps: 300,
		},
		Telemetry: TelemetryConfig{
			Enabled:      true,
			OTLPEndpoint: "localhost:4318",
			DebugPort:    6060,
		},
	}
}

// Load reads a YAML configuration file, applying defaults for any missing values.
// Environment variables override YAML values when set:
//   - ACORN_OTLP_ENDPOINT overrides telemetry.otlp_endpoint
func Load(path string) (*Config, error) {
	cfg := Default()

	data, err := os.ReadFile(path)
	if err != nil {
		if os.IsNotExist(err) {
			return cfg, nil
		}
		return nil, fmt.Errorf("reading config %s: %w", path, err)
	}

	if err := yaml.Unmarshal(data, cfg); err != nil {
		return nil, fmt.Errorf("parsing config %s: %w", path, err)
	}

	// Environment variable overrides.
	if v := os.Getenv("ACORN_OTLP_ENDPOINT"); v != "" {
		cfg.Telemetry.OTLPEndpoint = v
	}

	return cfg, nil
}
