#!/bin/bash
set -e

export AWS_PAGER=""

REGION="ap-southeast-1"
CLUSTER="htx-onboarding"
APP_SERVICES=(temporal-ui hr-svc onboarding-svc workflow-svc)

echo "==> Starting ECS services in ${CLUSTER}..."

# Temporal must be stable before app services start
echo "    Starting temporal..."
aws ecs update-service \
  --cluster "${CLUSTER}" \
  --service temporal \
  --desired-count 1 \
  --region "${REGION}" > /dev/null

echo "    Waiting for temporal to be stable..."
aws ecs wait services-stable \
  --cluster "${CLUSTER}" \
  --services temporal \
  --region "${REGION}"
echo "    Temporal ready."

echo ""
for svc in "${APP_SERVICES[@]}"; do
  echo "    Starting ${svc}..."
  aws ecs update-service \
    --cluster "${CLUSTER}" \
    --service "${svc}" \
    --desired-count 1 \
    --region "${REGION}" > /dev/null
done

echo ""
echo "==> Waiting for all app services to be stable..."
aws ecs wait services-stable \
  --cluster "${CLUSTER}" \
  --services "${APP_SERVICES[@]}" \
  --region "${REGION}"

echo ""
echo "All services running."
