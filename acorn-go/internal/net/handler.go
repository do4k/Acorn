package net

import (
	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net"
)

// Handler processes a single type of client packet.
type Handler interface {
	// Handle processes the packet for the given session.
	Handle(s *session.Session, pkt net.Packet) error
}

// HandlerFunc is a convenience adapter to use ordinary functions as Handlers.
type HandlerFunc func(s *session.Session, pkt net.Packet) error

func (f HandlerFunc) Handle(s *session.Session, pkt net.Packet) error {
	return f(s, pkt)
}
