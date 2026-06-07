#!/bin/bash
set -e

export AWS_PAGER=""

REGION="ap-southeast-1"
CLUSTER="htx-onboarding"
SERVICES=(temporal temporal-ui hr-svc onboarding-svc workflow-svc)

echo "==> Scaling all ECS services to 0 in ${CLUSTER}..."

for svc in "${SERVICES[@]}"; do
  echo "    Stopping ${svc}..."
  aws ecs update-service \
    --cluster "${CLUSTER}" \
    --service "${svc}" \
    --desired-count 0 \
    --region "${REGION}" > /dev/null
done

echo ""
echo "==> Waiting for all tasks to drain..."
aws ecs wait services-stable \
  --cluster "${CLUSTER}" \
  --services "${SERVICES[@]}" \
  --region "${REGION}"

echo ""
echo "All services stopped. RDS, ElastiCache, ALB, and VPC endpoints are still running (~\$93/month)."
