package net

import (
	"fmt"
	"io"
	"sync"

	"github.com/ethanmoffat/eolib-go/v3/data"
	"github.com/ethanmoffat/eolib-go/v3/encrypt"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net"
	"github.com/ethanmoffat/eolib-go/v3/protocol/net/client"
)

// Codec handles EO protocol framing and per-connection encryption state.
//
// The EO wire format is:
//
//	[len_byte1][len_byte2][action][family][sequence?][...payload...]
//
// The 2-byte length prefix is EO-number-encoded and covers everything after it.
// After the Init handshake establishes encryption multiples, all packet bodies
// (excluding the length prefix) are encrypted.
type Codec struct {
	mu sync.Mutex

	// Encryption multiples, zero until Init handshake completes.
	ClientEncryptionMulti int
	ServerEncryptionMulti int

	// Whether encryption is active (set after sending Init reply).
	encryptionActive bool
}

// NewCodec creates a new Codec with encryption disabled.
func NewCodec() *Codec {
	return &Codec{}
}

// SetEncryptionMultiples stores the negotiated encryption multiples and activates encryption.
func (c *Codec) SetEncryptionMultiples(clientMulti, serverMulti int) {
	c.mu.Lock()
	defer c.mu.Unlock()
	c.ClientEncryptionMulti = clientMulti
	c.ServerEncryptionMulti = serverMulti
	c.encryptionActive = true
}

// IsEncryptionActive returns whether encryption has been negotiated.
func (c *Codec) IsEncryptionActive() bool {
	c.mu.Lock()
	defer c.mu.Unlock()
	return c.encryptionActive
}

// ReadPacket reads a single EO protocol packet from the connection.
// It handles the 2-byte length prefix and decryption.
// Returns the raw decrypted bytes (action + family + sequence + payload).
func (c *Codec) ReadPacket(r io.Reader) ([]byte, error) {
	// Read the 2-byte EO-encoded length prefix.
	lenBuf := make([]byte, 2)
	if _, err := io.ReadFull(r, lenBuf); err != nil {
		return nil, fmt.Errorf("reading length prefix: %w", err)
	}

	packetLen := data.DecodeNumber(lenBuf)
	if packetLen < 2 {
		return nil, fmt.Errorf("packet too short: length %d", packetLen)
	}
	if packetLen > 65535 {
		return nil, fmt.Errorf("packet too large: length %d", packetLen)
	}

	// Read the packet body.
	body := make([]byte, packetLen)
	if _, err := io.ReadFull(r, body); err != nil {
		return nil, fmt.Errorf("reading packet body (%d bytes): %w", packetLen, err)
	}

	// Decrypt if encryption is active.
	// Init packets arrive before encryption is activated, so the encryptionActive
	// flag alone is sufficient — no need to peek at (encrypted) action/family bytes.
	c.mu.Lock()
	active := c.encryptionActive
	clientMulti := c.ClientEncryptionMulti
	c.mu.Unlock()

	if active {
		body = encrypt.FlipMsb(body)
		body = encrypt.Deinterleave(body)
		var err error
		body, err = encrypt.SwapMultiples(body, clientMulti)
		if err != nil {
			return nil, fmt.Errorf("decrypting packet: %w", err)
		}
	}

	return body, nil
}

// WritePacket serializes and sends an EO protocol packet.
// It handles encryption and the 2-byte length prefix.
func (c *Codec) WritePacket(w io.Writer, pkt net.Packet) error {
	// Serialize the packet body: [action][family][...serialized data...]
	writer := data.NewEoWriter()
	if err := writer.AddByte(int(pkt.Action())); err != nil {
		return fmt.Errorf("writing action byte: %w", err)
	}
	if err := writer.AddByte(int(pkt.Family())); err != nil {
		return fmt.Errorf("writing family byte: %w", err)
	}
	if err := pkt.Serialize(writer); err != nil {
		return fmt.Errorf("serializing packet: %w", err)
	}

	body := writer.Array()

	// Encrypt if active and not an Init packet.
	isInit := pkt.Action() == net.PacketAction_Init && pkt.Family() == net.PacketFamily_Init

	c.mu.Lock()
	active := c.encryptionActive
	serverMulti := c.ServerEncryptionMulti
	c.mu.Unlock()

	if active && !isInit {
		var err error
		body, err = encrypt.SwapMultiples(body, serverMulti)
		if err != nil {
			return fmt.Errorf("encrypting packet (swap): %w", err)
		}
		body = encrypt.Interleave(body)
		body = encrypt.FlipMsb(body)
	}

	// Prepend the 2-byte EO-encoded length.
	lenBytes := data.EncodeNumber(len(body))

	frame := make([]byte, 0, 2+len(body))
	frame = append(frame, lenBytes[:2]...)
	frame = append(frame, body...)

	if _, err := w.Write(frame); err != nil {
		return fmt.Errorf("writing packet: %w", err)
	}

	return nil
}

// DecodePacketBody parses the decrypted body bytes into a typed client packet.
// It reads action/family, resolves the packet struct, and deserializes it.
// Returns the packet and the (action, family) for routing.
func DecodePacketBody(body []byte) (net.Packet, net.PacketFamily, net.PacketAction, error) {
	if len(body) < 2 {
		return nil, 0, 0, fmt.Errorf("packet body too short: %d bytes", len(body))
	}

	reader := data.NewEoReader(body)
	action := net.PacketAction(reader.GetByte())
	family := net.PacketFamily(reader.GetByte())

	// Create the appropriate packet struct from the family/action.
	pkt, err := client.PacketFromId(family, action)
	if err != nil {
		return nil, family, action, fmt.Errorf("unknown packet family=%d action=%d: %w", family, action, err)
	}

	// Slice the reader to the remaining data for deserialization.
	dataReader, err := reader.SliceFromCurrent()
	if err != nil {
		return nil, family, action, fmt.Errorf("slicing packet data: %w", err)
	}

	if err := pkt.Deserialize(dataReader); err != nil {
		return nil, family, action, fmt.Errorf("deserializing packet family=%d action=%d: %w", family, action, err)
	}

	return pkt, family, action, nil
}
