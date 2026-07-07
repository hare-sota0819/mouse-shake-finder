#!/usr/bin/env bash
# Definition of done: build the whole solution and run all tests.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet build MouseShakeFinder.sln
dotnet test tests/MouseShakeFinder.Core.Tests
