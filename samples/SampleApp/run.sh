#!/bin/bash
set -e
cd "$(dirname "$0")"

# Kill any existing process on port 5100
fuser -k 5100/tcp 2>/dev/null || true
sleep 1

# Clean everything to avoid integrity hash mismatches
echo "Cleaning build artifacts..."
dotnet clean 2>/dev/null || true
rm -rf bin obj publish
rm -rf ../SampleApp.Client/bin ../SampleApp.Client/obj

# Build Release with AOT compilation for WASM client
echo "Building Release with AOT (this takes ~5 minutes)..."
dotnet publish -c Release -o ./publish

# Run the published app in background
echo "Starting server in background..."
URL="http://localhost:5100"
nohup dotnet ./publish/SampleApp.dll --urls "$URL" > server.log 2>&1 &
SERVER_PID=$!

# Wait for server to be ready (max 30 seconds)
for i in {1..30}; do
    if grep -q "Now listening on:" server.log 2>/dev/null; then
        echo "Server running at $URL (PID: $SERVER_PID)"
        echo "Log: $(pwd)/server.log"
        exit 0
    fi
    sleep 1
done

echo "Warning: Server may not have started correctly. Check server.log"
echo "PID: $SERVER_PID"
