package handler

import (
	"context"
	"fmt"
	"strings"

	"github.com/acorn-server/acorn-go/internal/database"
	"github.com/acorn-server/acorn-go/internal/session"
	eonet "github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/client"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/server"
)

// AccountRequest handles PACKET_ACCOUNT + PACKET_REQUEST.
// This is the first step of account creation — the client checks if a username is available.
//
// eoserv behaviour:
// - Validates username via Player::ValidName (a-z, space, 0-9)
// - Checks if account already exists
// - On success, sends reply_code = session_id (value > 9), which eolib-go treats
//   as the Default case containing a SequenceStart byte.
type AccountRequest struct {
	DB *database.DB
}

func (h *AccountRequest) Handle(s *session.Session, pkt eonet.Packet) error {
	reqPkt, ok := pkt.(*client.AccountRequestClientPacket)
	if !ok {
		return fmt.Errorf("expected AccountRequestClientPacket, got %T", pkt)
	}

	username := strings.ToLower(reqPkt.Username)

	// Validate the username (eoserv: Player::ValidName).
	if !ValidateAccountName(username) {
		return s.Send(&server.AccountReplyServerPacket{
			ReplyCode:     server.AccountReply_NotApproved,
			ReplyCodeData: &server.AccountReplyReplyCodeDataNotApproved{},
		})
	}

	// Check if the account already exists.
	exists, err := h.DB.AccountExists(context.Background(), username)
	if err != nil {
		return fmt.Errorf("checking account exists: %w", err)
	}
	if exists {
		return s.Send(&server.AccountReplyServerPacket{
			ReplyCode:     server.AccountReply_Exists,
			ReplyCodeData: &server.AccountReplyReplyCodeDataExists{},
		})
	}

	// Success: send the session ID as the reply code.
	// eoserv sends: AddShort(session_id) + AddChar(seqStart) + AddString("OK")
	// In eolib-go, reply_code > 9 hits the Default case with SequenceStart field.
	return s.Send(&server.AccountReplyServerPacket{
		ReplyCode: server.AccountReply(s.ID()),
		ReplyCodeData: &server.AccountReplyReplyCodeDataDefault{
			SequenceStart: 0, // Sequence start byte (eoserv: AccountReplyNewSequence).
		},
	})
}

// AccountCreate handles PACKET_ACCOUNT + PACKET_CREATE.
// This is the second step — the client sends the full registration details.
//
// eoserv read order: session_id(short), unknown(byte), username, password,
// fullname, location, email, computer, hdid — all as break strings.
type AccountCreate struct {
	DB *database.DB
}

func (h *AccountCreate) Handle(s *session.Session, pkt eonet.Packet) error {
	createPkt, ok := pkt.(*client.AccountCreateClientPacket)
	if !ok {
		return fmt.Errorf("expected AccountCreateClientPacket, got %T", pkt)
	}

	username := strings.ToLower(createPkt.Username)
	password := createPkt.Password

	// Validate username.
	if !ValidateAccountName(username) {
		return s.Send(&server.AccountReplyServerPacket{
			ReplyCode:     server.AccountReply_NotApproved,
			ReplyCodeData: &server.AccountReplyReplyCodeDataNotApproved{},
		})
	}

	// Check if account already exists (re-check, matching eoserv).
	exists, err := h.DB.AccountExists(context.Background(), username)
	if err != nil {
		return fmt.Errorf("checking account exists: %w", err)
	}
	if exists {
		return s.Send(&server.AccountReplyServerPacket{
			ReplyCode:     server.AccountReply_Exists,
			ReplyCodeData: &server.AccountReplyReplyCodeDataExists{},
		})
	}

	// Create the account.
	_, err = h.DB.CreateAccount(context.Background(), username, password)
	if err != nil {
		return fmt.Errorf("creating account: %w", err)
	}

	s.Logger().Info("account created", "username", username)

	return s.Send(&server.AccountReplyServerPacket{
		ReplyCode:     server.AccountReply_Created,
		ReplyCodeData: &server.AccountReplyReplyCodeDataCreated{},
	})
}
