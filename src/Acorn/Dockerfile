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

# Copy project files and restore dependencies (better layer caching)
COPY ["src/Acorn/Acorn.csproj", "src/Acorn/"]
COPY ["src/Acorn.Shared/Acorn.Shared.csproj", "src/Acorn.Shared/"]
COPY ["src/Acorn.Domain/Acorn.Domain.csproj", "src/Acorn.Domain/"]
COPY ["src/Acorn.Database/Acorn.Database.csproj", "src/Acorn.Database/"]
RUN dotnet restore "src/Acorn/Acorn.csproj"

# Copy remaining source files
COPY src/Acorn/ src/Acorn/
COPY src/Acorn.Shared/ src/Acorn.Shared/
COPY src/Acorn.Domain/ src/Acorn.Domain/
COPY src/Acorn.Database/ src/Acorn.Database/

# Stage 3: Publish image
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
RUN dotnet publish "src/Acorn/Acorn.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
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

# Health check - verify the server is listening on port 8078
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD nc -z localhost 8078 || exit 1

ENTRYPOINT ["dotnet", "Acorn.dll"]