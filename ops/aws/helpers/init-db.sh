#!/bin/bash
set -e

# One-shot script to initialize the RDS database.
# Runs a Fargate task inside the VPC — the only way to reach RDS from outside.
#
# Prerequisites:
#   1. Stacks deployed (./infra/1-deploy.sh)
#   2. db-init image built and pushed to ECR:
#        ECR=<account>.dkr.ecr.ap-southeast-1.amazonaws.com
#        docker build -t ${ECR}/htx-onboarding/db-init:latest infra/helpers/db-init/
#        docker push ${ECR}/htx-onboarding/db-init:latest

REGION="ap-southeast-1"
LOG_GROUP="/ecs/htx-onboarding/db-init"

# ── Fetch values from individual stacks ──────────────────────────────────────
echo "==> Fetching stack values..."
ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
ECR="${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com"

stack_output() {
  aws cloudformation describe-stacks \
    --stack-name "$1" --region "${REGION}" \
    --query "Stacks[0].Outputs[?OutputKey==\`$2\`].OutputValue" \
    --output text
}

DB_HOST=$(stack_output htx-onboarding-storage DBEndpoint)
ECS_CLUSTER=$(stack_output htx-onboarding-compute-infra ECSClusterName)
SUBNET=$(stack_output htx-onboarding-network AppSubnet1Id)
ECS_SG=$(stack_output htx-onboarding-network ECSSecurityGroupId)

echo "  DB:      ${DB_HOST}"
echo "  Cluster: ${ECS_CLUSTER}"
echo "  Subnet:  ${SUBNET}"
echo "  SG:      ${ECS_SG}"

# ── Read credentials from Secrets Manager ────────────────────────────────────
echo ""
echo "==> Reading credentials from Secrets Manager..."

get_secret_field() {
  aws secretsmanager get-secret-value \
    --secret-id "$1" --region "${REGION}" \
    --query SecretString --output text \
  | python3 -c "import sys,json; print(json.load(sys.stdin)['$2'])"
}

ADMIN_USER=$(get_secret_field "htx-onboarding/prod/db/admin" "username")
ADMIN_PWD=$(get_secret_field "htx-onboarding/prod/db/admin" "password")
HR_SVC_PWD=$(get_secret_field "htx-onboarding/prod/db/hr-svc" "password")
ONBOARDING_SVC_PWD=$(get_secret_field "htx-onboarding/prod/db/onboarding-svc" "password")

# ── Ensure CloudWatch log group exists ────────────────────────────────────────
echo ""
echo "==> Ensuring log group exists..."
aws logs create-log-group --log-group-name "${LOG_GROUP}" --region "${REGION}" 2>/dev/null || true
aws logs put-retention-policy --log-group-name "${LOG_GROUP}" --retention-in-days 14 --region "${REGION}"

# ── Register one-shot task definition ────────────────────────────────────────
echo ""
echo "==> Registering task definition..."
EXEC_ROLE_ARN="arn:aws:iam::${ACCOUNT_ID}:role/htx-onboarding-task-execution"

CONTAINER_DEFS=$(jq -n \
  --arg image "${ECR}/htx-onboarding/db-init:latest" \
  --arg log_group "${LOG_GROUP}" \
  --arg region "${REGION}" \
  '[{
    name: "db-init",
    image: $image,
    essential: true,
    logConfiguration: {
      logDriver: "awslogs",
      options: {
        "awslogs-group": $log_group,
        "awslogs-region": $region,
        "awslogs-stream-prefix": "ecs"
      }
    }
  }]')

TASK_DEF_ARN=$(aws ecs register-task-definition \
  --region "${REGION}" \
  --family "htx-onboarding-db-init" \
  --network-mode awsvpc \
  --requires-compatibilities FARGATE \
  --cpu 256 --memory 512 \
  --execution-role-arn "${EXEC_ROLE_ARN}" \
  --container-definitions "${CONTAINER_DEFS}" \
  --query 'taskDefinition.taskDefinitionArn' \
  --output text)

echo "  Task def: ${TASK_DEF_ARN}"

# ── Run the task with credentials injected as env vars ───────────────────────
echo ""
echo "==> Launching init task inside VPC..."

OVERRIDES=$(jq -n \
  --arg host "${DB_HOST}" \
  --arg user "${ADMIN_USER}" \
  --arg pwd "${ADMIN_PWD}" \
  --arg hr_pwd "${HR_SVC_PWD}" \
  --arg ob_pwd "${ONBOARDING_SVC_PWD}" \
  '{
    containerOverrides: [{
      name: "db-init",
      environment: [
        {name: "DB_HOST",                value: $host},
        {name: "DB_ADMIN_USER",          value: $user},
        {name: "DB_ADMIN_PASSWORD",      value: $pwd},
        {name: "HR_SVC_PASSWORD",        value: $hr_pwd},
        {name: "ONBOARDING_SVC_PASSWORD",value: $ob_pwd}
      ]
    }]
  }')

TASK_ARN=$(aws ecs run-task \
  --cluster "${ECS_CLUSTER}" \
  --task-definition "${TASK_DEF_ARN}" \
  --launch-type FARGATE \
  --region "${REGION}" \
  --network-configuration "awsvpcConfiguration={subnets=[${SUBNET}],securityGroups=[${ECS_SG}],assignPublicIp=DISABLED}" \
  --overrides "${OVERRIDES}" \
  --query 'tasks[0].taskArn' \
  --output text)

echo "  Task: ${TASK_ARN}"

# ── Wait and check result ─────────────────────────────────────────────────────
echo ""
echo "==> Waiting for task to complete..."
aws ecs wait tasks-stopped \
  --cluster "${ECS_CLUSTER}" \
  --tasks "${TASK_ARN}" \
  --region "${REGION}"

EXIT_CODE=$(aws ecs describe-tasks \
  --cluster "${ECS_CLUSTER}" \
  --tasks "${TASK_ARN}" \
  --region "${REGION}" \
  --query 'tasks[0].containers[0].exitCode' \
  --output text)

echo ""
if [ "${EXIT_CODE}" = "0" ]; then
  echo "==> Database initialized successfully."
else
  echo "ERROR: Init task failed with exit code ${EXIT_CODE}"
  echo "       Check logs: aws logs tail ${LOG_GROUP} --region ${REGION} --follow"
  exit 1
fi
