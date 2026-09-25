# EO Protocol Reference

Notes on the Endless Online network protocol, compiled from eoserv (C++) and eolib-go.

## Packet Framing

Every packet on the wire:

```
[length_hi][length_lo][action][family][...encrypted body...]
```

The 2-byte length prefix is EO-number-encoded (NOT raw uint16). It covers everything after itself: action + family + optional sequence + payload.

## Init Handshake

The first packet exchange is unencrypted.

### Client -> Server: `Init_Init` (family=255, action=255)

| Field | Type | Description |
|-------|------|-------------|
| Challenge | Three (3 bytes) | Random number for verification |
| Version | Version struct | Major, Minor, Patch |
| Hdid | Prefixed string | Hardware ID |

### Server -> Client: `Init_Init` (family=255, action=255)

| Field | Type | Description |
|-------|------|-------------|
| ReplyCode | Byte | 2 = OK |
| Seq1 | Byte | Sequence start component 1 |
| Seq2 | Byte | Sequence start component 2 |
| ServerEncryptionMultiple | Byte | 6-12 |
| ClientEncryptionMultiple | Byte | 6-12 |
| PlayerId | Short | Session ID |
| ChallengeResponse | Three | `ServerVerificationHash(challenge)` |

After this packet, encryption is activated on both sides.

### Client -> Server: `Connection_Accept` (family=1, action=2)

| Field | Type | Description |
|-------|------|-------------|
| ClientEncryptionMultiple | Short | Echo back |
| ServerEncryptionMultiple | Short | Echo back |
| PlayerId | Short | Echo back |

Server verifies all three values match.

## Sequence System

After init, every non-init packet includes a sequence number.

**Initial sequence:** `value = seq1 * 7 + seq2 - 13` (from Init handshake)

**Sequence counter:** Cycles 0-9, so actual sequence = `start + counter`

**Encoding:** If value >= 253 (CHAR_MAX), encoded as EO Short (2 bytes); otherwise EO Char (1 byte).

**Resets:**
- Init: `seq1 * 7 + seq2 - 13`, random in 0-1757
- Ping: `seq1 - seq2`, random in 0-1757
- Account reply: random in 0-240

## Encryption

Applied to the packet body (everything after the 2-byte length prefix).

### Operations (from eolib-go `encrypt` package)

- **FlipMSB**: XOR bit 7 of each byte (symmetric)
- **Interleave**: Rearrange bytes in a specific weaving pattern
- **Deinterleave**: Reverse of Interleave
- **SwapMultiples**: Reverse contiguous groups of bytes at positions that are multiples of the key

### Server receiving (decrypt):
```
FlipMSB(data) -> Deinterleave(data) -> SwapMultiples(data, clientMulti)
```

### Server sending (encrypt):
```
SwapMultiples(data, serverMulti) -> Interleave(data) -> FlipMSB(data)
```

## Packet Families

| Value | Name | Description |
|-------|------|-------------|
| 1 | Connection | Handshake, ping/pong |
| 2 | Account | Create, login |
| 3 | Character | Create, delete, select |
| 4 | Login | Login request/response |
| 5 | Welcome | Enter game sequence |
| 6 | Walk | Player movement |
| 11 | Attack | Melee combat |
| 12 | Spell | Spell casting |
| 14 | Item | Item interactions |
| 18 | Talk | Chat messages |
| 26 | Npc | NPC interactions |
| 255 | Init | Init handshake |

## Packet Actions

| Value | Name | Typical Use |
|-------|------|-------------|
| 1 | Request | Client initiating |
| 2 | Accept | Confirmation |
| 3 | Reply | Server response |
| 8 | Player | Server push |
| 240 | Ping | Keep-alive |
| 241 | Pong | Keep-alive response |
| 255 | Init | Init handshake |

## Number Encoding

EO integers use a custom base-253 encoding:

```
Byte value 0   -> decoded as 128
Byte value 254 -> "unused" (treated as 1)
All others     -> value as-is
Then subtract 1 from each decoded byte
```

Final value: `b1 + b2*253 + b3*253^2 + b4*253^3`

This encoding avoids `0x00` and `0xFF` in the data stream (both reserved).

## String Encoding

Encoded strings are:
1. Each byte is mapped: `byte = 0xFF - byte` (bitwise inversion)
2. The byte array is reversed

`0xFF` serves as a field delimiter ("break byte") between strings in packet payloads. The EoReader's chunked mode handles this.
