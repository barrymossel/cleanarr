#!/bin/sh

# Get PUID and PGID from environment (default to 1000)
PUID=${PUID:-1000}
PGID=${PGID:-1000}

echo "Setting up permissions with PUID=$PUID and PGID=$PGID"

# Update abc user/group IDs if they don't match
if [ "$(id -u abc)" != "$PUID" ]; then
    echo "Updating abc user ID to $PUID"
    usermod -o -u "$PUID" abc
fi

if [ "$(id -g abc)" != "$PGID" ]; then
    echo "Updating abc group ID to $PGID"
    groupmod -o -g "$PGID" abc
fi

# Ensure /config ownership
chown -R abc:abc /config 2>/dev/null || true

# Run application as abc user
echo "Starting Cleanarr as abc (PUID=$PUID, PGID=$PGID)"
exec su-exec abc:abc dotnet /app/Cleanarr.dll
