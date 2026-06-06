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

echo "==> Dropping and recreating htx database..."
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'htx' AND pid <> pg_backend_pid();"
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "DROP DATABASE IF EXISTS htx;"
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "CREATE DATABASE htx;"
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "GRANT ALL PRIVILEGES ON DATABASE htx TO htx_svc;"
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "GRANT CONNECT ON DATABASE htx TO hr_svc;"
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "GRANT CONNECT ON DATABASE htx TO onboarding_svc;"

echo "==> Running migrations..."
cd "$TEMPLATES_DIR/services"
$COMPOSE build --no-cache hr-db-migrate onboarding-db-migrate
$COMPOSE run --rm hr-db-migrate
$COMPOSE run --rm onboarding-db-migrate

echo ""
echo "Database reset complete. All seed data has been restored."
