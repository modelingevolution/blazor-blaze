#!/bin/bash
# Run MudBlazor Sample App for testing VectorGraphics performance with MudBlazor

cd "$(dirname "$0")"

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=http://localhost:5200

echo "Starting MudBlazorSample on http://localhost:5200"
echo "Test page: http://localhost:5200/vector-test"
echo ""

dotnet run
