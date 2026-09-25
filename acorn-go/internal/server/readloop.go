package server

import (
	"log/slog"

	"github.com/acorn-server/acorn-go/internal/config"
	acornnet "github.com/acorn-server/acorn-go/internal/net"
	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/ethanmoffat/eolib-go/v3/data"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
)

// readLoop is the shared read loop for both TCP and WebSocket connections.
// It reads packets, handles sequencing, decodes them, and dispatches to the router.
func readLoop(s *session.Session, router *acornnet.Router, logger *slog.Logger, cfg *config.Config) {
	for {
		select {
		case <-s.Context().Done():
			return
		default:
		}

		// Read a raw (decrypted) packet.
		body, err := s.ReadRawPacket()
		if err != nil {
			// Normal disconnection — don't log at error level.
			select {
			case <-s.Context().Done():
			default:
				s.Logger().Info("read error (disconnecting)", "error", err)
			}
			return
		}

		if len(body) < 2 {
			s.Logger().Warn("packet too short", "len", len(body))
			continue
		}

		// Peek at action/family before full decode (needed for sequence handling).
		action := eonet.PacketAction(body[0])
		family := eonet.PacketFamily(body[1])

		if cfg.Server.LogPackets {
			s.Logger().Debug("packet received",
				"family", family,
				"action", action,
				"len", len(body),
			)
		}

		// Handle sequence validation and stripping.
		// Init packets don't carry a sequence byte; sequence is skipped for uninitialized clients.
		isInit := family == eonet.PacketFamily_Init && action == eonet.PacketAction_Init
		if s.State() != session.StateUninitialized && !isInit {
			// The sequence byte(s) follow the action+family bytes.
			// We need to validate, advance the sequencer, and strip the
			// sequence bytes so that Deserialize sees clean packet data.
			expectedSeq := s.Sequencer().NextSequence()

			// Determine sequence field width: char (1 byte) or short (2 bytes).
			seqLen := 1
			if expectedSeq >= data.CHAR_MAX {
				seqLen = 2
			}

			if len(body) > 2 {
				seqReader := data.NewEoReader(body[2:])
				var clientSeq int
				if seqLen == 2 {
					clientSeq = seqReader.GetShort()
				} else {
					clientSeq = seqReader.GetChar()
				}

				if cfg.Server.EnforceSequence && clientSeq != expectedSeq {
					s.Logger().Warn("sequence mismatch",
						"expected", expectedSeq,
						"got", clientSeq,
						"family", family,
						"action", action,
					)
					return // Disconnect on sequence mismatch.
				}

				// Strip the sequence bytes from body so handlers get:
				// [action][family][payload...] without the sequence field.
				newBody := make([]byte, len(body)-seqLen)
				copy(newBody[:2], body[:2])      // action + family
				copy(newBody[2:], body[2+seqLen:]) // payload after sequence
				body = newBody
			}
		}

		// Decode the packet body into a typed struct.
		pkt, pktFamily, pktAction, err := acornnet.DecodePacketBody(body)
		if err != nil {
			s.Logger().Warn("packet decode error",
				"family", family,
				"action", action,
				"error", err,
			)
			continue
		}

		// Route to the appropriate handler.
		if err := router.Route(s, pktFamily, pktAction, pkt); err != nil {
			s.Logger().Error("handler error",
				"family", pktFamily,
				"action", pktAction,
				"error", err,
			)
		}
	}
}
