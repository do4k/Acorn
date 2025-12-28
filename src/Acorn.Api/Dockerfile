# Stage 1: Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS base
WORKDIR /app
EXPOSE 8080

# Create app user and directories
RUN addgroup -g 1000 acorn && \
    adduser -u 1000 -G acorn -s /bin/sh -D acorn && \
    chown -R acorn:acorn /app

# Stage 2: Build image
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files and restore dependencies (better layer caching)
COPY ["src/Acorn.Api/Acorn.Api.csproj", "src/Acorn.Api/"]
COPY ["src/Acorn.Shared/Acorn.Shared.csproj", "src/Acorn.Shared/"]
RUN dotnet restore "src/Acorn.Api/Acorn.Api.csproj"

# Copy remaining source files
COPY src/Acorn.Api/ src/Acorn.Api/
COPY src/Acorn.Shared/ src/Acorn.Shared/

# Stage 3: Publish image
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
RUN dotnet publish "src/Acorn.Api/Acorn.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Stage 4: Final image
FROM base AS final
WORKDIR /app

# Copy published application
COPY --from=publish --chown=acorn:acorn /app/publish .

# Switch to non-root user
USER acorn

# Health check using the health endpoint
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "Acorn.Api.dll"]

