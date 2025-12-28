# Build frontend
FROM node:20-alpine AS frontend-build
WORKDIR /src/frontend

COPY frontend/package*.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# Build backend
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS backend-build
WORKDIR /src

COPY backend/*.csproj ./backend/
RUN cd backend && dotnet restore

COPY backend/ ./backend/
COPY --from=frontend-build /src/frontend/build ./backend/wwwroot/
RUN cd backend && dotnet publish -c Release -o /app

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app

RUN apk add --no-cache \
    icu-libs \
    sqlite-libs \
    ca-certificates \
    tzdata \
    shadow \
    su-exec

COPY --from=backend-build /app ./
COPY VERSION ./

# Create abc user/group (like linuxserver.io containers)
RUN addgroup -g 1000 abc && \
    adduser -u 1000 -G abc -h /config -D abc

# Create required directories
RUN mkdir -p /config && \
    chown -R abc:abc /config /app

ENV ASPNETCORE_URLS=http://+:7979 \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=en_US.UTF-8 \
    LANG=en_US.UTF-8 \
    PUID=1000 \
    PGID=1000

VOLUME ["/config"]
EXPOSE 7979

# Entrypoint script to handle PUID/PGID
COPY docker-entrypoint.sh /
RUN chmod +x /docker-entrypoint.sh

ENTRYPOINT ["/docker-entrypoint.sh"]
