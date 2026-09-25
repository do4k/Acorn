package handler

import (
	"fmt"
	"math/rand"

	"github.com/acorn-server/acorn-go/internal/session"
	"github.com/ethanmoffat/eolib-go/v3/encrypt"
	"github.com/ethanmoffat/eolib-go/v3/packet"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/client"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/server"
)

// InitInit handles the Init_Init packet — the very first packet a client sends.
// It negotiates encryption multiples, initializes the packet sequencer, and
// sends back the server's verification hash so the client can confirm authenticity.
type InitInit struct{}

func (h *InitInit) Handle(s *session.Session, pkt eonet.Packet) error {
	initPkt, ok := pkt.(*client.InitInitClientPacket)
	if !ok {
		return fmt.Errorf("expected InitInitClientPacket, got %T", pkt)
	}

	if s.State() != session.StateUninitialized {
		s.Logger().Warn("init packet received in wrong state", "state", s.State())
		return nil
	}

	s.Logger().Info("init handshake",
		"challenge", initPkt.Challenge,
		"version", fmt.Sprintf("%d.%d.%d", initPkt.Version.Major, initPkt.Version.Minor, initPkt.Version.Patch),
		"hdid", initPkt.Hdid,
	)

	// Generate encryption multiples (6-12, matching eoserv).
	rng := rand.New(rand.NewSource(rand.Int63()))
	clientMulti := rng.Intn(7) + 6
	serverMulti := rng.Intn(7) + 6

	// Generate the initial packet sequence.
	seq := packet.GenerateInitSequence(rng)
	s.Sequencer().SetSequenceStart(seq)

	// Store multiples on the session for later reference.
	s.ClientEncryptionMulti = clientMulti
	s.ServerEncryptionMulti = serverMulti

	// Compute the challenge response hash.
	challengeResponse := encrypt.ServerVerificationHash(initPkt.Challenge)

	// Build the server reply.
	response := &server.InitInitServerPacket{
		ReplyCode: server.InitReply_Ok,
		ReplyCodeData: &server.InitInitReplyCodeDataOk{
			Seq1:                     seq.Seq1(),
			Seq2:                     seq.Seq2(),
			ServerEncryptionMultiple: serverMulti,
			ClientEncryptionMultiple: clientMulti,
			PlayerId:                 s.ID(),
			ChallengeResponse:        challengeResponse,
		},
	}

	// Send the init reply BEFORE activating encryption.
	// The Init reply itself is not encrypted (both sides know this).
	if err := s.Send(response); err != nil {
		return fmt.Errorf("sending init reply: %w", err)
	}

	// Now activate encryption for all subsequent packets.
	s.GetCodec().SetEncryptionMultiples(clientMulti, serverMulti)
	s.SetState(session.StateInitialized)

	s.Logger().Info("init handshake complete",
		"player_id", s.ID(),
		"client_multi", clientMulti,
		"server_multi", serverMulti,
	)

	return nil
}
