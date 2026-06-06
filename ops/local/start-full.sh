#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATES_DIR="$SCRIPT_DIR/templates"

# shellcheck source=../helpers/check-prerequisites.sh
source "${SCRIPT_DIR}/../helpers/check-prerequisites.sh" --with-runtime

echo "==> Using runtime: $RUNTIME"

echo "==> Creating network..."
$RUNTIME network create htx-network 2>/dev/null || echo "   (network already exists)"

echo "==> Starting PostgreSQL..."
cd "$TEMPLATES_DIR/postgres"
$COMPOSE up -d

echo "==> Waiting for PostgreSQL to be healthy..."
until $RUNTIME exec postgres pg_isready -U htx_svc -d postgres -q; do
  printf "."
  sleep 2
done
echo ""

echo "==> Setting up htx database permissions..."
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "GRANT CONNECT ON DATABASE htx TO hr_svc;" 2>/dev/null || true
$RUNTIME exec postgres psql -U htx_svc -d postgres -c "GRANT CONNECT ON DATABASE htx TO onboarding_svc;" 2>/dev/null || true

echo "==> Running database migrations..."
cd "$TEMPLATES_DIR/services"
$COMPOSE run --rm hr-db-migrate
$COMPOSE run --rm onboarding-db-migrate

echo "==> Starting Temporal..."
cd "$TEMPLATES_DIR/temporal"
$COMPOSE up -d

echo "==> Waiting for Temporal to be ready..."
until timeout 5 $RUNTIME exec temporal-admin-tools temporal operator namespace list --address temporal:7233 &>/dev/null; do
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

echo "==> Starting Valkey..."
cd "$TEMPLATES_DIR/valkey"
$COMPOSE up -d

echo "==> Starting RedisInsight..."
cd "$TEMPLATES_DIR/redisinsight"
$COMPOSE up -d

echo "==> Starting application services..."
cd "$TEMPLATES_DIR/services"
$COMPOSE up --build -d


echo ""
echo "All services are up."
echo ""
echo "  HR Web:         http://localhost:3000"
echo "  HR API:         http://localhost:5001/swagger"
echo "  Onboarding API: http://localhost:5002/swagger"
echo "  Workflow API:   http://localhost:5003/swagger"
echo "  Temporal UI:    http://localhost:8080"
echo "  Valkey:         localhost:6379"
echo "  RedisInsight:   http://localhost:5540"
