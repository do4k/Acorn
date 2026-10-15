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

// LoginRequest handles PACKET_LOGIN + PACKET_REQUEST.
//
// eoserv read order: username (break string), password (break string).
// On success, responds with LOGIN_OK + character list.
type LoginRequest struct {
	DB      *database.DB
	Manager *session.Manager
}

func (h *LoginRequest) Handle(s *session.Session, pkt eonet.Packet) error {
	loginPkt, ok := pkt.(*client.LoginRequestClientPacket)
	if !ok {
		return fmt.Errorf("expected LoginRequestClientPacket, got %T", pkt)
	}

	username := strings.ToLower(loginPkt.Username)
	password := loginPkt.Password

	// Look up the account.
	account, err := h.DB.GetAccount(context.Background(), username)
	if err != nil {
		return fmt.Errorf("looking up account: %w", err)
	}
	if account == nil {
		return s.Send(&server.LoginReplyServerPacket{
			ReplyCode:     server.LoginReply_WrongUser,
			ReplyCodeData: &server.LoginReplyReplyCodeDataWrongUser{},
		})
	}

	// Verify password.
	if !h.DB.VerifyPassword(account, password) {
		return s.Send(&server.LoginReplyServerPacket{
			ReplyCode:     server.LoginReply_WrongUserPassword,
			ReplyCodeData: &server.LoginReplyReplyCodeDataWrongUserPassword{},
		})
	}

	// Check if already logged in (another session with same account).
	for _, other := range h.Manager.All() {
		if other.ID() != s.ID() && other.Account != nil && other.Account.Username == username {
			return s.Send(&server.LoginReplyServerPacket{
				ReplyCode:     server.LoginReply_LoggedIn,
				ReplyCodeData: &server.LoginReplyReplyCodeDataLoggedIn{},
			})
		}
	}

	// Load characters for this account.
	chars, err := h.DB.GetCharactersByAccount(context.Background(), account.ID)
	if err != nil {
		return fmt.Errorf("loading characters: %w", err)
	}

	// Set account on session.
	s.Account = &session.Account{
		ID:       account.ID,
		Username: account.Username,
	}
	s.SetState(session.StateLoggedIn)

	s.Logger().Info("login successful", "username", username, "characters", len(chars))

	// Build character list matching eoserv format.
	return s.Send(&server.LoginReplyServerPacket{
		ReplyCode: server.LoginReply_Ok,
		ReplyCodeData: &server.LoginReplyReplyCodeDataOk{
			Characters: buildCharacterList(chars),
		},
	})
}
