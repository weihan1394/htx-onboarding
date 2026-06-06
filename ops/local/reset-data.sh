#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "==> Resetting all data (Postgres + Temporal)..."
echo ""

bash "$SCRIPT_DIR/reset-postgres.sh"
echo ""
bash "$SCRIPT_DIR/reset-temporal.sh"

echo ""
echo "==> Reset complete. All seed data restored and Temporal cleared."
echo "    Run start-full.sh if any application services need to be restarted."
