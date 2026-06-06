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

echo "==> Stopping Temporal..."
cd "$TEMPLATES_DIR/temporal"
$COMPOSE down

echo "==> Removing Temporal data volume..."
rm -rf "$TEMPLATES_DIR/temporal/data"

echo "==> Resetting Temporal databases..."
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "DROP DATABASE IF EXISTS temporal_db;"
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "DROP DATABASE IF EXISTS temporal_visibility;"
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "CREATE DATABASE temporal_db;"
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "CREATE DATABASE temporal_visibility;"
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "GRANT ALL PRIVILEGES ON DATABASE temporal_db TO htx_svc;"
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "GRANT ALL PRIVILEGES ON DATABASE temporal_visibility TO htx_svc;"

echo "==> Starting Temporal..."
$COMPOSE up -d

echo "==> Waiting for Temporal to be ready..."
until $RUNTIME exec temporal-admin-tools temporal operator namespace list --address temporal:7233 &>/dev/null; do
  printf "."
  sleep 2
done
echo ""

echo "==> Registering htx-onboarding namespace..."
$RUNTIME exec temporal-admin-tools temporal operator namespace create \
  -n htx-onboarding \
  --description "HTX Employee Onboarding System" \
  --address temporal:7233 2>/dev/null || echo "   (namespace already exists)"

echo "==> Registering search attributes..."
$RUNTIME exec temporal-admin-tools temporal operator search-attribute create \
  --name EmployeeName --type Text --namespace htx-onboarding 2>/dev/null || echo "   (EmployeeName already registered)"
$RUNTIME exec temporal-admin-tools temporal operator search-attribute create \
  --name EmployeeNumber --type Keyword --namespace htx-onboarding 2>/dev/null || echo "   (EmployeeNumber already registered)"
$RUNTIME exec temporal-admin-tools temporal operator search-attribute create \
  --name Department --type Keyword --namespace htx-onboarding 2>/dev/null || echo "   (Department already registered)"

echo ""
echo "Temporal reset complete."
echo "  Temporal UI: http://localhost:8080"
