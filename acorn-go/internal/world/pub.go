package world

import (
	"fmt"
	"log/slog"
	"os"
	"path/filepath"

	"github.com/ethanmoffat/eolib-go/v3/data"
	eomap "github.com/ethanmoffat/eolib-go/v3/protocol/map"
	"github.com/ethanmoffat/eolib-go/v3/protocol/pub"
)

// PubFiles holds the loaded EO pub data files.
type PubFiles struct {
	EIF *pub.Eif
	ENF *pub.Enf
	ESF *pub.Esf
	ECF *pub.Ecf

	// Raw file bytes for transfer to clients.
	EIFBytes []byte
	ENFBytes []byte
	ESFBytes []byte
	ECFBytes []byte
}

// LoadPubFiles loads all pub files from the given directory.
func LoadPubFiles(dir string, logger *slog.Logger) (*PubFiles, error) {
	p := &PubFiles{}
	var err error

	p.EIFBytes, err = os.ReadFile(filepath.Join(dir, "dat001.eif"))
	if err != nil {
		return nil, fmt.Errorf("reading EIF: %w", err)
	}
	p.EIF = &pub.Eif{}
	reader := data.NewEoReader(p.EIFBytes)
	if err := p.EIF.Deserialize(reader); err != nil {
		return nil, fmt.Errorf("parsing EIF: %w", err)
	}
	logger.Info("loaded EIF", "items", len(p.EIF.Items))

	p.ENFBytes, err = os.ReadFile(filepath.Join(dir, "dtn001.enf"))
	if err != nil {
		return nil, fmt.Errorf("reading ENF: %w", err)
	}
	p.ENF = &pub.Enf{}
	reader = data.NewEoReader(p.ENFBytes)
	if err := p.ENF.Deserialize(reader); err != nil {
		return nil, fmt.Errorf("parsing ENF: %w", err)
	}
	logger.Info("loaded ENF", "npcs", len(p.ENF.Npcs))

	p.ESFBytes, err = os.ReadFile(filepath.Join(dir, "dsl001.esf"))
	if err != nil {
		return nil, fmt.Errorf("reading ESF: %w", err)
	}
	p.ESF = &pub.Esf{}
	reader = data.NewEoReader(p.ESFBytes)
	if err := p.ESF.Deserialize(reader); err != nil {
		return nil, fmt.Errorf("parsing ESF: %w", err)
	}
	logger.Info("loaded ESF", "spells", len(p.ESF.Skills))

	p.ECFBytes, err = os.ReadFile(filepath.Join(dir, "dat001.ecf"))
	if err != nil {
		return nil, fmt.Errorf("reading ECF: %w", err)
	}
	p.ECF = &pub.Ecf{}
	reader = data.NewEoReader(p.ECFBytes)
	if err := p.ECF.Deserialize(reader); err != nil {
		return nil, fmt.Errorf("parsing ECF: %w", err)
	}
	logger.Info("loaded ECF", "classes", len(p.ECF.Classes))

	return p, nil
}

// MapFile holds a loaded EMF map and its raw bytes.
type MapFile struct {
	EMF      *eomap.Emf
	RawBytes []byte
}

// LoadMap loads a single map file by ID from the given directory.
func LoadMap(dir string, mapID int) (*MapFile, error) {
	filename := filepath.Join(dir, fmt.Sprintf("%05d.emf", mapID))

	rawBytes, err := os.ReadFile(filename)
	if err != nil {
		if os.IsNotExist(err) {
			return nil, nil // Map doesn't exist, not an error.
		}
		return nil, fmt.Errorf("reading map %d: %w", mapID, err)
	}

	emf := &eomap.Emf{}
	reader := data.NewEoReader(rawBytes)
	if err := emf.Deserialize(reader); err != nil {
		return nil, fmt.Errorf("parsing map %d: %w", mapID, err)
	}

	return &MapFile{EMF: emf, RawBytes: rawBytes}, nil
}

// LoadAllMaps loads all map files from 1 to maxMaps from the given directory.
func LoadAllMaps(dir string, maxMaps int, logger *slog.Logger) (map[int]*MapFile, error) {
	maps := make(map[int]*MapFile)
	loaded := 0

	for i := 1; i <= maxMaps; i++ {
		mf, err := LoadMap(dir, i)
		if err != nil {
			logger.Warn("failed to load map", "id", i, "error", err)
			continue
		}
		if mf != nil {
			maps[i] = mf
			loaded++
		}
	}

	logger.Info("maps loaded", "count", loaded, "max_id", maxMaps)
	return maps, nil
}
