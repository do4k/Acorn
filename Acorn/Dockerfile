# Stage 1: Base runtime image
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine AS base
WORKDIR /app
EXPOSE 8078 8079

# Create app user and directories
RUN addgroup -g 1000 acorn && \
    adduser -u 1000 -G acorn -s /bin/sh -D acorn && \
    mkdir -p /app/mnt /app/Data && \
    chown -R acorn:acorn /app

# Stage 2: Build image
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project file and restore dependencies (better layer caching)
COPY ["Acorn.csproj", "./"]
RUN dotnet restore "Acorn.csproj"

# Copy remaining source files
COPY . .

# Build the application
RUN dotnet build "Acorn.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore

# Stage 3: Publish image
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Acorn.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    --no-build \
    /p:UseAppHost=false

# Stage 4: Final image
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish --chown=acorn:acorn /app/publish .

# Set proper permissions
RUN chmod -R 755 /app && \
    chmod -R 777 /app/mnt

# Switch to non-root user
USER acorn

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8078 || exit 1

ENTRYPOINT ["dotnet", "Acorn.dll"]