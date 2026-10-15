package session

// ClientState represents the current phase of a client connection.
type ClientState int

const (
	// StateUninitialized is the initial state before the Init handshake.
	StateUninitialized ClientState = iota
	// StateInitialized is set after the Init handshake completes.
	StateInitialized
	// StateAccepted is set after the client confirms the connection.
	StateAccepted
	// StateLoggedIn is set after a successful account login.
	StateLoggedIn
	// StateEnteringGame is set while loading character data.
	StateEnteringGame
	// StateInGame is the final state — the player is fully in the game world.
	StateInGame
)

func (s ClientState) String() string {
	switch s {
	case StateUninitialized:
		return "Uninitialized"
	case StateInitialized:
		return "Initialized"
	case StateAccepted:
		return "Accepted"
	case StateLoggedIn:
		return "LoggedIn"
	case StateEnteringGame:
		return "EnteringGame"
	case StateInGame:
		return "InGame"
	default:
		return "Unknown"
	}
}
