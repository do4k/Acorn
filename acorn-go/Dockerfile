# Build stage
FROM golang:1.25-alpine AS builder

RUN apk add --no-cache git

WORKDIR /src

# Cache module downloads.
COPY go.mod go.sum ./
RUN go mod download

# Build the server binary.
COPY . .
RUN CGO_ENABLED=0 go build -trimpath -ldflags="-s -w" -o /acorn-go ./cmd/server

# Runtime stage
FROM alpine:3.21

RUN apk add --no-cache ca-certificates tzdata

RUN addgroup -S acorn && adduser -S acorn -G acorn

WORKDIR /app

COPY --from=builder /acorn-go /app/acorn-go

# Default config; override with a bind mount.
COPY config.yaml /app/config.yaml

# Create directories for game data and the SQLite database.
# These should be bind-mounted or volume-mounted at runtime.
RUN mkdir -p /app/data/pub /app/data/maps && chown -R acorn:acorn /app

USER acorn

# TCP port (EO client connections)
EXPOSE 8078
# WebSocket port (browser / nginx proxy)
EXPOSE 8079
# Debug port (pprof, /healthz, /metrics)
EXPOSE 6060

ENTRYPOINT ["/app/acorn-go"]
CMD ["--config", "/app/config.yaml"]
