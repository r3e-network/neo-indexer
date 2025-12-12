#!/bin/bash
# Neo Mainnet Node with BlockStateIndexer Plugin
# Loads environment variables and runs neo-cli

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Load environment variables from .env
if [ -f .env ]; then
    echo "Loading environment variables from .env..."
    set -a
    source .env
    set +a
else
    echo "Warning: .env file not found!"
fi

# Display loaded config
echo "=== StateRecorder Configuration ==="
echo "  ENABLED: ${NEO_STATE_RECORDER__ENABLED:-not set}"
echo "  SUPABASE_URL: ${NEO_STATE_RECORDER__SUPABASE_URL:-not set}"
echo "  UPLOAD_MODE: ${NEO_STATE_RECORDER__UPLOAD_MODE:-not set}"
echo "  CONNECTION_STRING: ${NEO_STATE_RECORDER__SUPABASE_CONNECTION_STRING:+[SET]}"
echo ""

# Change to neo-cli output directory
cd bin/Neo.CLI/net9.0

# Copy mainnet config as active config
cp config.mainnet.json config.json

# Warn if recorder is enabled but storage wrapper isn't configured
if [ "${NEO_STATE_RECORDER__ENABLED:-false}" = "true" ]; then
    if ! grep -q '"Engine"[[:space:]]*:[[:space:]]*"RecordingStore"' config.json; then
        echo "Warning: NEO_STATE_RECORDER__ENABLED is true but Storage.Engine is not RecordingStore."
        echo "         Storage reads will not be captured unless you edit config.json."
        echo ""
    fi
fi

echo "Starting Neo Mainnet Node..."
echo "Network ID: 860833102 (0x334F454E)"
echo ""

# Run neo-cli
./neo-cli
