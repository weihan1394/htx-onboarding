#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATES_DIR="$SCRIPT_DIR/templates"

# Detect container runtime — prefer podman, fall back to docker.
if command -v podman &>/dev/null; then
  RUNTIME=podman
  COMPOSE="podman compose"
elif command -v docker &>/dev/null; then
  RUNTIME=docker
  COMPOSE="docker compose"
else
  echo "Error: neither podman nor docker found." >&2
  exit 1
fi

echo "==> Using runtime: $RUNTIME"

echo "==> Stopping application services..."
cd "$TEMPLATES_DIR/services"
$COMPOSE down

echo "==> Stopping RedisInsight..."
cd "$TEMPLATES_DIR/redisinsight"
$COMPOSE down

echo "==> Stopping Valkey..."
cd "$TEMPLATES_DIR/valkey"
$COMPOSE down

echo "==> Stopping Temporal..."
cd "$TEMPLATES_DIR/temporal"
$COMPOSE down

echo "==> Stopping PostgreSQL..."
cd "$TEMPLATES_DIR/postgres"
$COMPOSE down

echo ""
echo "All services stopped."
