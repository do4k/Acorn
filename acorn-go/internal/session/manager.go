package session

import (
	"log/slog"
	"math/rand/v2"
	"strings"
	"sync"
	"sync/atomic"

	"github.com/ethanmoffat/eolib-go/v3/data"
)

// Manager tracks all active sessions and handles ID generation.
// It is safe for concurrent use.
type Manager struct {
	mu       sync.RWMutex
	sessions map[int]*Session
	logger   *slog.Logger

	nextID atomic.Int64

	// Per-IP connection tracking for rate limiting.
	ipCount map[string]int
}

// NewManager creates a new session manager.
func NewManager(logger *slog.Logger) *Manager {
	return &Manager{
		sessions: make(map[int]*Session),
		ipCount:  make(map[string]int),
		logger:   logger,
	}
}

// GenerateID creates a unique session ID in the range [8, SHORT_MAX).
// The lower bound avoids collisions with protocol reply codes (1-7).
func (m *Manager) GenerateID() int {
	for {
		id := rand.IntN(data.SHORT_MAX-8) + 8
		m.mu.RLock()
		_, exists := m.sessions[id]
		m.mu.RUnlock()
		if !exists {
			return id
		}
	}
}

// Add registers a session. Returns false if the session ID is already taken.
func (m *Manager) Add(s *Session) bool {
	m.mu.Lock()
	defer m.mu.Unlock()

	if _, exists := m.sessions[s.ID()]; exists {
		return false
	}
	m.sessions[s.ID()] = s
	m.ipCount[s.RemoteIP()]++
	return true
}

// Remove unregisters a session.
func (m *Manager) Remove(id int) {
	m.mu.Lock()
	defer m.mu.Unlock()

	if s, ok := m.sessions[id]; ok {
		ip := s.RemoteIP()
		m.ipCount[ip]--
		if m.ipCount[ip] <= 0 {
			delete(m.ipCount, ip)
		}
		delete(m.sessions, id)
	}
}

// Get returns a session by ID, or nil if not found.
func (m *Manager) Get(id int) *Session {
	m.mu.RLock()
	defer m.mu.RUnlock()
	return m.sessions[id]
}

// Count returns the total number of active sessions.
func (m *Manager) Count() int {
	m.mu.RLock()
	defer m.mu.RUnlock()
	return len(m.sessions)
}

// CountByIP returns how many sessions are connected from the given IP.
func (m *Manager) CountByIP(ip string) int {
	m.mu.RLock()
	defer m.mu.RUnlock()
	return m.ipCount[ip]
}

// All returns a snapshot of all active sessions.
func (m *Manager) All() []*Session {
	m.mu.RLock()
	defer m.mu.RUnlock()

	result := make([]*Session, 0, len(m.sessions))
	for _, s := range m.sessions {
		result = append(result, s)
	}
	return result
}

// FindByCharacterName returns the session for the character with the given name,
// or nil if no such character is online. Name comparison is case-insensitive.
func (m *Manager) FindByCharacterName(name string) *Session {
	m.mu.RLock()
	defer m.mu.RUnlock()

	lower := strings.ToLower(name)
	for _, s := range m.sessions {
		if s.Character != nil && strings.ToLower(s.Character.Name) == lower {
			return s
		}
	}
	return nil
}
